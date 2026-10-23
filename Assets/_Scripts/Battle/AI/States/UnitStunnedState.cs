namespace AutoBattler.Battle.AI.States
{
    public sealed class UnitStunnedState : UnitBaseState
    {
        public override UnitBehaviourAction Action=>UnitBehaviourAction.Stunned;
        public UnitStunnedState(UnitStateController controller):base(controller) { }
        public override void Update()
        {
            if(!Unit.Effects.IsStunned) controller.Wait(UnitBehaviourAction.None);
        }
    }
}
