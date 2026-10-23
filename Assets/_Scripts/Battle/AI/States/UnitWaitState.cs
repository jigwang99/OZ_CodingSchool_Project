namespace AutoBattler.Battle.AI.States
{
    public sealed class UnitWaitState : UnitBaseState
    {
        public override UnitBehaviourAction Action=>reason;
        private UnitBehaviourAction reason;
        public UnitWaitState(UnitStateController controller):base(controller) { }
        internal void SetReason(UnitBehaviourAction value)
        {
            reason=value;
            if(ReferenceEquals(controller.StateMachine.CurrentState,this)) Blackboard.Action=reason;
        }
    }
}
