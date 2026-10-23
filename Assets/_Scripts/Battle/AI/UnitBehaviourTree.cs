using System;
using System.Linq;
using System.Threading;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Units;
using AutoBattler.Battle.AI.States;
using UnityEngine;

namespace AutoBattler.Battle.AI
{
    /// <summary>우선순위와 대상을 판단한 뒤 FSM 상태 선택. 실제 행동은 각 상태에서 실행.</summary>
    public sealed class UnitBehaviourTree : IDisposable
    {
        public UnitBlackboard Blackboard { get; }
        public BehaviourNode Root { get; }
        public UnitStateController States { get; }
        public UnitBehaviourTree(UnitBlackboard blackboard)
        {
            Blackboard=blackboard; States=new UnitStateController(blackboard);
            Root=new SelectorNode(
                new SequenceNode(
                    new ConditionNode(()=>!Blackboard.Unit.Health.IsAlive),
                    new ActionNode(ReleaseDeadUnit)),
                new SequenceNode(
                    new ConditionNode(IsWaiting),
                    new ActionNode(WaitForCurrentAction)),
                new SequenceNode(
                    new ConditionNode(()=>Blackboard.Unit.Skill.TryPrepare(Blackboard.Participants,Blackboard.Target,out Blackboard.PreparedSkill)),
                    new ActionNode(CastSkill)),
                new SequenceNode(
                    new ConditionNode(()=>Time.time<Blackboard.RetryTime),
                    new ActionNode(WaitForCurrentAction)),
                new SequenceNode(
                    new ActionNode(EnsureTarget),
                    new SelectorNode(
                        new SequenceNode(
                            new ConditionNode(()=>Blackboard.Unit.Combat.IsInAttackRange(Blackboard.Target.Combat)),
                            new ActionNode(Attack)),
                        new ActionNode(MoveIntoRange))));
        }
        public BehaviourStatus Tick(CancellationToken token=default)
        {
            if(token.IsCancellationRequested) { Dispose(); token.ThrowIfCancellationRequested(); }
            Blackboard.Token=token; States.Update();
            if(States.IsDisposed) return BehaviourStatus.Failure;
            return Root.Tick();
        }
        private BehaviourStatus ReleaseDeadUnit()
        {
            States.Die();
            return BehaviourStatus.Success;
        }
        private bool IsWaiting()=>Blackboard.Unit.Effects.IsStunned || States.IsBusy;
        private BehaviourStatus WaitForCurrentAction()
        {
            var unit=Blackboard.Unit;
            if(unit.Effects.IsStunned) States.Stun();
            else if(States.IsMoving) States.Move();
            else if(unit.Skill.IsCasting) States.Cast();
            else if(unit.Combat.IsAttacking) States.Attack();
            else States.Wait(UnitBehaviourAction.WaitingToRetry);
            return BehaviourStatus.Running;
        }
        private BehaviourStatus CastSkill()
        {
            States.Cast();
            return BehaviourStatus.Running;
        }
        private BehaviourStatus EnsureTarget()
        {
            if(Blackboard.Target==null || !Blackboard.Target.Health.IsAlive
                || (Blackboard.FailedSince>=0f && Time.time-Blackboard.FailedSince>=1f))
            {
                Blackboard.Target=ChooseTarget(); Blackboard.Path=null; Blackboard.Revision=-1; Blackboard.FailedSince=-1f;
            }
            if(Blackboard.Target!=null) return BehaviourStatus.Success;
            States.Wait(UnitBehaviourAction.WaitingForTarget); Blackboard.RetryTime=Time.time+.2f;
            return BehaviourStatus.Running;
        }
        private Unit ChooseTarget()
        {
            var unit=Blackboard.Unit;
            foreach(var target in Blackboard.Participants.Where(target=>target.Team!=unit.Team && target.Health.IsAlive)
                .OrderBy(target=>unit.CurrentCell.Value.DistanceTo(target.CurrentCell.Value))
                .ThenBy(target=>target.Definition.Role).ThenBy(target=>target.InstanceId))
            {
                if(unit.Combat.IsInAttackRange(target.Combat) || HexPathfinder.Find(Blackboard.Board,unit.InstanceId,
                    unit.CurrentCell.Value,target.CurrentCell.Value,unit.Stats.AttackRange)!=null) return target;
            }
            return null;
        }
        private BehaviourStatus Attack()
        {
            Blackboard.FailedSince=-1f;
            if(Blackboard.Unit.Combat.CanAttack) States.Attack();
            else States.Wait(UnitBehaviourAction.WaitingForAttack);
            return BehaviourStatus.Running;
        }
        private BehaviourStatus MoveIntoRange()
        {
            States.Move();
            return BehaviourStatus.Running;
        }
        public void Dispose()=>States.Dispose();
    }
}
