using AutoBattler.Battle.Units;

namespace AutoBattler.Battle.Combat
{
    /// <summary>초과 회복을 제외한 실제 회복량과 발생 당시 출처.</summary>
    public readonly struct HealingResult
    {
        public CombatUnitReference Healer { get; }
        public CombatUnitReference Target { get; }
        public int Amount { get; }
        internal HealingResult(CombatUnitReference healer, CombatUnitReference target, int amount)
        { Healer = healer; Target = target; Amount = amount; }
    }
}
