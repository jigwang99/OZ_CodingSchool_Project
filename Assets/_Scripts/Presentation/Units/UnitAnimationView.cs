using AutoBattler.Battle.Units;
using AutoBattler.Battle.Flow;
using UnityEngine;

namespace AutoBattler.Presentation.Units
{
    /// <summary>PIXEM Animator의 표현 연결. 피해 시점은 전투 모듈이 결정.</summary>
    [RequireComponent(typeof(Unit))]
    public sealed class UnitAnimationView : MonoBehaviour
    {
        private Unit unit;
        private Animator animator;
        private bool wasMoving,wasAttacking;
        private void Awake() { unit=GetComponent<Unit>(); animator=GetComponent<Animator>(); }
        private void OnEnable()
        {
            unit.Combat.AttackStarted+=OnAttack; RestoreIdle();
        }
        private void Update()
        {
            if(animator==null || !unit.Health.IsAlive || unit.BattleFlow?.CurrentPhase==BattlePhase.Result) return;
            bool moving=unit.Move.IsMoving,attacking=unit.Combat.IsAttacking || unit.Skill.IsCasting;
            if(moving && !wasMoving) animator.SetTrigger(UnitAnimatorParameters.Run);
            else if(!moving && !attacking && (wasMoving || wasAttacking)) animator.SetTrigger(UnitAnimatorParameters.Idle);
            wasMoving=moving; wasAttacking=attacking;
        }
        public void RestoreIdle()
        {
            wasMoving=false; wasAttacking=false;
            if(animator==null) return;
            animator.ResetTrigger(UnitAnimatorParameters.Run);
            animator.ResetTrigger(UnitAnimatorParameters.AttackSlash); animator.ResetTrigger(UnitAnimatorParameters.AttackPrick);
            animator.ResetTrigger(UnitAnimatorParameters.AttackShoot); animator.ResetTrigger(UnitAnimatorParameters.AttackMagic);
            animator.SetTrigger(UnitAnimatorParameters.Idle);
        }
        private void OnAttack(UnitCombat target)
        {
            if(animator==null) return;
            animator.SetTrigger(unit.Definition.IsRanged ? (unit.Definition.ClassId==2?UnitAnimatorParameters.AttackMagic:UnitAnimatorParameters.AttackShoot)
                :(unit.Definition.ClassId==5 || unit.Definition.ClassId==6)?UnitAnimatorParameters.AttackPrick:UnitAnimatorParameters.AttackSlash);
        }
        private void OnDisable() { if(unit!=null && unit.Combat!=null) unit.Combat.AttackStarted-=OnAttack; }
    }
}
