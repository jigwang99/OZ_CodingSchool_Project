using System;

namespace AutoBattler.Battle.Units
{
    /// <summary>Unity 오브젝트와 독립적인 체력 계산 및 생존 상태.</summary>
    public sealed class UnitHealthState
    {
        public int CurrentHp { get; private set; }
        public int MaxHp { get; }
        public int Defense { get; }
        public bool IsAlive => CurrentHp > 0;
        public event Action<int, int> HealthChanged;
        public event Action Died;
        private bool isUpdatingHealth;

        public UnitHealthState(int maxHp, int defense)
        {
            if (maxHp < 1) throw new ArgumentOutOfRangeException(nameof(maxHp));
            if (defense < 0) throw new ArgumentOutOfRangeException(nameof(defense));
            MaxHp = maxHp;
            Defense = defense;
            CurrentHp = maxHp;
        }

        /// <summary>방어력을 적용한 실제 체력 감소량 반환. 양수 공격은 최소 1 피해.</summary>
        public int TakeDamage(int attackDamage)
        {
            if (attackDamage < 0) throw new ArgumentOutOfRangeException(nameof(attackDamage));
            if (!IsAlive || attackDamage == 0 || isUpdatingHealth) return 0;
            int damage = Math.Max(1, attackDamage - Defense);
            int lostHp = Math.Min(CurrentHp, damage);
            isUpdatingHealth = true;
            try
            {
                CurrentHp -= lostHp;
                HealthChanged?.Invoke(CurrentHp, MaxHp);
                if (!IsAlive) Died?.Invoke();
                return lostHp;
            }
            finally { isUpdatingHealth = false; }
        }

        /// <summary>최대 체력으로 복구하고 생존 상태 초기화.</summary>
        public void RestoreFullHealth()
        {
            if (isUpdatingHealth) return;
            isUpdatingHealth = true;
            try
            {
                CurrentHp = MaxHp;
                HealthChanged?.Invoke(CurrentHp, MaxHp);
            }
            finally { isUpdatingHealth = false; }
        }
    }
}
