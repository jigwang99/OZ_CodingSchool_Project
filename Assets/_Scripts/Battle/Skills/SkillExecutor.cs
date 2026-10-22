using System;
using System.Linq;
using System.Threading;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Skills;
using AutoBattler.Data.Snapshot;
using AutoBattler.Data.Units;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Battle.Skills
{
    /// <summary>스킬 피해·효과 적용. 치명타 없이 방어 계산은 UnitHealth에서 한 번 수행.</summary>
    public static class SkillExecutor
    {
        public static async UniTask ExecuteAsync(SkillCastContext context,DataRepository data,CancellationToken token)
        {
            var skill=context.Definition; var caster=context.Caster;
            var center=context.Center;
            // 발동 시 수치 고정. 투사체 비행 중 시전자 상태 변경과 풀 재사용의 영향 방지.
            double fixedScale=caster.Star==2?data.Rule(EconomyRule.StarMultiplier):1d;
            double raw=skill.BaseDamage*(float)fixedScale+caster.Stats.AttackDamage*skill.AttackRatio;
            float damage=(float)Math.Max(0,Math.Min(float.MaxValue,raw));
            float power=caster.StatModifiers.SkillPower;
            float potency=1f;
            data.Effects.TryGetValue(skill.EffectId,out var effect);
            if(effect!=null && (effect.Kind==EffectKind.Heal || effect.Kind==EffectKind.Shield)) potency=power*(float)fixedScale;
            var source=new ModifierSource(ModifierSourceKind.Skill,skill.Id,caster.InstanceId);
            var damageContext=DamageContext.Skill(caster,skill.Id);
            var team=caster.Team;
            if(skill.Delivery==SkillDelivery.Projectile)
            {
                // 생존 대상 위치 추적. 사망·반환 후에는 마지막 위치를 유지하며 비행 시간 진행.
                float traveled=0f;
                var from=caster.CurrentCell.Value;
                do
                {
                    token.ThrowIfCancellationRequested();
                    if(context.Target.IsValid) center=context.Target.Unit.CurrentCell.Value;
                    traveled+=Time.deltaTime*skill.ProjectileSpeed;
                    await UniTask.Yield(PlayerLoopTiming.Update,token);
                } while(traveled<from.DistanceTo(center));
            }
            token.ThrowIfCancellationRequested(); context.Center=center;
            SkillTarget[] targets;
            if(skill.Radius>0)
                targets=context.Participants.Where(unit=>unit!=null && unit.IsInCombat && unit.Health.IsAlive && unit.Team!=team
                    && unit.CurrentCell.HasValue && unit.CurrentCell.Value.DistanceTo(center)<=skill.Radius)
                    .OrderBy(unit=>unit.InstanceId).Select(unit=>new SkillTarget(unit)).ToArray();
            else targets=new[]{context.Target};
            caster.Skill.NotifyEffectApplied(context);
            foreach(var target in targets)
            {
                if(!target.IsValid) continue;
                if(damage>0) target.Unit.Health.TakeDamage(damageContext,damage,power,skill.ArmorIgnore);
                if(effect!=null && target.IsValid) target.Unit.Effects.Apply(effect,source,potency);
            }
        }
    }
}
