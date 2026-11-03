using System;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Units;

namespace AutoBattler.Data.Skills
{
    public enum SkillTargetRule { Self, CurrentEnemy, LowestHealthAlly, NearestOtherAllies }
    public enum SkillDelivery { Instant, Projectile }
    public enum SkillAnimation { Slash, Shoot, Magic, Prick }
    public enum SkillShape { Default, Line, FrontArc }
    public enum SecondarySkillTarget { SameTargets, EnemiesAroundCaster }
    [Serializable] public sealed class SkillRuleRow
    {
        public int skillId,targetCount,hitCount,secondaryEffectId,secondaryRadius;
        public SkillShape shape;
        public SecondarySkillTarget secondaryTarget;
        public bool retargetOnKill;
        public float hitInterval,executeThreshold,executeMultiplier,distanceBonus,lifeStealRatio;
        public float effectLowHpThreshold,effectLowHpMultiplier,shieldMaxHpRatio;
    }
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
        public SkillShape Shape { get; }
        public int TargetCount { get; } public int HitCount { get; }
        public float HitInterval { get; } public bool RetargetOnKill { get; }
        public float ExecuteThreshold { get; } public float ExecuteMultiplier { get; }
        public float DistanceBonus { get; } public float LifeStealRatio { get; }
        public int SecondaryEffectId { get; } public SecondarySkillTarget SecondaryTarget { get; }
        public int SecondaryRadius { get; }
        public float EffectLowHpThreshold { get; } public float EffectLowHpMultiplier { get; }
        public float ShieldMaxHpRatio { get; }
        public SkillDefinition(SkillRow row,SkillRuleRow rule=null)
        {
            Id=row.skillId; Name=row.name; ManaCost=row.manaCost; CastRange=row.castRange; Radius=row.radius;
            EffectId=row.effectId; TargetRule=row.targetRule; Delivery=row.delivery; Animation=row.animation;
            CenterOnCaster=row.centerOnCaster; Windup=row.windup; Cooldown=row.cooldown;
            BaseDamage=row.baseDamage; AttackRatio=row.attackRatio; ArmorIgnore=row.armorIgnore;
            ProjectileSpeed=row.projectileSpeed;
            Shape=rule?.shape ?? SkillShape.Default; TargetCount=rule?.targetCount ?? 0;
            HitCount=Math.Max(1,rule?.hitCount ?? 1); HitInterval=rule?.hitInterval ?? 0f; RetargetOnKill=rule?.retargetOnKill ?? false;
            ExecuteThreshold=rule?.executeThreshold ?? 0f; ExecuteMultiplier=rule?.executeMultiplier ?? 1f;
            DistanceBonus=rule?.distanceBonus ?? 0f; LifeStealRatio=rule?.lifeStealRatio ?? 0f;
            SecondaryEffectId=rule?.secondaryEffectId ?? 0; SecondaryTarget=rule?.secondaryTarget ?? SecondarySkillTarget.SameTargets;
            SecondaryRadius=rule?.secondaryRadius ?? 0; EffectLowHpThreshold=rule?.effectLowHpThreshold ?? 0f;
            EffectLowHpMultiplier=rule?.effectLowHpMultiplier ?? 1f; ShieldMaxHpRatio=rule?.shieldMaxHpRatio ?? 0f;
        }
    }
}
