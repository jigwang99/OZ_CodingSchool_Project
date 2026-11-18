using AutoBattler.Battle.Units;

namespace AutoBattler.Battle.AI.States
{
    /// <summary>현재 유닛과 전투 문맥을 공유하는 상태 기본형.</summary>
    public abstract class UnitBaseState : IState
    {
        protected readonly UnitStateController controller;
        protected Unit Unit=>controller.Unit;
        protected UnitBlackboard Blackboard=>controller.Blackboard;
        public abstract UnitBehaviourAction Action { get; }

        protected UnitBaseState(UnitStateController controller)=>this.controller=controller;
        public virtual void Enter()=>Blackboard.Action=Action;
        public virtual void Exit() { }
        public virtual void Update() { }
        public virtual void FixedUpdate() { }
    }
}
