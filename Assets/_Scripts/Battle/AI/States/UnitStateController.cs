using System;
using System.Threading;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Units;
using Cysharp.Threading.Tasks;

namespace AutoBattler.Battle.AI.States
{
    /// <summary>행동트리의 선택을 상태로 전환. 실행 갱신은 Unit의 Unity 콜백에서 전달.</summary>
    public sealed class UnitStateController : IDisposable
    {
        private readonly CombatUnitReference owner;
        private readonly UnitWaitState waiting;
        private readonly UnitMoveState moving;
        private readonly UnitAttackState attacking;
        private readonly UnitCastState casting;
        private readonly UnitStunnedState stunned;
        private readonly UnitDeadState dead;
        public UnitBlackboard Blackboard { get; }
        public Unit Unit=>Blackboard.Unit;
        public StateMachine StateMachine { get; }=new StateMachine();
        public bool IsDisposed { get; private set; }
        public bool IsMoving=>moving.HasOperation || Unit.Move.IsMoving;
        public bool IsBusy=>IsMoving || Unit.Combat.IsAttacking || Unit.Skill.IsCasting;

        public UnitStateController(UnitBlackboard blackboard)
        {
            Blackboard=blackboard; owner=new CombatUnitReference(Unit);
            waiting=new UnitWaitState(this); moving=new UnitMoveState(this);
            attacking=new UnitAttackState(this); casting=new UnitCastState(this);
            stunned=new UnitStunnedState(this); dead=new UnitDeadState(this);
            Unit.BindActions(this); StateMachine.ChangeState(waiting);
        }

        public void Update()
        {
            if(IsDisposed) return;
            if(!owner.IsCurrent || !Unit.isActiveAndEnabled || !Unit.IsInCombat || Blackboard.Token.IsCancellationRequested)
            { Dispose(); return; }
            if(!Unit.Health.IsAlive) Die();
            else if(Unit.Effects.IsStunned) Stun();
            StateMachine.Update();
        }
        public void FixedUpdate()
        {
            if(IsDisposed) return;
            if(!owner.IsCurrent || !Unit.isActiveAndEnabled || !Unit.IsInCombat || Blackboard.Token.IsCancellationRequested)
            { Dispose(); return; }
            StateMachine.FixedUpdate();
        }
        public void Wait(UnitBehaviourAction reason)
        {
            if(IsDisposed) return;
            waiting.SetReason(reason); StateMachine.ChangeState(waiting);
        }
        public void Move() { if(!IsDisposed) StateMachine.ChangeState(moving); }
        public void Attack() { if(!IsDisposed) StateMachine.ChangeState(attacking); }
        public void Cast() { if(!IsDisposed) StateMachine.ChangeState(casting); }
        public void Stun() { if(!IsDisposed) StateMachine.ChangeState(stunned); }
        public void Die() { if(!IsDisposed) StateMachine.ChangeState(dead); }

        internal UniTask<bool> JumpAsync(HexCoord from,HexCoord destination,CancellationToken token)
        {
            if(IsDisposed || !owner.IsAlive)
            { Blackboard.Board.CancelReservation(owner.InstanceId,destination); return UniTask.FromResult(false); }
            Blackboard.Token=token;
            return moving.JumpAsync(from,destination);
        }
        internal void ReleaseUnit()
        {
            if(Blackboard.Released) return;
            Blackboard.Released=true; Blackboard.Board.Release(owner.InstanceId);
            Blackboard.Target=null; Blackboard.Path=null; Blackboard.PreparedSkill=null;
        }
        public void Dispose()
        {
            if(IsDisposed) return;
            IsDisposed=true; StateMachine.ChangeState(null); ReleaseUnit();
            Unit.UnbindActions(this);
        }
    }
}
