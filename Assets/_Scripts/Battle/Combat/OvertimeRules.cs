using System;

namespace AutoBattler.Battle.Combat
{
    internal static class OvertimeRules
    {
        public const float AttackSpeedMultiplier = 4f;
        public const float DamageMultiplier = 3f;
        public const float ControlDurationMultiplier = .34f;
        public const float ShieldMultiplier = .34f;

        public static int ScaleHealing(int amount) => (int)(Math.Max(0, amount) * 34L / 100L);
    }
}
