using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Skills;
using AutoBattler.Data.Augments;
using AutoBattler.Data.Snapshot;
using AutoBattler.Data.Units;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    /// <summary>한 전투의 배치 조건·장비 조건·처치·시전 증강 효과와 구독 수명 관리.</summary>
    public sealed class AugmentBattleSession : IDisposable
    {
        private readonly Binding[] bindings;
        private readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        private bool started,disposed;

        public AugmentBattleSession(IEnumerable<Unit> participants,IEnumerable<AugmentDefinition> augments,
            DataRepository data,int deploymentLimit,int activeTraitCount,Action<AugmentDefinition> activated=null)
        {
            Unit[] roster=participants.ToArray();
            Unit[] allies=roster.Where(unit=>unit!=null && unit.Team==UnitTeam.Ally && !unit.IsSummon
                && unit.IsInCombat && unit.Health.IsAlive).ToArray();
            AugmentDefinition[] selected=augments.ToArray();
            bindings=allies.Select(unit=>new Binding(unit,allies,roster,selected,data,deploymentLimit,activeTraitCount,
                lifetime.Token,activated)).ToArray();
        }

        public void Start()
        {
            if(started || disposed) return;
            started=true;
            // 최대 체력 수정 완료 후 시작 보호막을 계산하여 적용 순서에 따른 수치 차이 방지.
            foreach(Binding binding in bindings) binding.ApplyStats();
            foreach(Binding binding in bindings) binding.Start();
        }

        public void Dispose()
        {
            if(disposed) return;
            disposed=true; lifetime.Cancel();
            foreach(Binding binding in bindings) binding.Dispose();
            lifetime.Dispose();
        }

        private sealed class Binding : IDisposable
        {
            private readonly Unit unit;
            private readonly Unit[] allies;
            private readonly CombatUnitReference reference;
            private readonly CombatUnitReference[] participants;
            private readonly AugmentDefinition[] augments;
            private readonly DataRepository data;
            private readonly CancellationToken token;
            private readonly Action<AugmentDefinition> activated;
            private readonly int deploymentLimit,activeTraitCount,adjacentCount;
            private readonly HashSet<int> used=new HashSet<int>();
            private readonly HashSet<int> equipmentActive=new HashSet<int>();
            private bool bound;
            private bool CanApply=>bound && reference.IsAlive && unit.IsInCombat && !token.IsCancellationRequested;

            public Binding(Unit unit,Unit[] allies,Unit[] roster,AugmentDefinition[] augments,DataRepository data,
                int deploymentLimit,int activeTraitCount,CancellationToken token,Action<AugmentDefinition> activated)
            {
                this.unit=unit; this.allies=allies; this.augments=augments; this.data=data;
                this.deploymentLimit=deploymentLimit; this.activeTraitCount=activeTraitCount; this.token=token;
                this.activated=activated;
                reference=new CombatUnitReference(unit);
                participants=roster.Select(actor=>new CombatUnitReference(actor)).ToArray();
                adjacentCount=allies.Count(ally=>ally!=unit && ally.CurrentCell.Value.DistanceTo(unit.CurrentCell.Value)==1);
            }

            public void ApplyStats()
            {
                if(!reference.IsAlive || !unit.IsInCombat) return;
                int previousMaxHp=unit.Health.MaxHp;
                foreach(AugmentDefinition augment in augments)
                {
                    if(augment.Effect==AugmentEffect.EliteArmy && allies.Length<deploymentLimit)
                        Set(augment,new StatModifierSpec(StatType.MaxHp,ModifierOperation.Percent,augment.Value),
                            new StatModifierSpec(StatType.AttackDamage,ModifierOperation.Percent,augment.Value));
                    else if(augment.Effect==AugmentEffect.Comrades && adjacentCount==1)
                        Set(augment,new StatModifierSpec(StatType.DamageReduction,ModifierOperation.Flat,augment.Value));
                    else if(augment.Effect==AugmentEffect.ActiveTraitPower && activeTraitCount>0)
                        Set(augment,new StatModifierSpec(StatType.SkillPower,ModifierOperation.Percent,activeTraitCount*augment.Value));
                }
                UpdateEquipmentStats();
                if(unit.Health.MaxHp>previousMaxHp) unit.Health.Heal(unit.Health.MaxHp-previousMaxHp);
            }

            public void Start()
            {
                if(!reference.IsAlive || !unit.IsInCombat) return;
                bound=true;
                unit.Health.DamageTaken+=OnDamaged;
                unit.Combat.Killed+=OnKilled;
                unit.Skill.CastReleased+=OnCastReleased;
                unit.Equipment.Changed+=OnEquipmentChanged;
                foreach(Unit ally in allies) ally.Health.Died+=OnAllyDied;
                foreach(AugmentDefinition augment in augments)
                {
                    if(augment.Effect==AugmentEffect.IsolatedShield && adjacentCount==0
                        || augment.Effect==AugmentEffect.FullyEquipped && unit.Equipment.Count==Run.ItemInventoryState.EquipmentCapacity)
                        Shield(augment);
                }
                TryLastStand();
            }

            private void Set(AugmentDefinition augment,params StatModifierSpec[] specs)
            {
                unit.StatModifiers.ReplaceSource(augment.Source,specs);
                activated?.Invoke(augment);
            }

            private void UpdateEquipmentStats()
            {
                foreach(AugmentDefinition augment in augments)
                {
                    if(augment.Effect!=AugmentEffect.Unarmed) continue;
                    bool active=unit.Equipment.Count==0;
                    if(active==equipmentActive.Contains(augment.Id)) continue;
                    if(active)
                    {
                        equipmentActive.Add(augment.Id);
                        Set(augment,new StatModifierSpec(StatType.AttackDamage,ModifierOperation.Percent,augment.Value),
                            new StatModifierSpec(StatType.AttackSpeed,ModifierOperation.Percent,augment.Value));
                    }
                    else { equipmentActive.Remove(augment.Id); unit.StatModifiers.RemoveSource(augment.Source); }
                }
            }

            private void OnEquipmentChanged()
            {
                if(!CanApply) return;
                UpdateEquipmentStats();
                // 전투 중 세 번째 장비를 장착해도 적용하되, 합성·교체로 보호막을 반복 지급하지 않음.
                foreach(AugmentDefinition augment in augments)
                    if(augment.Effect==AugmentEffect.FullyEquipped && unit.Equipment.Count==Run.ItemInventoryState.EquipmentCapacity)
                        Shield(augment);
            }

            private void Shield(AugmentDefinition augment)
            {
                if(!CanApply || !used.Add(augment.Id)) return;
                if(unit.Effects.Apply(new EffectDefinition(augment.Id,EffectKind.Shield,
                    Mathf.FloorToInt(unit.Health.MaxHp*augment.Value),augment.Duration>0f?augment.Duration:-1f),augment.Source).IsValid)
                    activated?.Invoke(augment);
            }

            private void OnDamaged(int amount)
            {
                if(amount<=0 || !CanApply) return;
                foreach(AugmentDefinition augment in augments)
                    if(augment.Effect==AugmentEffect.EmergencyShield
                        && unit.Health.CurrentHp<=(double)unit.Health.MaxHp*augment.Threshold) Shield(augment);
            }

            private void OnKilled(KillEvent info)
            {
                if(!CanApply || !info.IsCredited || info.Killer.Unit!=unit) return;
                foreach(AugmentDefinition augment in augments)
                    if(augment.Effect==AugmentEffect.HealOnKill)
                    {
                        int healed=unit.Health.Heal(Mathf.FloorToInt(unit.Health.MaxHp*augment.Value));
                        int manaBefore=unit.Mana.Current;
                        unit.Mana.Add(Mathf.FloorToInt(augment.SecondaryValue));
                        if(healed>0 || unit.Mana.Current>manaBefore) activated?.Invoke(augment);
                    }
            }

            private void OnAllyDied()=>TryLastStand();
            private void TryLastStand()
            {
                if(!CanApply || allies.Count(ally=>ally!=null && ally.isActiveAndEnabled && ally.Health.IsAlive)!=1) return;
                foreach(AugmentDefinition augment in augments)
                    if(augment.Effect==AugmentEffect.LastStand && used.Add(augment.Id))
                    {
                        Set(augment,new StatModifierSpec(StatType.AttackSpeed,ModifierOperation.Percent,augment.Value));
                        unit.Effects.Apply(new EffectDefinition(augment.Id,EffectKind.Shield,
                            Mathf.FloorToInt(unit.Health.MaxHp*augment.SecondaryValue),-1f),augment.Source);
                    }
            }

            private void OnCastReleased(SkillCastContext context)
            {
                if(!CanApply || context.IsEcho) return;
                foreach(AugmentDefinition augment in augments)
                {
                    if(augment.Effect==AugmentEffect.ManaCycle)
                    {
                        int manaBefore=unit.Mana.Current;
                        unit.Mana.Add(Mathf.FloorToInt(augment.Value));
                        if(unit.Mana.Current>manaBefore) activated?.Invoke(augment);
                    }
                    else if(augment.Effect==AugmentEffect.EchoSpell && used.Add(augment.Id))
                    {
                        SkillCastContext original=new SkillCastContext(unit,context.Definition,context.Target,context.CurrentParticipants);
                        EchoAsync(original,augment).Forget();
                    }
                }
            }

            private async UniTask EchoAsync(SkillCastContext original,AugmentDefinition augment)
            {
                SkillCastContext echo=null;
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(augment.Duration),cancellationToken:token);
                    if(!CanApply) return;
                    Unit target=original.Target.IsValid?original.Target.Unit
                        :SkillTargetSelector.Select(unit,original.Definition,original.CurrentParticipants);
                    if(target==null) return;
                    echo=new SkillCastContext(unit,original.Definition,new SkillTarget(target),
                        original.CurrentParticipants,augment.Value,true,new ModifierSource(ModifierSourceKind.Augment,augment.Id,unit.InstanceId));
                    echo.Center=original.Definition.CenterOnCaster?unit.CurrentCell.Value:target.CurrentCell.Value;
                    unit.Skill.NotifyRepeatedRelease(echo);
                    activated?.Invoke(augment);
                    await SkillExecutor.ExecuteAsync(echo,data,token);
                }
                catch(OperationCanceledException) { }
                finally { if(echo!=null && unit!=null) unit.Skill.NotifyEffectFinished(echo); }
            }

            public void Dispose()
            {
                if(bound && unit!=null)
                {
                    unit.Health.DamageTaken-=OnDamaged; unit.Combat.Killed-=OnKilled;
                    unit.Skill.CastReleased-=OnCastReleased; unit.Equipment.Changed-=OnEquipmentChanged;
                }
                if(bound) foreach(Unit ally in allies) if(ally!=null) ally.Health.Died-=OnAllyDied;
                bound=false;
                foreach(AugmentDefinition augment in augments)
                    if(augment.Effect==AugmentEffect.EchoSpell)
                        foreach(CombatUnitReference participant in participants)
                            if(participant.IsCurrent) participant.Unit.Effects.RemoveSource(new ModifierSource(ModifierSourceKind.Augment,augment.Id,reference.InstanceId));
                if(!reference.IsCurrent) return;
                foreach(AugmentDefinition augment in augments)
                {
                    // 상시 증강은 유지하고 이 전투의 조건부 출처만 해제.
                    if(augment.Effect<=AugmentEffect.VictoryGold) continue;
                    unit.Effects.RemoveSource(augment.Source); unit.StatModifiers.RemoveSource(augment.Source);
                }
                used.Clear(); equipmentActive.Clear();
            }
        }
    }
}
