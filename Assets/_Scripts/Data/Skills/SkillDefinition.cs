using System;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Units;

namespace AutoBattler.Data.Skills
{
    public enum SkillTargetRule { Self, CurrentEnemy, LowestHealthAlly }
    public enum SkillDelivery { Instant, Projectile }
    public enum SkillAnimation { Slash, Shoot, Magic }
    [Serializable] public sealed class SkillRow
    {
        public int skillId,manaCost,castRange,radius,effectId;
        public string name;
        public SkillTargetRule targetRule;
        public SkillDelivery delivery;
        public SkillAnimation animation;
        public bool centerOnCaster;
        public float windup,cooldown,baseDamage,attackRatio,armorIgnore,projectileSpeed;
    }
    [Serializable] public sealed class EffectRow
    {
        public int effectId;
        public EffectKind kind;
        public float value,duration;
        public EffectStacking stacking;
        public StatType stat;
        public ModifierOperation operation;
        public EffectDefinition Create()=>new EffectDefinition(effectId,kind,value,duration,stacking,stat,operation);
    }
    /// <summary>시트에서 생성하는 불변 스킬 정의. 시전 중 상태는 UnitSkill에서 보관.</summary>
    public sealed class SkillDefinition
    {
        public int Id { get; } public string Name { get; }
        public int ManaCost { get; } public int CastRange { get; } public int Radius { get; }
        public int EffectId { get; } public SkillTargetRule TargetRule { get; }
        public SkillDelivery Delivery { get; } public SkillAnimation Animation { get; }
        public bool CenterOnCaster { get; }
        public float Windup { get; } public float Cooldown { get; }
        public float BaseDamage { get; } public float AttackRatio { get; }
        public float ArmorIgnore { get; } public float ProjectileSpeed { get; }
        public SkillDefinition(SkillRow row)
        {
            Id=row.skillId; Name=row.name; ManaCost=row.manaCost; CastRange=row.castRange; Radius=row.radius;
            EffectId=row.effectId; TargetRule=row.targetRule; Delivery=row.delivery; Animation=row.animation;
            CenterOnCaster=row.centerOnCaster; Windup=row.windup; Cooldown=row.cooldown;
            BaseDamage=row.baseDamage; AttackRatio=row.attackRatio; ArmorIgnore=row.armorIgnore;
            ProjectileSpeed=row.projectileSpeed;
        }
    }
}
