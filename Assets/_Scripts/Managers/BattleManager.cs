using System;
using AutoBattler.Audio;
using System.Threading;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Data.Snapshot;
using AutoBattler.Run;
using AutoBattler.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Managers
{
    /// <summary>자동 전투 시작·제한 시간·취소·정산과 다음 라운드 전환 담당.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleManager : Singleton<BattleManager>
    {
        private RunState state;
        private BattleFlowController flow;
        private UnitDragController drag;
        private UnitManager units;
        private AssetManager assets;
        private SynergyManager synergies;
        private AugmentManager augments;
        private Action<string> publish;
        private CancellationToken lifetime;
        private CancellationTokenSource operation;
        private UniTaskCompletionSource battleCompletion;
        private float deadline;
        private bool stopping;
        public bool IsRunning=>operation!=null;
        public float RemainingSeconds=>Mathf.Max(0f,deadline-Time.time);
        public void Initialize(BattleFlowController phases,UnitDragController input,UnitManager roster,AssetManager content,
            SynergyManager traits,AugmentManager choices,Action<string> message,CancellationToken token)
        { flow=phases; drag=input; units=roster; assets=content; synergies=traits; augments=choices; publish=message; lifetime=token; }
        public void Bind(RunState current) { state=current; stopping=false; }
        public bool StartBattle()
        {
            if(stopping || state==null || operation!=null || flow.CurrentPhase!=BattlePhase.Preparation || state.IsOver || state.Augments.HasPending) return false;
            drag.CancelDrag();
            if(state.Board.DeployedCount==0) { publish("기물을 배치해 주세요."); return false; }
            synergies.Refresh(); var participants=units.Combatants;
            foreach(var unit in participants) unit.BeginBattle();
            var battle=new AutoBattleController(participants);
            if(!flow.TryStartCombat()) return false;
            state.CanPrepare=false; drag.enabled=true;
            if(SoundManager.TryGetExisting(out var sound)) sound.BeginBattle(participants);
            SoundManager.PlayEffect(SoundId.BattleStart);
            assets.Visuals.BeginBattle(participants); synergies.BeginBattle(participants); augments.ApplyBattleStart(participants);
            operation=CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            battleCompletion=new UniTaskCompletionSource(); deadline=Time.time+state.Data.Rule(EconomyRule.CombatSeconds);
            publish("전투 중"); ExecuteAsync(battle,operation,battleCompletion).Forget(); return true;
        }
        private async UniTask ExecuteAsync(AutoBattleController battle,CancellationTokenSource current,UniTaskCompletionSource completion)
        {
            try
            {
                bool won=await battle.RunAsync(state.Data.Rule(EconomyRule.CombatSeconds),current.Token);
                if(stopping || !flow.TryFinishCombat()) return;
                current.Cancel(); synergies.EndBattle(); units.StopCombat();
                if(SoundManager.TryGetExisting(out var sound)) sound.EndBattle();
                if(state.TrySettle(won))
                { publish(state.LastResult); SoundManager.PlayEffect(won?SoundId.Victory:SoundId.Defeat); }
            }
            catch(OperationCanceledException) { }
            catch(Exception exception)
            {
                current.Cancel(); synergies.EndBattle(); Debug.LogException(exception,this);
                if(!stopping)
                { if(flow.TryFinishCombat()) state.TrySettle(false); publish("전투 오류가 발생했습니다."); }
            }
            finally
            {
                synergies.EndBattle();
                if(SoundManager.TryGetExisting(out var sound)) sound.EndBattle();
                if(ReferenceEquals(operation,current)) operation=null;
                current.Dispose(); completion.TrySetResult();
            }
        }
        public bool Continue()
        {
            if(stopping || state==null || flow.CurrentPhase!=BattlePhase.Result || state.IsOver) return false;
            if(!state.TryNextRound()) return false;
            units.RestorePreparation(); units.CreateEnemies(); flow.TryBeginPreparation(); augments.EnterPreparation(); synergies.Refresh();
            publish(state.Augments.HasPending?"증강 선택":"준비");
            return true;
        }
        public UniTask StopAsync()
        {
            if(SoundManager.TryGetExisting(out var sound)) sound.EndBattle();
            stopping=true; operation?.Cancel(); synergies?.EndBattle(); units?.StopCombat();
            return battleCompletion!=null ? battleCompletion.Task : UniTask.CompletedTask;
        }
        protected override void OnDestroy() { if(SoundManager.TryGetExisting(out var sound)) sound.EndBattle(); operation?.Cancel(); base.OnDestroy(); }
    }
}
