using System.Linq;
using System.Threading;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Units;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Battle.AI
{
    /// <summary>사망·행동 대기를 우선하고 대상 확보 후 공격 또는 이동을 선택.</summary>
    public sealed class UnitBehaviourTree
    {
        public UnitBlackboard Blackboard { get; }
        public BehaviourNode Root { get; }
        public UnitBehaviourTree(UnitBlackboard blackboard)
        {
            Blackboard=blackboard;
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
            token.ThrowIfCancellationRequested(); Blackboard.Token=token;
            return Root.Tick();
        }
        private BehaviourStatus ReleaseDeadUnit()
        {
            if(!Blackboard.Released)
            {
                Blackboard.Board.Release(Blackboard.Unit.InstanceId); Blackboard.Released=true;
                Blackboard.Target=null; Blackboard.Path=null;
            }
            Blackboard.Action=UnitBehaviourAction.Dead;
            return BehaviourStatus.Success;
        }
        private bool IsWaiting()=>Blackboard.Unit.Effects.IsStunned || Blackboard.Unit.Move.IsMoving
            || Blackboard.Unit.Combat.IsAttacking || Blackboard.Unit.Skill.IsCasting;
        private BehaviourStatus WaitForCurrentAction()
        {
            var unit=Blackboard.Unit;
            Blackboard.Action=unit.Effects.IsStunned?UnitBehaviourAction.Stunned
                :unit.Move.IsMoving?UnitBehaviourAction.Moving
                :unit.Skill.IsCasting?UnitBehaviourAction.Casting
                :unit.Combat.IsAttacking?UnitBehaviourAction.Attacking:UnitBehaviourAction.WaitingToRetry;
            return BehaviourStatus.Running;
        }
        private BehaviourStatus CastSkill()
        {
            Blackboard.Unit.Skill.CastAsync(Blackboard.PreparedSkill,Blackboard.Token).Forget();
            Blackboard.Action=UnitBehaviourAction.Casting;
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
            Blackboard.Action=UnitBehaviourAction.WaitingForTarget; Blackboard.RetryTime=Time.time+.2f;
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
            if(Blackboard.Unit.Combat.CanAttack)
            {
                Blackboard.Unit.Combat.AttackAsync(Blackboard.Target.Combat,Blackboard.Token).Forget();
                Blackboard.Action=UnitBehaviourAction.Attacking;
            }
            else Blackboard.Action=UnitBehaviourAction.WaitingForAttack;
            return BehaviourStatus.Running;
        }
        private BehaviourStatus MoveIntoRange()
        {
            var unit=Blackboard.Unit;
            HexCoord targetCell=Blackboard.Target.CurrentCell.Value;
            if(Blackboard.Path==null || Blackboard.Path.Count==0 || Blackboard.Revision!=Blackboard.Board.Revision || Blackboard.TargetCell!=targetCell)
            {
                Blackboard.Path=HexPathfinder.Find(Blackboard.Board,unit.InstanceId,unit.CurrentCell.Value,targetCell,unit.Stats.AttackRange);
                Blackboard.Revision=Blackboard.Board.Revision; Blackboard.TargetCell=targetCell;
            }
            if(Blackboard.Path==null || Blackboard.Path.Count==0
                || !Blackboard.Board.TryReserve(unit.InstanceId,unit.CurrentCell.Value,Blackboard.Path[0]))
            {
                if(Blackboard.FailedSince<0f) Blackboard.FailedSince=Time.time;
                Blackboard.RetryTime=Time.time+.2f; Blackboard.Revision=-1;
                Blackboard.Action=UnitBehaviourAction.WaitingForPath;
                return BehaviourStatus.Running;
            }
            HexCoord from=unit.CurrentCell.Value,next=Blackboard.Path[0];
            Blackboard.Path.RemoveAt(0); Blackboard.FailedSince=-1f;
            Blackboard.Action=UnitBehaviourAction.Moving;
            MoveAsync(from,next,Blackboard.Token).Forget();
            return BehaviourStatus.Running;
        }
        private async UniTask MoveAsync(HexCoord from,HexCoord next,CancellationToken token)
        {
            bool arrived=await Blackboard.Unit.Move.MoveToCellAsync(next,token);
            if(arrived && Blackboard.Unit.Health.IsAlive) Blackboard.Board.TryArrive(Blackboard.Unit.InstanceId,from,next);
            else Blackboard.Board.CancelReservation(Blackboard.Unit.InstanceId,next);
        }
    }
}
