using AutoBattler.Battle.Skills;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Skills;
using UnityEngine;

namespace AutoBattler.Presentation.Units
{
    /// <summary>시전 이벤트를 PIXEM 애니메이션 해시와 방향 표현에 연결.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitSkillView : MonoBehaviour
    {
        private Unit unit;
        private Animator animator;
        private UnitFacingView facing;
        private void Awake() { unit=GetComponent<Unit>(); animator=GetComponent<Animator>(); facing=GetComponent<UnitFacingView>(); }
        private void OnEnable()=>unit.Skill.CastStarted+=OnCast;
        private void OnCast(SkillCastContext context)
        {
            if(facing!=null && context.Target.Unit!=unit) facing.FacePosition(context.Target.Unit.transform.position);
            if(animator==null) return;
            int hash=context.Definition.Animation==SkillAnimation.Magic?UnitAnimatorParameters.AttackMagic
                :context.Definition.Animation==SkillAnimation.Shoot?UnitAnimatorParameters.AttackShoot:UnitAnimatorParameters.AttackSlash;
            animator.SetTrigger(hash);
        }
        private void OnDisable()=>unit.Skill.CastStarted-=OnCast;
    }
}
