using System;
using AutoBattler.Data.Units;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    [DisallowMultipleComponent]
    public sealed class UnitStats : MonoBehaviour
    {
        private readonly UnitStatState state=new UnitStatState();
        public UnitStatState State=>state;
        public UnitStat Current=>state.Current;
        public bool IsInitialized=>state.IsInitialized;
        public float CritChance=>state.CritChance;
        public float CritMultiplier=>state.CritMultiplier;
        public float SkillPower=>state.SkillPower;
        public float BasicAttackPower=>state.BasicAttackPower;
        public event Action Changed { add=>state.Changed+=value; remove=>state.Changed-=value; }
        public void Initialize(UnitStat stats,float critChance=.1f,float critMultiplier=1.5f)=>state.Initialize(stats,critChance,critMultiplier);
        public ModifierHandle AddModifier(ModifierSource source,StatType stat,ModifierOperation operation,float value)
            =>state.AddModifier(source,new StatModifierSpec(stat,operation,value));
        public bool RemoveModifier(ModifierHandle handle)=>state.RemoveModifier(handle);
        public void RemoveSource(ModifierSource source)=>state.RemoveSource(source);
        public ModifierHandle[] ReplaceSource(ModifierSource source,params StatModifierSpec[] specs)=>state.ReplaceSource(source,specs);
        public void ClearModifiers()=>state.ClearModifiers();
    }
}
