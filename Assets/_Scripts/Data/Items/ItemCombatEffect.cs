namespace AutoBattler.Data.Items
{
    public enum ItemEffectKind
    {
        None, BonusArmor, AdjacentStartShield, AttackAndArmorStacks, ArmorShred, LowHealthShield,
        BonusHealth, RandomStartStat, Omnivamp, AdjacentPeriodicHeal, AttackSpeedStacks,
        AttackStacks, SkillCritical, ManaOnHit, FirstCastMana
    }

    public readonly struct ItemCombatEffect
    {
        public ItemEffectKind Kind { get; }
        public float Value { get; }
        public float SecondaryValue { get; }
        public float Seconds { get; }
        public int MaxStacks { get; }

        private ItemCombatEffect(ItemEffectKind kind, float value, float secondaryValue = 0f, float seconds = 0f, int maxStacks = 0)
        {
            Kind = kind;
            Value = value;
            SecondaryValue = secondaryValue;
            Seconds = seconds;
            MaxStacks = maxStacks;
        }

        // 시트에서 불러오는 기본 능력치와 별개로 합성 장비의 전투 효과를 연결.
        internal static ItemCombatEffect ForItem(int itemId) => itemId switch
        {
            6101 => new(ItemEffectKind.BonusArmor, .20f),
            6102 => new(ItemEffectKind.AdjacentStartShield, .08f, seconds: 4f),
            6103 => new(ItemEffectKind.AttackAndArmorStacks, 1f, maxStacks: 10),
            6104 => new(ItemEffectKind.ArmorShred, .20f, seconds: 4f),
            6105 => new(ItemEffectKind.LowHealthShield, .15f, secondaryValue: .50f, seconds: 4f),
            6106 => new(ItemEffectKind.BonusHealth, .15f),
            6107 => new(ItemEffectKind.RandomStartStat, 150f, secondaryValue: .25f),
            6108 => new(ItemEffectKind.Omnivamp, .12f),
            6109 => new(ItemEffectKind.AdjacentPeriodicHeal, .08f, seconds: 5f),
            6110 => new(ItemEffectKind.AttackSpeedStacks, .015f, maxStacks: 10),
            6111 => new(ItemEffectKind.AttackStacks, 2f, maxStacks: 10),
            6113 => new(ItemEffectKind.SkillCritical, .15f),
            6114 => new(ItemEffectKind.ManaOnHit, 3f),
            6115 => new(ItemEffectKind.FirstCastMana, 70f),
            _ => default
        };
    }
}
