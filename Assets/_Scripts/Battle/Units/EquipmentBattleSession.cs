using System;
using System.Collections.Generic;
using System.Linq;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Skills;
using AutoBattler.Data.Items;
using AutoBattler.Data.Units;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    /// <summary>장착 효과의 전투 시작 처리, 발동 횟수와 중첩 관리.</summary>
    internal sealed class EquipmentBattleSession : IDisposable
    {
        private readonly CombatUnitReference[] roster;
        private readonly Binding[] bindings;
        private readonly HashSet<int> usedItems = new();

        public EquipmentBattleSession(IEnumerable<Unit> participants)
        {
            roster = participants.Select(unit => new CombatUnitReference(unit)).ToArray();
            bindings = roster.Where(reference => !reference.IsSummon)
                .Select(reference => new Binding(reference, roster, usedItems)).ToArray();
        }

        public void Start()
        {
            foreach (Binding binding in bindings) binding.Start();
            foreach (Binding binding in bindings) binding.ApplyStartingShields();
        }

        public void Tick()
        {
            foreach (Binding binding in bindings) binding.Tick(Time.time);
        }

        public void Dispose()
        {
            foreach (Binding binding in bindings) binding.Dispose();
        }

        private sealed class ActiveItem
        {
            public readonly int Id;
            public readonly ItemCombatEffect Effect;
            public readonly ModifierSource Source;
            public int Stacks;
            public float NextTick;

            public ActiveItem(int id, ItemDefinition definition)
            {
                Id = id;
                Effect = definition.CombatEffect;
                Source = new ModifierSource(ModifierSourceKind.EquipmentEffect, definition.Id, id);
                NextTick = Time.time + Effect.Seconds;
            }
        }

        private sealed class Binding : IDisposable
        {
            private readonly Unit unit;
            private readonly CombatUnitReference owner;
            private readonly CombatUnitReference[] roster;
            private readonly HashSet<int> usedItems;
            private readonly List<ActiveItem> items = new();
            private bool bound;
            private bool CanApply => bound && owner.IsAlive && unit.IsInCombat;

            public Binding(CombatUnitReference owner, CombatUnitReference[] roster, HashSet<int> usedItems)
            {
                this.owner = owner;
                this.roster = roster;
                this.usedItems = usedItems;
                unit = owner.Unit;
            }

            public void Start()
            {
                if (!owner.IsAlive || !unit.IsInCombat) return;
                bound = true;
                unit.Equipment.Changed += Synchronize;
                unit.Combat.DamageDealt += OnDamageDealt;
                unit.Health.DamageResolved += OnDamageReceived;
                unit.Skill.CastReleased += OnCastReleased;
                Synchronize();
                int previousMax = unit.Health.MaxHp;
                foreach (ActiveItem item in items)
                {
                    if (item.Effect.Kind != ItemEffectKind.RandomStartStat) continue;
                    if (UnityEngine.Random.value < .5f)
                        unit.StatModifiers.ReplaceSource(item.Source, new StatModifierSpec(StatType.MaxHp, ModifierOperation.Flat, item.Effect.Value));
                    else
                        unit.StatModifiers.ReplaceSource(item.Source, new StatModifierSpec(StatType.AttackSpeed, ModifierOperation.Percent, item.Effect.SecondaryValue));
                    unit.Equipment.NotifyEffectActivated(item.Id);
                }
                // 전투 시작에 늘어난 체력만 채움. 통계 집계는 장비 시작 효과 이후에 시작.
                int gainedHp = unit.Health.MaxHp - previousMax;
                if (gainedHp > 0) unit.Health.Heal(gainedHp);
            }

            public void ApplyStartingShields()
            {
                if (!CanApply) return;
                foreach (ActiveItem item in items)
                {
                    if (item.Effect.Kind != ItemEffectKind.AdjacentStartShield) continue;
                    bool applied = false;
                    foreach (CombatUnitReference candidate in roster)
                        if (IsAdjacentAlly(candidate)) applied |= ApplyShield(item, candidate.Unit, EffectStacking.Stack);
                    if (applied) unit.Equipment.NotifyEffectActivated(item.Id);
                }
            }

            private void Synchronize()
            {
                if (!CanApply) return;
                IReadOnlyList<(int InstanceId, ItemDefinition Definition)> equipped = unit.Equipment.EquippedEffects;
                for (int index = items.Count - 1; index >= 0; index--)
                {
                    ActiveItem item = items[index];
                    if (equipped.Any(entry => entry.InstanceId == item.Id)) continue;
                    items.RemoveAt(index);
                    if (item.Stacks > 0 || item.Effect.Kind == ItemEffectKind.RandomStartStat)
                        unit.StatModifiers.RemoveSource(item.Source);
                }
                foreach ((int instanceId, ItemDefinition definition) in equipped)
                    if (!items.Exists(item => item.Id == instanceId)) items.Add(new ActiveItem(instanceId, definition));
            }

            private void OnDamageDealt(DamageResult result)
            {
                if (!CanApply || result.Target.Team == unit.Team || !result.Target.HasIdentity) return;
                bool basicAttack = result.Context.Kind == DamageKind.BasicAttack;
                float omnivamp = 0f;
                foreach (ActiveItem item in items)
                {
                    if (item.Effect.Kind == ItemEffectKind.Omnivamp) omnivamp += item.Effect.Value;
                    if (!basicAttack) continue;
                    switch (item.Effect.Kind)
                    {
                        case ItemEffectKind.AttackAndArmorStacks:
                        case ItemEffectKind.AttackSpeedStacks:
                        case ItemEffectKind.AttackStacks:
                            AddStack(item);
                            break;
                        case ItemEffectKind.ArmorShred:
                            if (result.Target.IsAlive) ApplyArmorShred(item, result.Target.Unit);
                            break;
                        case ItemEffectKind.ManaOnHit:
                            unit.Mana.Add(Mathf.RoundToInt(item.Effect.Value));
                            break;
                    }
                }
                if (omnivamp > 0f && result.HealthDamage > 0)
                    unit.Health.Heal(Mathf.FloorToInt(result.HealthDamage * omnivamp));
            }

            private void OnDamageReceived(DamageResult result)
            {
                if (!CanApply || !result.Context.Attacker.HasIdentity || result.Context.Attacker.Team == unit.Team) return;
                foreach (ActiveItem item in items)
                {
                    if (item.Effect.Kind == ItemEffectKind.AttackAndArmorStacks)
                        AddStack(item);
                    else if (item.Effect.Kind == ItemEffectKind.LowHealthShield
                        && unit.Health.CurrentHp <= unit.Health.MaxHp * item.Effect.SecondaryValue
                        && usedItems.Add(item.Id) && ApplyShield(item, unit, EffectStacking.Refresh))
                        unit.Equipment.NotifyEffectActivated(item.Id);
                }
            }

            private void OnCastReleased(SkillCastContext context)
            {
                if (!CanApply || context.IsEcho) return;
                foreach (ActiveItem item in items)
                {
                    if (item.Effect.Kind == ItemEffectKind.FirstCastMana && usedItems.Add(item.Id))
                    {
                        int previousMana = unit.Mana.Current;
                        unit.Mana.Add(Mathf.RoundToInt(item.Effect.Value));
                        if (unit.Mana.Current > previousMana) unit.Equipment.NotifyEffectActivated(item.Id);
                    }
                }
            }

            private void AddStack(ActiveItem item)
            {
                if (item.Stacks >= item.Effect.MaxStacks) return;
                float total = ++item.Stacks * item.Effect.Value;
                if (item.Effect.Kind == ItemEffectKind.AttackSpeedStacks)
                    unit.StatModifiers.ReplaceSource(item.Source, new StatModifierSpec(StatType.AttackSpeed, ModifierOperation.Percent, total));
                else if (item.Effect.Kind == ItemEffectKind.AttackAndArmorStacks)
                    unit.StatModifiers.ReplaceSource(item.Source, new StatModifierSpec(StatType.AttackDamage, ModifierOperation.Flat, total),
                        new StatModifierSpec(StatType.Defense, ModifierOperation.Flat, total));
                else
                    unit.StatModifiers.ReplaceSource(item.Source, new StatModifierSpec(StatType.AttackDamage, ModifierOperation.Flat, total));
                unit.Equipment.SetEffectStacks(item.Id, item.Stacks);
            }

            private bool ApplyShield(ActiveItem item, Unit target, EffectStacking stacking)
            {
                int amount = Mathf.FloorToInt(unit.Health.MaxHp * item.Effect.Value);
                if (amount <= 0) return false;
                EffectDefinition effect = new(item.Source.DefinitionId, EffectKind.Shield, amount, item.Effect.Seconds, stacking);
                return target.Effects.Apply(effect, item.Source).IsValid;
            }

            private static void ApplyArmorShred(ActiveItem item, Unit target)
            {
                // 여러 장착자의 분쇄 효과는 같은 대상에서 중첩 없이 지속시간 갱신.
                int id = item.Source.DefinitionId;
                EffectDefinition effect = new(id, EffectKind.StatModifier, -item.Effect.Value, item.Effect.Seconds,
                    stat: StatType.Defense, operation: ModifierOperation.Scale);
                target.Effects.Apply(effect, new ModifierSource(ModifierSourceKind.EquipmentEffect, id));
            }

            public void Tick(float now)
            {
                if (!CanApply) return;
                foreach (ActiveItem item in items)
                {
                    if (item.Effect.Kind != ItemEffectKind.AdjacentPeriodicHeal || now < item.NextTick) continue;
                    item.NextTick = now + item.Effect.Seconds;
                    Unit target = ChooseHealTarget();
                    if (target != null)
                        target.Health.Heal(Mathf.FloorToInt(target.Health.MaxHp * item.Effect.Value), unit);
                }
            }

            private Unit ChooseHealTarget()
            {
                Unit chosen = unit.Health.CurrentHp < unit.Health.MaxHp ? unit : null;
                int count = chosen != null ? 1 : 0;
                foreach (CombatUnitReference candidate in roster)
                {
                    if (!IsAdjacentAlly(candidate)) continue;
                    Unit ally = candidate.Unit;
                    if (ally.Health.CurrentHp >= ally.Health.MaxHp) continue;
                    if (UnityEngine.Random.Range(0, ++count) == 0) chosen = ally;
                }
                return chosen;
            }

            private bool IsAdjacentAlly(CombatUnitReference candidate)
            {
                if (!candidate.IsAlive || candidate.Team != unit.Team || candidate.Unit == unit) return false;
                HexCoord? from = unit.CurrentCell;
                HexCoord? to = candidate.Unit.CurrentCell;
                return from.HasValue && to.HasValue && from.Value.DistanceTo(to.Value) == 1;
            }

            public void Dispose()
            {
                if (!bound) return;
                bound = false;
                unit.Equipment.Changed -= Synchronize;
                unit.Combat.DamageDealt -= OnDamageDealt;
                unit.Health.DamageResolved -= OnDamageReceived;
                unit.Skill.CastReleased -= OnCastReleased;
                if (owner.IsCurrent)
                    foreach (ActiveItem item in items)
                    {
                        if (item.Stacks > 0 || item.Effect.Kind == ItemEffectKind.RandomStartStat)
                            unit.StatModifiers.RemoveSource(item.Source);
                        if (item.Stacks > 0) unit.Equipment.SetEffectStacks(item.Id, 0);
                    }
                items.Clear();
            }
        }
    }
}
