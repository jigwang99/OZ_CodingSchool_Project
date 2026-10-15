using System;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Snapshot;

namespace AutoBattler.Data.Traits
{
    public enum TraitCategory { Origin, Class }
    public enum TraitEffect { ShieldAtStart, StartingMana, HealOnKill, LowHealthAttack, BasicAttackPower, SkillPower, AttackSpeed, Critical }

    [Serializable]
    public sealed class TraitRow
    {
        public int traitId,groupId,requiredCount;
        public string name;
        public TraitCategory category;
        public TraitEffect effect;
        public float value,secondaryValue,duration,threshold;
    }

    /// <summary>시트에서 읽은 시너지 단계와 효과 수치.</summary>
    public sealed class TraitDefinition
    {
        public int Id { get; }
        public string Name { get; }
        public TraitCategory Category { get; }
        public int GroupId { get; }
        public int RequiredCount { get; }
        public TraitEffect Effect { get; }
        public float Value { get; }
        public float SecondaryValue { get; }
        public float Duration { get; }
        public float Threshold { get; }
        public ModifierSource Source => new ModifierSource(Category==TraitCategory.Origin?ModifierSourceKind.OriginTrait:ModifierSourceKind.ClassTrait,Id);
        public TraitDefinition(TraitRow row)
        {
            Id=row.traitId; Name=row.name; Category=row.category; GroupId=row.groupId; RequiredCount=row.requiredCount;
            Effect=row.effect; Value=row.value; SecondaryValue=row.secondaryValue; Duration=row.duration; Threshold=row.threshold;
        }
        public bool Matches(UnitDefinition unit)=>unit!=null && (Category==TraitCategory.Origin?unit.OriginId:unit.ClassId)==GroupId;
    }
}
