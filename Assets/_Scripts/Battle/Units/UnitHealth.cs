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
        private Unit owner;
        private int fallbackDefense;
        private bool isApplyingDamage;
        public bool IsInitialized=>state!=null;
        public int CurrentHp=>state?.CurrentHp ?? 0;
        public int MaxHp=>state?.MaxHp ?? 0;
        public bool IsAlive=>state!=null && state.IsAlive;
        public int LifeVersion { get; private set; }
        public event Action<int,int> HealthChanged;
        /// <summary>방어·보호막 처리 후 실제 체력 피해 발생.</summary>
        public event Action<int> DamageTaken;
        public event Action<DamageResult> DamageResolved;
        public event Action<KillEvent> DeathResolved;
        public event Action Died;
        private void Awake() { stats=GetComponent<UnitStats>(); effects=GetComponent<UnitEffects>(); }
        private void OnEnable() { if(stats!=null) stats.Changed+=OnStatsChanged; }
        private void OnDisable() { LifeVersion++; if(stats!=null) stats.Changed-=OnStatsChanged; }
        internal void BindUnit(Unit unit)=>owner=unit;
        private void OnStatsChanged() { if(stats.IsInitialized) state?.SetMaxHp(stats.Current.MaxHp); }
        public void Initialize(int maxHp,int defense=0)
        {
            if(state!=null) { state.HealthChanged-=ForwardHealthChanged; state.Died-=ForwardDied; }
            LifeVersion++; fallbackDefense=defense; state=new UnitHealthState(maxHp);
            state.HealthChanged+=ForwardHealthChanged; state.Died+=ForwardDied;
            CombatEventDispatcher.Publish(HealthChanged,CurrentHp,MaxHp);
        }
        private int Defense=>stats!=null && stats.IsInitialized?stats.Current.Defense:fallbackDefense;
        public int TakeDamage(int attackDamage,float multiplier=1f,float armorIgnoreRatio=0f)
            =>TakeDamage(DamageContext.Environment,attackDamage,multiplier,armorIgnoreRatio);
        public int TakeDamage(float attackDamage,float multiplier=1f,float armorIgnoreRatio=0f)
            =>TakeDamage(DamageContext.Environment,attackDamage,multiplier,armorIgnoreRatio);
        public int TakeDamage(DamageContext context,int attackDamage,float multiplier=1f,float armorIgnoreRatio=0f)
            =>ApplyDamage(context,DamageResolver.Calculate(attackDamage,Defense,multiplier,armorIgnoreRatio));
        public int TakeDamage(DamageContext context,float attackDamage,float multiplier=1f,float armorIgnoreRatio=0f)
            =>ApplyDamage(context,DamageResolver.Calculate(attackDamage,Defense,multiplier,armorIgnoreRatio));
        private int ApplyDamage(DamageContext context,int damage)
        {
            if(!IsAlive || isApplyingDamage || damage<=0) return 0;
            var damagedState=state;
            var target=new CombatUnitReference(owner);
            int version=LifeVersion, resolved=damage;
            isApplyingDamage=true;
            try
            {
                if(effects!=null) damage=effects.AbsorbDamage(damage);
                int shieldDamage=resolved-damage;
                // 보호막 이벤트에서 재설정·반환된 대상에 이전 피해를 적용하지 않음.
                if(!IsSameLife(damagedState,version)) return 0;
                int lostHp=damagedState.TakeDamage(damage);
                var result=new DamageResult(context,target,resolved,shieldDamage,lostHp,lostHp>0 && !damagedState.IsAlive);
                if(lostHp==0 && shieldDamage==0) return 0;
                if(lostHp>0 && IsSameLife(damagedState,version)) CombatEventDispatcher.Publish(DamageTaken,lostHp);
                if(IsSameLife(damagedState,version)) CombatEventDispatcher.Publish(DamageResolved,result);
                if(result.IsLethal)
                {
                    if(IsSameLife(damagedState,version)) CombatEventDispatcher.Publish(Died);
                    if(IsSameLife(damagedState,version)) CombatEventDispatcher.Publish(DeathResolved,new KillEvent(result));
                }
                // 출처의 현재 수명을 다시 확인하여 풀의 새 유닛에 이전 사건을 전달하지 않음.
                var attacker=context.Attacker.Unit;
                if(attacker!=null && attacker.Combat!=null) attacker.Combat.NotifyDamageDealt(result);
                return lostHp;
            }
            finally { isApplyingDamage=false; }
        }
        private bool IsSameLife(UnitHealthState damagedState,int version)=>ReferenceEquals(state,damagedState) && LifeVersion==version;
        public int Heal(int amount)=>state?.Heal(amount) ?? 0;
        public void RestoreFullHealth() { LifeVersion++; state?.RestoreFullHealth(); }
        private void ForwardHealthChanged(int hp,int maxHp)=>CombatEventDispatcher.Publish(HealthChanged,hp,maxHp);
        private void ForwardDied()
        {
            // 피해 결과를 고정한 뒤 ApplyDamage에서 사망 이벤트 발행.
            if(!isApplyingDamage) CombatEventDispatcher.Publish(Died);
        }
    }
}
