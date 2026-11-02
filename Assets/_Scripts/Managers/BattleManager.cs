using AutoBattler.Battle.Units;
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
        [Header("전투 종료 후 준비 전환")]
        [SerializeField, Min(0f)] private float resultDelaySeconds=5f;
        private CancellationTokenSource preparationDelay;
        private UniTaskCompletionSource preparationCompletion;
        private Func<bool> advancePreparation;
        private int waitingRound;
        private double preparationDeadline;
        public bool IsWaitingForPreparation=>preparationDelay!=null;
        public float ResultDelaySeconds=>resultDelaySeconds;
        public float NextPreparationSeconds=>IsWaitingForPreparation
            ?Mathf.Max(0f,(float)(preparationDeadline-Time.realtimeSinceStartupAsDouble)):0f;
        private float deadline;
        private bool stopping;
        public bool IsRunning=>operation!=null;
        public float RemainingSeconds=>Mathf.Max(0f,deadline-Time.time);
        public void Initialize(BattleFlowController phases,UnitDragController input,UnitManager roster,AssetManager content,
            SynergyManager traits,AugmentManager choices,Action<string> message,Func<bool> advance,CancellationToken token)
        { flow=phases; drag=input; units=roster; assets=content; synergies=traits; augments=choices; publish=message; advancePreparation=advance; lifetime=token; }
        public void Bind(RunState current) { CancelPreparationDelay(); state=current; stopping=false; }
        public bool StartBattle()
        {
            if(stopping || state==null || operation!=null || flow.CurrentPhase!=BattlePhase.Preparation || state.IsOver || state.Augments.HasPending) return false;
            drag.CancelDrag();
            if(state.Board.DeployedCount==0) { publish("기물을 배치해 주세요."); return false; }
            synergies.Refresh(); Unit[] participants=units.Combatants;
            foreach(Unit unit in participants) unit.BeginBattle();
            AutoBattleController battle=new AutoBattleController(participants);
            if(!flow.TryStartCombat()) return false;
            state.CanPrepare=false; drag.enabled=true;
            if(SoundManager.TryGetExisting(out SoundManager sound)) sound.BeginBattle(participants);
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
                if(SoundManager.TryGetExisting(out SoundManager sound)) sound.EndBattle();
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
                if(SoundManager.TryGetExisting(out SoundManager sound)) sound.EndBattle();
                if(ReferenceEquals(operation,current)) operation=null;
                current.Dispose(); completion.TrySetResult();
            }
        }
        public void ScheduleNextPreparation()
        {
            if(stopping || state==null || !state.IsSettled || state.IsOver && !state.HasPendingRoundReward || flow.CurrentPhase!=BattlePhase.Result)
            { CancelPreparationDelay(); return; }
            if(preparationDelay!=null && waitingRound==state.Round) return;
            CancelPreparationDelay(); waitingRound=state.Round;
            // 결과 대기 중 전장 유닛도 확인·판매할 수 있도록 원래 배치로 복귀.
            units.RestorePreparation();
            preparationDeadline=Time.realtimeSinceStartupAsDouble+resultDelaySeconds;
            CancellationTokenSource current=CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            UniTaskCompletionSource completion=new UniTaskCompletionSource();
            preparationDelay=current; preparationCompletion=completion;
            WaitForPreparationAsync(state,waitingRound,current,completion).Forget();
        }
        private async UniTask WaitForPreparationAsync(RunState expected,int round,CancellationTokenSource current,UniTaskCompletionSource completion)
        {
            try
            {
                // 게임 배속·프레임 제한과 관계없이 실제 시간으로 대기.
                await UniTask.Delay(TimeSpan.FromSeconds(resultDelaySeconds),DelayType.Realtime,cancellationToken:current.Token);
                while(!stopping && ReferenceEquals(state,expected) && state.Round==round
                    && state.IsSettled && (state.HasPendingRoundReward || !state.IsOver) && flow.CurrentPhase==BattlePhase.Result)
                {
                    current.Token.ThrowIfCancellationRequested();
                    if(state.TryGrantRoundRewards()) publish(state.LastResult);
                    if(state.IsOver) return;
                    if(advancePreparation()) return;
                    // 저장 실패 시 결과를 유지하고 다시 시도하여 진행도 보호.
                    await UniTask.Delay(1000,ignoreTimeScale:true,cancellationToken:current.Token);
                }
            }
            catch(OperationCanceledException) { }
            catch(Exception exception) { Debug.LogException(exception,this); }
            finally
            {
                if(ReferenceEquals(preparationDelay,current)) preparationDelay=null;
                current.Dispose(); completion.TrySetResult();
            }
        }
        private void CancelPreparationDelay()
        {
            CancellationTokenSource current=preparationDelay; preparationDelay=null; waitingRound=0;
            current?.Cancel();
        }
        public bool Continue()
        {
            if(stopping || state==null || flow.CurrentPhase!=BattlePhase.Result || state.IsOver) return false;
            if(!state.TryNextRound()) return false;
            CancelPreparationDelay();
            units.RestorePreparation(); units.CreateEnemies(); flow.TryBeginPreparation(); augments.EnterPreparation(); synergies.Refresh();
            publish(state.Augments.HasPending?"증강 선택":"준비");
            return true;
        }
        public UniTask StopAsync()
        {
            if(SoundManager.TryGetExisting(out SoundManager sound)) sound.EndBattle();
            stopping=true; CancelPreparationDelay(); operation?.Cancel(); synergies?.EndBattle(); units?.StopCombat();
            return UniTask.WhenAll(battleCompletion!=null?battleCompletion.Task:UniTask.CompletedTask,
                preparationCompletion!=null?preparationCompletion.Task:UniTask.CompletedTask);
        }
        protected override void OnDestroy() { CancelPreparationDelay(); if(SoundManager.TryGetExisting(out SoundManager sound)) sound.EndBattle(); operation?.Cancel(); base.OnDestroy(); }
    }
}
