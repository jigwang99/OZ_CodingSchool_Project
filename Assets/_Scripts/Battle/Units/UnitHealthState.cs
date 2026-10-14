using System;

namespace AutoBattler.Battle.Units
{
    /// <summary>최종 피해·회복과 현재 체력만 관리. 방어 계산은 DamageResolver에서 처리.</summary>
    public sealed class UnitHealthState
    {
        public int CurrentHp { get; private set; }
        public int MaxHp { get; private set; }
        public bool IsAlive=>CurrentHp>0;
        public event Action<int,int> HealthChanged;
        public event Action Died;
        private bool isUpdatingHealth;
        private int? pendingMaxHp;
        public UnitHealthState(int maxHp)
        {
            if(maxHp<1) throw new ArgumentOutOfRangeException(nameof(maxHp));
            MaxHp=maxHp; CurrentHp=maxHp;
        }
        public int TakeDamage(int finalDamage)
        {
            if(!IsAlive || finalDamage<=0 || isUpdatingHealth) return 0;
            int lost=Math.Min(CurrentHp,finalDamage); isUpdatingHealth=true;
            try
            {
                CurrentHp-=lost; HealthChanged?.Invoke(CurrentHp,MaxHp);
                if(!IsAlive) Died?.Invoke(); return lost;
            }
            finally { FinishUpdate(); }
        }
        public int Heal(int amount)
        {
            if(!IsAlive || amount<=0 || isUpdatingHealth) return 0;
            int restored=Math.Min(amount,MaxHp-CurrentHp);
            if(restored==0) return 0;
            isUpdatingHealth=true;
            try { CurrentHp+=restored; HealthChanged?.Invoke(CurrentHp,MaxHp); return restored; }
            finally { FinishUpdate(); }
        }
        public void SetMaxHp(int value)
        {
            value=Math.Max(1,value);
            if(isUpdatingHealth) { pendingMaxHp=value; return; }
            if(MaxHp==value) return;
            isUpdatingHealth=true;
            try
            {
                MaxHp=value;
                // 전투 중 최대 체력 증가로 회복을 지급하지 않음. 감소 시 현재 체력 제한.
                CurrentHp=Math.Min(CurrentHp,MaxHp); HealthChanged?.Invoke(CurrentHp,MaxHp);
            }
            finally { FinishUpdate(); }
        }
        public void RestoreFullHealth()
        {
            if(isUpdatingHealth) return;
            isUpdatingHealth=true;
            try { CurrentHp=MaxHp; HealthChanged?.Invoke(CurrentHp,MaxHp); }
            finally { FinishUpdate(); }
        }
        private void FinishUpdate()
        {
            isUpdatingHealth=false;
            if(!pendingMaxHp.HasValue) return;
            int value=pendingMaxHp.Value; pendingMaxHp=null; SetMaxHp(value);
        }
    }
}
