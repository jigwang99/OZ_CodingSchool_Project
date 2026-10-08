using System;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    /// <summary>체력, 피해, 회복 및 사망 이벤트만 담당.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitHealth : MonoBehaviour
    {
        public bool IsInitialized => state != null;
        public int CurrentHp => state != null ? state.CurrentHp : 0;
        public int MaxHp => state != null ? state.MaxHp : 0;
        public bool IsAlive => state != null && state.IsAlive;
        public event Action<int, int> HealthChanged;
        public event Action Died;
        private UnitHealthState state;

        public void Initialize(int maxHp, int defense)
        {
            if (state != null) throw new InvalidOperationException("체력 모듈 중복 초기화.");
            state = new UnitHealthState(maxHp, defense);
            state.HealthChanged += ForwardHealthChanged;
            state.Died += ForwardDied;
            HealthChanged?.Invoke(CurrentHp, MaxHp);
        }

        public int TakeDamage(int attackDamage) => state != null ? state.TakeDamage(attackDamage) : 0;
        public void RestoreFullHealth() => state?.RestoreFullHealth();
        private void ForwardHealthChanged(int hp, int maxHp) => HealthChanged?.Invoke(hp, maxHp);
        private void ForwardDied() => Died?.Invoke();
    }
}
