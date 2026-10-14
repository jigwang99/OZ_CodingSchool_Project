using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using AutoBattler.Data.Units;

namespace AutoBattler.Battle.Units
{
    public readonly struct EffectHandle : IEquatable<EffectHandle>
    {
        public long Value { get; }
        public bool IsValid=>Value!=0;
        internal EffectHandle(long value)=>Value=value;
        public bool Equals(EffectHandle other)=>Value==other.Value;
        public override bool Equals(object other)=>other is EffectHandle handle && Equals(handle);
        public override int GetHashCode()=>Value.GetHashCode();
    }
    public sealed class EffectInstance
    {
        public EffectHandle Handle { get; internal set; }
        public EffectDefinition Definition { get; internal set; }
        public ModifierSource Source { get; internal set; }
        public float RemainingSeconds { get; internal set; }
        public float Magnitude { get; internal set; }
        public int RemainingShield { get; internal set; }
        internal ModifierHandle Modifier;
    }

    /// <summary>지속 시간·중첩·갱신과 효과별 수정자 수명 관리.</summary>
    public sealed class UnitEffectState
    {
        private static long nextHandle;
        private readonly UnitStatState stats;
        private readonly Func<int,int> heal;
        private readonly List<EffectInstance> active=new List<EffectInstance>();
        private readonly ReadOnlyCollection<EffectInstance> view;
        public IReadOnlyList<EffectInstance> Active=>view;
        public int Shield { get; private set; }
        public bool IsStunned { get; private set; }
        public float SlowRate { get; private set; }
        public event Action Changed;
        public UnitEffectState(UnitStatState stats,Func<int,int> heal)
        { this.stats=stats; this.heal=heal; view=active.AsReadOnly(); }

        public EffectHandle Apply(EffectDefinition definition,ModifierSource source,float potency=1f)
        {
            float value=definition.Value*potency;
            if(definition.Kind==EffectKind.Heal)
            { heal(Math.Max(0,(int)Math.Floor(value))); return default; }
            if(definition.Duration==0f) return default;
            var existing=definition.Stacking==EffectStacking.Refresh
                ? active.FirstOrDefault(entry=>entry.Definition.Id==definition.Id && entry.Source.Equals(source)) : null;
            if(existing!=null)
            {
                existing.RemainingSeconds=Math.Max(existing.RemainingSeconds,Duration(definition));
                existing.Magnitude=definition.Kind==EffectKind.Slow ? Math.Max(existing.Magnitude,value) : value;
                existing.RemainingShield=Math.Max(existing.RemainingShield,Amount(value));
                existing.Definition=definition;
                if(definition.Kind==EffectKind.StatModifier)
                {
                    var spec=new StatModifierSpec(definition.Stat,definition.Operation,value);
                    if(!stats.UpdateModifier(existing.Modifier,spec)) existing.Modifier=stats.AddModifier(source,spec);
                }
                Refresh(); return existing.Handle;
            }
            var instance=new EffectInstance
            {
                Handle=new EffectHandle(Interlocked.Increment(ref nextHandle)),Definition=definition,Source=source,
                RemainingSeconds=Duration(definition),Magnitude=value,RemainingShield=Amount(value)
            };
            active.Add(instance);
            if(definition.Kind==EffectKind.StatModifier)
                instance.Modifier=stats.AddModifier(source,new StatModifierSpec(definition.Stat,definition.Operation,value));
            Refresh(); return instance.Handle;
        }
        private static float Duration(EffectDefinition definition)=>definition.Duration<0f?float.PositiveInfinity:definition.Duration;
        private static int Amount(float value)=>(int)Math.Max(0,Math.Min(int.MaxValue,Math.Floor(value)));
        public void Tick(float seconds)
        {
            if(seconds<=0f || active.Count==0) return;
            List<EffectInstance> expired=null;
            foreach(var entry in active)
            {
                entry.RemainingSeconds-=seconds;
                if(entry.RemainingSeconds<=0f) (expired ?? (expired=new List<EffectInstance>())).Add(entry);
            }
            if(expired!=null) RemoveEntries(expired);
        }
        public bool Remove(EffectHandle handle)
        {
            var entry=active.FirstOrDefault(effect=>effect.Handle.Equals(handle));
            if(entry==null) return false;
            RemoveEntries(new[] {entry}); return true;
        }
        public void RemoveSource(ModifierSource source)=>RemoveEntries(active.Where(entry=>entry.Source.Equals(source)).ToArray());
        public void Clear()=>RemoveEntries(active.ToArray());
        private void RemoveEntries(IEnumerable<EffectInstance> entries)
        {
            var removed=new List<ModifierHandle>(); bool changed=false;
            foreach(var entry in entries)
            {
                if(!active.Remove(entry)) continue;
                changed=true; if(entry.Modifier.IsValid) removed.Add(entry.Modifier);
            }
            if(!changed) return;
            stats.RemoveModifiers(removed); Refresh();
        }
        /// <summary>방어 계산을 마친 피해를 만료가 가까운 보호막부터 흡수하고 잔여 피해 반환.</summary>
        public int AbsorbDamage(int damage)
        {
            if(damage<=0 || Shield==0) return Math.Max(0,damage);
            var depleted=new List<EffectInstance>();
            foreach(var shield in active.Where(entry=>entry.Definition.Kind==EffectKind.Shield)
                .OrderBy(entry=>entry.RemainingSeconds).ThenBy(entry=>entry.Handle.Value))
            {
                int absorbed=Math.Min(damage,shield.RemainingShield);
                damage-=absorbed; shield.RemainingShield-=absorbed;
                if(shield.RemainingShield==0) depleted.Add(shield);
                if(damage==0) break;
            }
            foreach(var entry in depleted) active.Remove(entry);
            Refresh(); return damage;
        }
        private void Refresh()
        {
            long shield=0; bool stunned=false; float slow=0f;
            foreach(var entry in active)
            {
                switch(entry.Definition.Kind)
                {
                    case EffectKind.Shield: shield+=entry.RemainingShield; break;
                    case EffectKind.Stun: stunned=true; break;
                    case EffectKind.Slow: slow=Math.Max(slow,entry.Magnitude); break;
                }
            }
            Shield=(int)Math.Min(int.MaxValue,shield); IsStunned=stunned; SlowRate=Math.Max(0f,Math.Min(1f,slow));
            Changed?.Invoke();
        }
    }
}
