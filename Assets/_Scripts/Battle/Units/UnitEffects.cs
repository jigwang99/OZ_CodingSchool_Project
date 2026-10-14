using System;
using System.Collections.Generic;
using AutoBattler.Battle.Flow;
using AutoBattler.Data.Units;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    [DisallowMultipleComponent,RequireComponent(typeof(UnitStats),typeof(UnitHealth))]
    public sealed class UnitEffects : MonoBehaviour
    {
        private UnitEffectState state;
        private Unit unit;
        private UnitHealth health;
        private BattleFlowController flow;
        private UnitEffectState State=>state ?? (state=new UnitEffectState(GetComponent<UnitStats>().State,GetComponent<UnitHealth>().Heal));
        public IReadOnlyList<EffectInstance> Active=>State.Active;
        public int Shield=>State.Shield;
        public bool IsStunned=>State.IsStunned;
        public float SlowRate=>State.SlowRate;
        public event Action Changed { add=>State.Changed+=value; remove=>State.Changed-=value; }
        private void Awake() { unit=GetComponent<Unit>(); health=GetComponent<UnitHealth>(); }
        private void OnEnable()
        {
            health.Died+=Clear;
            BindBattleFlow(unit!=null?unit.BattleFlow:null);
        }
        public void BindBattleFlow(BattleFlowController controller)
        {
            if(flow!=null) flow.PhaseChanged-=OnPhaseChanged;
            flow=controller;
            if(isActiveAndEnabled && flow!=null) flow.PhaseChanged+=OnPhaseChanged;
        }
        private void OnPhaseChanged(BattlePhase phase)
        { if(phase!=BattlePhase.Combat) Clear(); }
        private void Update()
        { if(unit!=null && unit.IsInCombat) State.Tick(Time.deltaTime); }
        public EffectHandle Apply(EffectDefinition definition,ModifierSource source,float potency=1f)
            =>GetComponent<UnitHealth>().IsAlive ? State.Apply(definition,source,potency) : default;
        public bool Remove(EffectHandle handle)=>State.Remove(handle);
        public void RemoveSource(ModifierSource source)=>State.RemoveSource(source);
        public void Clear() { if(state!=null) state.Clear(); }
        public int AbsorbDamage(int damage)=>State.AbsorbDamage(damage);
        private void OnDisable()
        {
            if(health!=null) health.Died-=Clear;
            if(flow!=null) flow.PhaseChanged-=OnPhaseChanged;
            flow=null; Clear();
        }
    }
}
