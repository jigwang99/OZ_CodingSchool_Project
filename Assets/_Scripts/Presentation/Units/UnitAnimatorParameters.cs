using UnityEngine;

namespace AutoBattler.Presentation.Units
{
    /// <summary>PIXEM Animator 파라미터를 한 번 해시로 변환하고 재사용.</summary>
    public static class UnitAnimatorParameters
    {
        public static readonly int Idle = Animator.StringToHash("Idle");
        public static readonly int Run = Animator.StringToHash("Run");
        public static readonly int AttackSlash = Animator.StringToHash("AttackSlash");
        public static readonly int AttackShoot = Animator.StringToHash("AttackShoot");
        public static readonly int AttackMagic = Animator.StringToHash("AttackMagic");
    }
}
