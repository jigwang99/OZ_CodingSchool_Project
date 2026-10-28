using System;
using System.Collections.Generic;
using System.Threading;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Skills;
using AutoBattler.Data.Skills;
using AutoBattler.Data.Snapshot;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    public enum SkillCancelReason { None, Stunned, Dead, NoTarget, RoundEnded, Returned }
    /// <summary>준비 시 마나 차감, 기절·대상 소멸 시 한 번 반환, 발동 후 효과 실행.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitSkill : MonoBehaviour
    {
        private sealed class CastOperation
        {
            public CancellationTokenSource Cancellation;
            public SkillCancelReason Reason;
            public int Spent;
            public bool Released;
        }
        private Unit unit;
        private UnitHealth boundHealth;
        private UnitEffects boundEffects;
        private DataRepository data;
        private BattleFlowController boundFlow;
        private CastOperation current;
        private float nextCastTime;
        public SkillDefinition Definition { get; private set; }
        public bool IsCasting=>current!=null;
        public bool CanCast=>isActiveAndEnabled && Definition!=null && unit!=null && unit.IsInCombat && unit.Health.IsAlive
            && !unit.Effects.IsStunned && !unit.Move.IsMoving && !unit.Combat.IsAttacking && !IsCasting
            && Time.time>=nextCastTime && unit.Mana.Current>=Definition.ManaCost;
        public event Action<SkillCastContext> CastStarted;
        public event Action<SkillCastContext> EffectReleased;
        public event Action<SkillCastContext> EffectApplied;
        internal void NotifyEffectApplied(SkillCastContext context)=>AutoBattler.Battle.Combat.CombatEventDispatcher.Publish(EffectApplied,context);
        public event Action CastFinished;
        private void Awake()=>unit=GetComponent<Unit>();
        private void OnEnable()=>BindUnit(GetComponent<Unit>());
        public void BindUnit(Unit owner)
        {
            UnbindUnit(); unit=owner;
            // RequireComponent로 추가되는 도중에는 Unit.Awake에서 참조 초기화 후 다시 연결.
            if(!isActiveAndEnabled || unit==null || unit.Health==null || unit.Effects==null) return;
            boundHealth=unit.Health; boundEffects=unit.Effects;
            boundHealth.Died+=OnDied; boundEffects.Changed+=OnEffectsChanged;
            BindFlow(unit.BattleFlow);
        }
        private void UnbindUnit()
        {
            if(boundHealth!=null) boundHealth.Died-=OnDied;
            if(boundEffects!=null) boundEffects.Changed-=OnEffectsChanged;
            boundHealth=null; boundEffects=null;
        }
        public void Initialize(DataRepository repository,int skillId)
        { ResetForRound(); data=repository; Definition=repository.Skills.TryGetValue(skillId,out SkillDefinition skill)?skill:null; BindUnit(unit); }
        private void BindFlow(BattleFlowController flow)
        {
            if(boundFlow!=null) boundFlow.PhaseChanged-=OnPhaseChanged;
            boundFlow=flow;
            if(isActiveAndEnabled && boundFlow!=null) boundFlow.PhaseChanged+=OnPhaseChanged;
        }
        public bool TryPrepare(IReadOnlyList<Unit> participants,Unit preferred,out SkillCastContext context)
        {
            context=null;
            if(!CanCast) return false;
            Unit target=SkillTargetSelector.Select(unit,Definition,participants,preferred);
            if(target==null) return false;
            context=new SkillCastContext(unit,Definition,new SkillTarget(target),participants); return true;
        }
        public async UniTask CastAsync(SkillCastContext context,CancellationToken token)
        {
            if(!CanCast || context==null || context.Caster!=unit || context.Definition!=Definition || !context.Target.IsValid
                || !SkillTargetSelector.IsEligible(unit,Definition,context.Target.Unit)
                || token.IsCancellationRequested || !unit.Mana.TrySpend(Definition.ManaCost,out int spent)) return;
            CastOperation op=new CastOperation {Spent=spent,Cancellation=CancellationTokenSource.CreateLinkedTokenSource(token,this.GetCancellationTokenOnDestroy())};
            current=op;
            try
            {
                CastStarted?.Invoke(context);
                await UniTask.Delay(TimeSpan.FromSeconds(Definition.Windup),cancellationToken:op.Cancellation.Token);
                op.Cancellation.Token.ThrowIfCancellationRequested();
                if(!unit.IsInCombat || !unit.Health.IsAlive) return;
                if(!context.Target.IsValid || !SkillTargetSelector.IsEligible(unit,Definition,context.Target.Unit))
                {
                    Unit replacement=SkillTargetSelector.Select(unit,Definition,context.Participants);
                    if(replacement==null) { op.Reason=SkillCancelReason.NoTarget; return; }
                    context.Target=new SkillTarget(replacement);
                }
                op.Released=true; nextCastTime=Time.time+Definition.Cooldown;
                context.Center=Definition.CenterOnCaster?unit.CurrentCell.Value:context.Target.Unit.CurrentCell.Value;
                AutoBattler.Battle.Combat.CombatEventDispatcher.Publish(EffectReleased,context);
                await SkillExecutor.ExecuteAsync(context,data,op.Cancellation.Token);
            }
            catch(OperationCanceledException) { }
            finally
            {
                if(ReferenceEquals(current,op))
                {
                    if(!op.Released && unit.IsInCombat && unit.Health.IsAlive
                        && (op.Reason==SkillCancelReason.Stunned || op.Reason==SkillCancelReason.NoTarget)) unit.Mana.Add(op.Spent);
                    current=null; CastFinished?.Invoke();
                }
                op.Cancellation.Dispose();
            }
        }
        private void OnDied() { if(current?.Released!=true) Cancel(SkillCancelReason.Dead); }
        private void OnEffectsChanged() { if(unit.Effects.IsStunned && current?.Released!=true) Cancel(SkillCancelReason.Stunned); }
        private void OnPhaseChanged(BattlePhase phase) { if(phase!=BattlePhase.Combat) Cancel(SkillCancelReason.RoundEnded); }
        private void Cancel(SkillCancelReason reason)
        {
            if(current==null) return;
            current.Reason=reason; current.Cancellation.Cancel();
        }
        public void ResetForRound()
        { Cancel(SkillCancelReason.RoundEnded); current=null; nextCastTime=0f; }
        private void OnDisable()
        {
            UnbindUnit();
            if(boundFlow!=null) boundFlow.PhaseChanged-=OnPhaseChanged;
            Cancel(SkillCancelReason.Returned); current=null; nextCastTime=0f;
        }
    }
}
