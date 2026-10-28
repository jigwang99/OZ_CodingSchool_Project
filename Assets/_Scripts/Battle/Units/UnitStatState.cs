using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoBattler.Data.Units;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    /// <summary>기본값을 유지하고 출처별 수정자에서 최종 능력치 재계산.</summary>
    public sealed class UnitStatState
    {
        private static long nextHandle;
        private readonly Dictionary<ModifierHandle,StatModifier> modifiers=new Dictionary<ModifierHandle,StatModifier>();
        private float baseCritChance,baseCritMultiplier;
        public UnitStat Base { get; private set; }
        public UnitStat Current { get; private set; }
        public float CritChance { get; private set; }
        public float CritMultiplier { get; private set; }=1.5f;
        public float SkillPower { get; private set; }=1f;
        public float BasicAttackPower { get; private set; }=1f;
        public int ModifierCount=>modifiers.Count;
        public bool IsInitialized=>Base!=null;
        public IReadOnlyList<StatModifier> Modifiers=>Array.AsReadOnly(modifiers.Values.ToArray());
        public event Action Changed;

        public void Initialize(UnitStat stats,float critChance=.1f,float critMultiplier=1.5f)
        {
            Base=stats.Clone(); baseCritChance=critChance; baseCritMultiplier=critMultiplier;
            modifiers.Clear(); Recalculate();
        }
        public ModifierHandle AddModifier(ModifierSource source,StatModifierSpec spec)
        {
            ModifierHandle handle=Add(source,spec); Recalculate(); return handle;
        }
        private ModifierHandle Add(ModifierSource source,StatModifierSpec spec)
        {
            // 풀 재사용 이후에도 이전 Handle을 재발급하지 않음.
            ModifierHandle handle=new ModifierHandle(Interlocked.Increment(ref nextHandle));
            modifiers.Add(handle,new StatModifier(handle,source,spec)); return handle;
        }
        public bool RemoveModifier(ModifierHandle handle)
        {
            if(!modifiers.Remove(handle)) return false;
            Recalculate(); return true;
        }
        public bool UpdateModifier(ModifierHandle handle,StatModifierSpec spec)
        {
            if(!modifiers.TryGetValue(handle,out StatModifier old)) return false;
            modifiers[handle]=new StatModifier(handle,old.Source,spec); Recalculate(); return true;
        }
        public void RemoveModifiers(IEnumerable<ModifierHandle> handles)
        {
            bool changed=false;
            foreach(ModifierHandle handle in handles) changed|=modifiers.Remove(handle);
            if(changed) Recalculate();
        }
        public void RemoveSource(ModifierSource source)
        { RemoveModifiers(modifiers.Values.Where(modifier=>modifier.Source.Equals(source)).Select(modifier=>modifier.Handle).ToArray()); }
        public ModifierHandle[] ReplaceSource(ModifierSource source,params StatModifierSpec[] specs)
        {
            foreach(ModifierHandle handle in modifiers.Values.Where(modifier=>modifier.Source.Equals(source)).Select(modifier=>modifier.Handle).ToArray())
                modifiers.Remove(handle);
            ModifierHandle[] handles=specs.Select(spec=>Add(source,spec)).ToArray();
            // 중간 제거 상태를 알리지 않고 교체 결과를 한 번 전달.
            Recalculate(); return handles;
        }
        public void ClearModifiers()
        {
            if(modifiers.Count==0) return;
            modifiers.Clear(); Recalculate();
        }
        private float Calculate(StatType type,float value)
        {
            double flat=0,percent=0;
            foreach(StatModifier modifier in modifiers.Values)
            {
                if(modifier.Spec.Stat!=type) continue;
                if(modifier.Spec.Operation==ModifierOperation.Flat) flat+=modifier.Spec.Value;
                else percent+=modifier.Spec.Value;
            }
            return (float)((value+flat)*(1+percent));
        }
        private void Recalculate()
        {
            if(!IsInitialized) return;
            float attackSpeed=Mathf.Max(.01f,Calculate(StatType.AttackSpeed,1f));
            Current=new UnitStat(Mathf.Max(1,Mathf.FloorToInt(Calculate(StatType.MaxHp,Base.MaxHp))),
                Mathf.Max(0,Mathf.FloorToInt(Calculate(StatType.AttackDamage,Base.AttackDamage))),
                Mathf.Max(0,Mathf.FloorToInt(Calculate(StatType.Defense,Base.Defense))),
                Mathf.Max(0f,Calculate(StatType.MoveSpeed,Base.MoveSpeed)),Base.AttackRange,
                Mathf.Max(.01f,Base.AttackCooldown/attackSpeed));
            CritChance=Mathf.Clamp01(Calculate(StatType.CritChance,baseCritChance));
            CritMultiplier=Mathf.Max(1f,Calculate(StatType.CritMultiplier,baseCritMultiplier));
            SkillPower=Mathf.Max(0f,Calculate(StatType.SkillPower,1f));
            BasicAttackPower=Mathf.Max(0f,Calculate(StatType.BasicAttackPower,1f));
            Changed?.Invoke();
        }
    }
}
