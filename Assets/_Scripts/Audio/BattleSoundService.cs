using System;
using System.Collections.Generic;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Skills;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Skills;
using AutoBattler.Managers;

namespace AutoBattler.Audio
{
    /// <summary>전투 동안만 이벤트 구독. 풀 반환 전에 구독 해제.</summary>
    public sealed class BattleSoundService
    {
        private readonly List<Binding> bindings = new List<Binding>();
        public void Begin(IEnumerable<Unit> units)
        { End(); foreach (var unit in units) bindings.Add(new Binding(unit)); }
        public void End() { foreach (var binding in bindings) binding.Dispose(); bindings.Clear(); }

        private sealed class Binding : IDisposable
        {
            private readonly Unit unit;
            private readonly CombatUnitReference reference;
            private int shield;
            public Binding(Unit value)
            {
                unit = value; reference = new CombatUnitReference(value); shield = unit.Effects.Shield;
                unit.Health.DamageResolved += OnDamage;
                unit.Health.Died += OnDied;
                unit.Health.Healed += OnHealed;
                unit.Effects.Changed += OnEffects;
                unit.Skill.EffectReleased += OnSkill;
            }
            private bool Current => reference.IsCurrent && unit.IsInCombat;
            private void OnDamage(DamageResult result)
            {
                if (!Current || result.HealthDamage + result.ShieldDamage <= 0) return;
                SoundManager.PlayEffect(result.HealthDamage == 0 ? SoundId.Shield
                    : result.Context.Attacker.ClassId == 2 || result.Context.Kind == DamageKind.Skill
                        ? SoundId.MagicHit : SoundId.Hit);
            }
            private void OnDied() { if (Current) SoundManager.PlayEffect(SoundId.Death); }
            private void OnHealed(int amount) { if (Current && amount > 0) SoundManager.PlayEffect(SoundId.Heal); }
            private void OnEffects()
            {
                int next = unit.Effects.Shield;
                if (Current && next > shield) SoundManager.PlayEffect(SoundId.Shield);
                shield = next;
            }
            private void OnSkill(SkillCastContext context)
            {
                if (!Current) return;
                // 회복·보호막은 실제 적용 이벤트에서 한 번 재생.
                if (context.Definition.TargetRule != SkillTargetRule.CurrentEnemy) return;
                SoundManager.PlayEffect(context.Definition.Animation == SkillAnimation.Magic ? SoundId.Fire
                    : context.Definition.Animation == SkillAnimation.Shoot ? SoundId.Shoot
                    : context.Definition.Radius > 0 ? SoundId.HeavyImpact : SoundId.Slash);
            }
            public void Dispose()
            {
                if (unit == null) return;
                unit.Health.DamageResolved -= OnDamage; unit.Health.Died -= OnDied;
                unit.Health.Healed -= OnHealed; unit.Effects.Changed -= OnEffects;
                unit.Skill.EffectReleased -= OnSkill;
            }
        }
    }
}
