using System;
using System.Threading;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Skills;
using AutoBattler.Data.Snapshot;
using AutoBattler.Data.Units;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Battle.Skills
{
    /// <summary>발동 시 수치를 고정하고 피해·복합 효과·연속 공격을 순서대로 실행.</summary>
    public static class SkillExecutor
    {
        public static async UniTask ExecuteAsync(SkillCastContext context,DataRepository data,CancellationToken token)
        {
            SkillDefinition skill=context.Definition; Unit caster=context.Caster;
            CombatUnitReference owner=new CombatUnitReference(caster);
            HexCoord origin=caster.CurrentCell.Value;
            double starScale=UnitStarRules.StatMultiplier(caster.Star,data.Rule(EconomyRule.StarMultiplier));
            float damage=(float)Math.Max(0,Math.Min(float.MaxValue,skill.BaseDamage*starScale+caster.Stats.AttackDamage*skill.AttackRatio));
            float power=caster.StatModifiers.SkillPower*context.PowerMultiplier,maxHp=caster.Stats.MaxHp;
            float effectMultiplier=skill.EffectLowHpThreshold>0 && (double)caster.Health.CurrentHp/caster.Health.MaxHp<=skill.EffectLowHpThreshold
                ?skill.EffectLowHpMultiplier:1f;
            data.Effects.TryGetValue(skill.EffectId,out EffectDefinition effect);
            data.Effects.TryGetValue(skill.SecondaryEffectId,out EffectDefinition secondary);
            ModifierSource source=context.EffectSourceOverride ?? new ModifierSource(ModifierSourceKind.Skill,skill.Id,caster.InstanceId);
            DamageContext damageContext=DamageContext.Skill(caster,skill.Id);
            UnitTeam team=caster.Team;
            for(int hit=0;hit<skill.HitCount;hit++)
            {
                token.ThrowIfCancellationRequested();
                if(hit>0)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(skill.HitInterval),cancellationToken:token);
                    if(!owner.IsAlive || caster.Effects.IsStunned) return;
                    if(!context.Target.IsValid)
                    {
                        if(!skill.RetargetOnKill) return;
                        Unit replacement=SkillTargetSelector.Select(caster,skill,context.CurrentParticipants);
                        if(replacement==null) return;
                        context.Target=new SkillTarget(replacement);
                    }
                    context.Center=context.Target.Unit.CurrentCell.Value;
                    caster.Skill.NotifyRepeatedRelease(context);
                }
                HexCoord center=context.Center;
                if(skill.Delivery==SkillDelivery.Projectile)
                {
                    float traveled=0f;
                    do
                    {
                        token.ThrowIfCancellationRequested();
                        if(context.Target.IsValid) center=context.Target.Unit.CurrentCell.Value;
                        traveled+=Time.deltaTime*skill.ProjectileSpeed;
                        await UniTask.Yield(PlayerLoopTiming.Update,token);
                    } while(traveled<origin.DistanceTo(center));
                }
                token.ThrowIfCancellationRequested(); context.Center=center;
                if(!owner.IsCurrent) return;
                SkillTarget[] targets=SkillAreaSelector.Select(context,origin,team);
                context.AppliedTargets=targets;
                caster.Skill.NotifyEffectApplied(context);
                token.ThrowIfCancellationRequested();
                long stolenHealth=0;
                foreach(SkillTarget target in targets)
                {
                    if(!target.IsValid) continue;
                    float multiplier=power;
                    if(skill.ExecuteThreshold>0 && (double)target.Unit.Health.CurrentHp/target.Unit.Health.MaxHp<=skill.ExecuteThreshold)
                        multiplier*=skill.ExecuteMultiplier;
                    if(skill.DistanceBonus>0) multiplier*=1f+skill.DistanceBonus*Math.Max(0,origin.DistanceTo(target.Unit.CurrentCell.Value)-1);
                    if(damage>0) stolenHealth+=target.Unit.Health.TakeDamage(damageContext,damage,multiplier,skill.ArmorIgnore);
                    if(effect!=null && target.IsValid) Apply(target.Unit,effect,source,power,(float)starScale,maxHp,skill.ShieldMaxHpRatio,effectMultiplier);
                    if(secondary!=null && skill.SecondaryTarget==SecondarySkillTarget.SameTargets && target.IsValid)
                        Apply(target.Unit,secondary,source,power,(float)starScale,maxHp,0f,1f);
                    token.ThrowIfCancellationRequested();
                }
                if(secondary!=null && skill.SecondaryTarget==SecondarySkillTarget.EnemiesAroundCaster)
                    foreach(SkillTarget target in SkillAreaSelector.EnemiesAround(context,origin,team,skill.SecondaryRadius))
                        if(target.IsValid) Apply(target.Unit,secondary,source,power,(float)starScale,maxHp,0f,1f);
                // 보호막 피해와 초과 피해는 실제 체력 감소량에 포함되지 않음.
                if(skill.LifeStealRatio>0 && stolenHealth>0 && owner.IsAlive)
                    caster.Health.Heal((int)Math.Min(int.MaxValue,Math.Floor(stolenHealth*(double)skill.LifeStealRatio)));
            }
        }
        private static void Apply(Unit target,EffectDefinition effect,ModifierSource source,float power,float starScale,
            float maxHp,float shieldRatio,float multiplier)
        {
            float potency=multiplier;
            if(effect.Kind==EffectKind.Shield && shieldRatio>0)
                potency*=maxHp*shieldRatio*power/Mathf.Max(.0001f,effect.Value);
            else if(effect.Kind==EffectKind.Heal || effect.Kind==EffectKind.Shield) potency*=power*starScale;
            target.Effects.Apply(effect,source,potency);
        }
    }
}
