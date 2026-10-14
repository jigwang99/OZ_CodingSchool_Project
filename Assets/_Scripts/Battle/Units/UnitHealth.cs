using System;
using AutoBattler.Battle.Combat;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    /// <summary>최종 피해의 보호막·체력 적용, 회복 및 사망 이벤트 담당.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitHealth : MonoBehaviour
    {
        private UnitHealthState state;
        private UnitStats stats;
        private UnitEffects effects;
        private int fallbackDefense;
        private bool isApplyingDamage;
        public bool IsInitialized=>state!=null;
        public int CurrentHp=>state?.CurrentHp ?? 0;
        public int MaxHp=>state?.MaxHp ?? 0;
        public bool IsAlive=>state!=null && state.IsAlive;
        public event Action<int,int> HealthChanged;
        public event Action Died;
        private void Awake() { stats=GetComponent<UnitStats>(); effects=GetComponent<UnitEffects>(); }
        private void OnEnable() { if(stats!=null) stats.Changed+=OnStatsChanged; }
        private void OnDisable() { if(stats!=null) stats.Changed-=OnStatsChanged; }
        private void OnStatsChanged() { if(stats.IsInitialized) state?.SetMaxHp(stats.Current.MaxHp); }
        public void Initialize(int maxHp,int defense=0)
        {
            if(state!=null) { state.HealthChanged-=ForwardHealthChanged; state.Died-=ForwardDied; }
            fallbackDefense=defense; state=new UnitHealthState(maxHp);
            state.HealthChanged+=ForwardHealthChanged; state.Died+=ForwardDied;
            HealthChanged?.Invoke(CurrentHp,MaxHp);
        }
        public int TakeDamage(int attackDamage,float multiplier=1f,float armorIgnoreRatio=0f)
        {
            if(!IsAlive || isApplyingDamage) return 0;
            isApplyingDamage=true;
            try
            {
                int defense=stats!=null && stats.IsInitialized?stats.Current.Defense:fallbackDefense;
                int damage=DamageResolver.Calculate(attackDamage,defense,multiplier,armorIgnoreRatio);
                if(effects!=null) damage=effects.AbsorbDamage(damage);
                return state.TakeDamage(damage);
            }
            finally { isApplyingDamage=false; }
        }
        public int Heal(int amount)=>state?.Heal(amount) ?? 0;
        public void RestoreFullHealth()=>state?.RestoreFullHealth();
        private void ForwardHealthChanged(int hp,int maxHp)=>HealthChanged?.Invoke(hp,maxHp);
        private void ForwardDied()=>Died?.Invoke();
    }
}
