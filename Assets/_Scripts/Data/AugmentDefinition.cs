using System;
using AutoBattler.Battle.Units;

namespace AutoBattler.Data.Augments
{
    public enum AugmentEffect { StatModifier, StartingShield, StartingMana, ImmediateGold, VictoryGold }
    [Serializable]
    public sealed class AugmentRow
    {
        public int augmentId;
        public string name,description;
        public AugmentEffect effect;
        public StatType stat;
        public ModifierOperation operation;
        public float value,duration;
    }
    /// <summary>도전 동안 고정한 강화 정의. 실행 상태와 분리.</summary>
    public sealed class AugmentDefinition
    {
        public int Id { get; }
        public string Name { get; }
        public string Description { get; }
        public AugmentEffect Effect { get; }
        public StatType Stat { get; }
        public ModifierOperation Operation { get; }
        public float Value { get; }
        public float Duration { get; }
        public ModifierSource Source=>new ModifierSource(ModifierSourceKind.Augment,Id);
        public AugmentDefinition(AugmentRow row)
        { Id=row.augmentId; Name=row.name; Description=row.description; Effect=row.effect; Stat=row.stat; Operation=row.operation; Value=row.value; Duration=row.duration; }
    }
}
