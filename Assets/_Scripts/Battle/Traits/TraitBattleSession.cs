using System;
using System.Collections.Generic;
using System.Linq;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Traits;
using AutoBattler.Data.Units;
using UnityEngine;

namespace AutoBattler.Battle.Traits
{
    /// <summary>전투 시작에 확정한 시너지를 적용하고 전투 종료에 해당 출처만 제거.</summary>
    public sealed class TraitBattleSession : IDisposable
    {
        private readonly Binding[] bindings;
        private bool started,disposed;
        public TraitBattleSession(IEnumerable<Unit> roster,TraitSnapshot allies,TraitSnapshot enemies)
        {
            bindings=roster.Where(unit=>unit!=null && unit.isActiveAndEnabled && unit.IsOnBoard && unit.Health.IsAlive)
                .OrderBy(unit=>unit.InstanceId)
                .Select(unit=>new Binding(unit,(unit.Team==UnitTeam.Ally?allies:enemies).Active.Where(definition=>definition.Matches(unit.Definition)).ToArray()))
                .ToArray();
        }
        public void Start()
        {
            if(started || disposed) return;
            started=true;
            // 상시 능력치·시작 마나를 먼저 확정한 뒤 시작 보호막 등 적용.
            foreach(Binding binding in bindings) binding.ApplyPersistent();
            foreach(Binding binding in bindings) binding.ApplyStart();
        }
        public void Dispose()
        {
            if(disposed) return;
            disposed=true;
            foreach(Binding binding in bindings) binding.Dispose();
        }

        private sealed class Binding : IDisposable
        {
            private readonly CombatUnitReference reference;
            private readonly Unit unit;
            private readonly TraitDefinition[] traits;
            private readonly HashSet<int> conditional=new HashSet<int>();
            private bool bound;
            private bool CanApply=>reference.IsAlive && unit.IsInCombat;
            public Binding(Unit unit,TraitDefinition[] traits)
            { this.unit=unit; reference=new CombatUnitReference(unit); this.traits=traits; }
            public void ApplyPersistent()
            {
                if(!CanApply) return;
                unit.Health.HealthChanged+=OnHealthChanged; unit.Combat.Killed+=OnKilled; bound=true;
                foreach(TraitDefinition trait in traits)
                {
                    switch(trait.Effect)
                    {
                        case TraitEffect.StartingMana: unit.Mana.SetStartingBonus(trait.Source,Mathf.FloorToInt(trait.Value)); break;
                        case TraitEffect.BasicAttackPower: Set(trait,StatType.BasicAttackPower); break;
                        case TraitEffect.SkillPower: Set(trait,StatType.SkillPower); break;
                        case TraitEffect.AttackSpeed: Set(trait,StatType.AttackSpeed); break;
                        case TraitEffect.Critical:
                            unit.StatModifiers.ReplaceSource(trait.Source,
                                new StatModifierSpec(StatType.CritChance,ModifierOperation.Flat,trait.Value),
                                new StatModifierSpec(StatType.CritMultiplier,ModifierOperation.Flat,trait.SecondaryValue));
                            break;
                    }
                }
                OnHealthChanged(unit.Health.CurrentHp,unit.Health.MaxHp);
            }
            private void Set(TraitDefinition trait,StatType stat)=>unit.StatModifiers.ReplaceSource(trait.Source,
                new StatModifierSpec(stat,ModifierOperation.Percent,trait.Value));
            public void ApplyStart()
            {
                if(!CanApply) return;
                unit.Mana.ResetForRound();
                foreach(TraitDefinition trait in traits)
                    if(trait.Effect==TraitEffect.ShieldAtStart)
                        unit.Effects.Apply(new EffectDefinition(trait.Id,EffectKind.Shield,Mathf.FloorToInt(unit.Health.MaxHp*trait.Value),trait.Duration),trait.Source);
            }
            private void OnHealthChanged(int hp,int maxHp)
            {
                if(!bound || !reference.IsCurrent) return;
                foreach(TraitDefinition trait in traits)
                {
                    if(trait.Effect!=TraitEffect.LowHealthAttack) continue;
                    bool active=CanApply && hp<=(double)maxHp*trait.Threshold;
                    if(active==conditional.Contains(trait.Id)) continue;
                    if(active) { conditional.Add(trait.Id); Set(trait,StatType.AttackDamage); }
                    else { conditional.Remove(trait.Id); unit.StatModifiers.RemoveSource(trait.Source); }
                }
            }
            private void OnKilled(KillEvent info)
            {
                if(!bound || !CanApply || !info.IsCredited || info.Killer.Unit!=unit) return;
                foreach(TraitDefinition trait in traits)
                    if(trait.Effect==TraitEffect.HealOnKill)
                        unit.Health.Heal(Mathf.FloorToInt(unit.Health.MaxHp*trait.Value));
            }
            public void Dispose()
            {
                if(bound && unit!=null)
                { unit.Health.HealthChanged-=OnHealthChanged; unit.Combat.Killed-=OnKilled; }
                bound=false;
                if(!reference.IsCurrent) return;
                foreach(TraitDefinition trait in traits)
                {
                    unit.Effects.RemoveSource(trait.Source);
                    unit.StatModifiers.RemoveSource(trait.Source);
                    unit.Mana.RemoveStartingBonus(trait.Source);
                }
                conditional.Clear();
            }
        }
    }
}
