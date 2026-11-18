namespace AutoBattler.Battle.AI.States
{
    public sealed class UnitDeadState : UnitBaseState
    {
        public override UnitBehaviourAction Action=>UnitBehaviourAction.Dead;
        public UnitDeadState(UnitStateController controller):base(controller) { }
        public override void Enter()
        {
            base.Enter();
            controller.ReleaseUnit();
        }
    }
}
