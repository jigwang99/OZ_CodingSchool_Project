using System;
using System.Threading;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Grid;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Battle.AI.States
{
    /// <summary>한 번의 셀 이동·도약 실행과 목적지 예약 수명 관리.</summary>
    public sealed class UnitMoveState : UnitBaseState
    {
        private sealed class MoveOperation
        {
            public CombatUnitReference Owner;
            public HexCoord From,To;
            public bool Jump,Reserved=true;
            public CancellationTokenSource Cancellation;
        }
        private MoveOperation current,pendingJump;
        private UniTask<bool> jumpTask;
        public bool HasOperation=>current!=null;
        public override UnitBehaviourAction Action=>UnitBehaviourAction.Moving;
        public UnitMoveState(UnitStateController controller):base(controller) { }

        public override void Enter()
        {
            base.Enter();
            if(pendingJump!=null)
            {
                current=pendingJump; pendingJump=null;
                jumpTask=MoveAsync(current);
                return;
            }
            // 외부에서 이미 진행 중인 이동은 새 요청 없이 관찰.
            if(Unit.Move.IsMoving) return;
            Units.Unit target=Blackboard.Target;
            if(target==null || !target.CurrentCell.HasValue || !Unit.Move.CanMove)
            { WaitForPath(); return; }
            HexCoord targetCell=target.CurrentCell.Value;
            if(Blackboard.Path==null || Blackboard.Path.Count==0 || Blackboard.Revision!=Blackboard.Board.Revision || Blackboard.TargetCell!=targetCell)
            {
                Blackboard.Path=HexPathfinder.Find(Blackboard.Board,Unit.InstanceId,Unit.CurrentCell.Value,targetCell,Unit.Stats.AttackRange);
                Blackboard.Revision=Blackboard.Board.Revision; Blackboard.TargetCell=targetCell;
            }
            if(Blackboard.Path==null || Blackboard.Path.Count==0
                || !Blackboard.Board.TryReserve(Unit.InstanceId,Unit.CurrentCell.Value,Blackboard.Path[0]))
            { WaitForPath(); return; }
            HexCoord next=Blackboard.Path[0]; Blackboard.Path.RemoveAt(0); Blackboard.FailedSince=-1f;
            current=Create(Unit.CurrentCell.Value,next,false);
            MoveAsync(current).Forget();
        }
        private MoveOperation Create(HexCoord from,HexCoord to,bool jump)=>new MoveOperation
        {
            Owner=new CombatUnitReference(Unit),From=from,To=to,Jump=jump,
            Cancellation=CancellationTokenSource.CreateLinkedTokenSource(Blackboard.Token,Unit.GetCancellationTokenOnDestroy())
        };
        internal UniTask<bool> JumpAsync(HexCoord from,HexCoord destination)
        {
            pendingJump=Create(from,destination,true);
            controller.Move();
            return jumpTask;
        }
        private void WaitForPath()
        {
            if(Blackboard.FailedSince<0f) Blackboard.FailedSince=Time.time;
            Blackboard.RetryTime=Time.time+.2f; Blackboard.Revision=-1;
            controller.Wait(UnitBehaviourAction.WaitingForPath);
        }
        public override void Update()
        {
            if(current==null && !Unit.Move.IsMoving) controller.Wait(UnitBehaviourAction.None);
        }
        public override void Exit()
        {
            MoveOperation operation=current;
            if(operation==null) return;
            current=null;
            operation.Cancellation.Cancel();
            if(operation.Owner.IsCurrent) Unit.Move.Stop();
            CancelReservation(operation);
        }
        private void CancelReservation(MoveOperation operation)
        {
            if(!operation.Reserved) return;
            operation.Reserved=false;
            Blackboard.Board.CancelReservation(operation.Owner.InstanceId,operation.To);
        }
        private async UniTask<bool> MoveAsync(MoveOperation operation)
        {
            try
            {
                bool arrived=operation.Jump
                    ? await Unit.Move.JumpToCellAsync(operation.To,operation.Cancellation.Token)
                    : await Unit.Move.MoveToCellAsync(operation.To,operation.Cancellation.Token);
                if(!arrived || !operation.Owner.IsAlive || !ReferenceEquals(current,operation) || !operation.Reserved) return false;
                if(!Blackboard.Board.TryArrive(operation.Owner.InstanceId,operation.From,operation.To))
                    throw new InvalidOperationException("이동 목적지 예약 소유권 불일치.");
                operation.Reserved=false;
                return true;
            }
            finally
            {
                CancelReservation(operation);
                if(!operation.Owner.IsAlive) controller.ReleaseUnit();
                if(ReferenceEquals(current,operation))
                {
                    current=null;
                    if(operation.Jump) { Blackboard.Path=null; Blackboard.Revision=-1; }
                }
                operation.Cancellation.Dispose();
            }
        }
    }
}
