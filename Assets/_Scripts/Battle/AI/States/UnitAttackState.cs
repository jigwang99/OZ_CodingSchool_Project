using Cysharp.Threading.Tasks;

namespace AutoBattler.Battle.AI.States
{
    public sealed class UnitAttackState : UnitBaseState
    {
        public override UnitBehaviourAction Action=>UnitBehaviourAction.Attacking;
        public UnitAttackState(UnitStateController controller):base(controller) { }
        public override void Enter()
        {
            base.Enter();
            if(!Unit.Combat.IsAttacking && Blackboard.Target!=null)
                Unit.Combat.AttackAsync(Blackboard.Target.Combat,Blackboard.Token).Forget();
        }
        public override void Update()
        {
            if(!Unit.Combat.IsAttacking) controller.Wait(UnitBehaviourAction.WaitingForAttack);
        }
        // 기절 후에도 이미 발사된 투사체는 유지. 선딜 취소는 UnitCombat에서 처리.
    }
}
