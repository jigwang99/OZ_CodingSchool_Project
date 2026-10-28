using System;

namespace AutoBattler.Battle.Units
{
    public enum StatType { MaxHp, AttackDamage, Defense, AttackSpeed, MoveSpeed, SkillPower, CritChance, CritMultiplier, BasicAttackPower }
    public enum ModifierOperation { Flat, Percent }
    public enum ModifierSourceKind { System, Skill, OriginTrait, ClassTrait, Augment, Equipment }

    /// <summary>효과 출처. 정의 ID와 시전자 인스턴스 ID를 구분.</summary>
    public readonly struct ModifierSource : IEquatable<ModifierSource>
    {
        public ModifierSourceKind Kind { get; }
        public int DefinitionId { get; }
        public int CasterInstanceId { get; }
        public ModifierSource(ModifierSourceKind kind,int definitionId,int casterInstanceId=0)
        { Kind=kind; DefinitionId=definitionId; CasterInstanceId=casterInstanceId; }
        public bool Equals(ModifierSource other)=>Kind==other.Kind && DefinitionId==other.DefinitionId && CasterInstanceId==other.CasterInstanceId;
        public override bool Equals(object other)=>other is ModifierSource source && Equals(source);
        public override int GetHashCode()=>((int)Kind*397^DefinitionId)*397^CasterInstanceId;
    }
    public readonly struct ModifierHandle : IEquatable<ModifierHandle>
    {
        public long Value { get; }
        public bool IsValid=>Value!=0;
        internal ModifierHandle(long value)=>Value=value;
        public bool Equals(ModifierHandle other)=>Value==other.Value;
        public override bool Equals(object other)=>other is ModifierHandle handle && Equals(handle);
        public override int GetHashCode()=>Value.GetHashCode();
    }
    public readonly struct StatModifierSpec
    {
        public StatType Stat { get; }
        public ModifierOperation Operation { get; }
        public float Value { get; }
        public StatModifierSpec(StatType stat,ModifierOperation operation,float value)
        { Stat=stat; Operation=operation; Value=value; }
    }
    public readonly struct StatModifier
    {
        public ModifierHandle Handle { get; }
        public ModifierSource Source { get; }
        public StatModifierSpec Spec { get; }
        internal StatModifier(ModifierHandle handle,ModifierSource source,StatModifierSpec spec)
        { Handle=handle; Source=source; Spec=spec; }
    }
}
