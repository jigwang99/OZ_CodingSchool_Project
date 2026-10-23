using Cysharp.Threading.Tasks;

namespace AutoBattler.Battle.AI.States
{
    public sealed class UnitCastState : UnitBaseState
    {
        public override UnitBehaviourAction Action=>UnitBehaviourAction.Casting;
        public UnitCastState(UnitStateController controller):base(controller) { }
        public override void Enter()
        {
            base.Enter();
            var context=Blackboard.PreparedSkill;
            Blackboard.PreparedSkill=null;
            if(!Unit.Skill.IsCasting && context!=null) Unit.Skill.CastAsync(context,Blackboard.Token).Forget();
        }
        public override void Update()
        {
            if(!Unit.Skill.IsCasting) controller.Wait(UnitBehaviourAction.None);
        }
        // 시전 취소·마나 반환·발동 후 효과 수명은 UnitSkill에서 처리.
    }
}
