using AutoBattler.Battle.Flow;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    /// <summary>전투 참여, 공격 사거리 및 공격 쿨다운 담당.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Unit), typeof(UnitHealth))]
    public sealed class UnitCombat : MonoBehaviour
    {
        [Header("공격 피해 확인")]
        [SerializeField, Min(1)] private int testAttackDamage = 20;

        private Unit unit;
        private UnitHealth health;
        private float nextAttackTime;
        public bool CanFight => isActiveAndEnabled && unit != null && unit.IsInCombat
            && health != null && health.isActiveAndEnabled && health.IsAlive;
        public bool CanAttack => CanFight && Time.time >= nextAttackTime
            && (unit.Move == null || !unit.Move.IsMoving);

        private void Awake()
        {
            unit = GetComponent<Unit>();
            health = GetComponent<UnitHealth>();
        }

        private void OnEnable()
        {
            if (unit != null && unit.BattleFlow != null)
                unit.BattleFlow.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (unit != null && unit.BattleFlow != null)
                unit.BattleFlow.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(BattlePhase phase) => nextAttackTime = 0f;

        public bool IsInAttackRange(UnitCombat target)
        {
            return target != null && target != this && unit != null && unit.Stats != null
                && unit.CurrentCell.HasValue && target.unit != null && target.unit.CurrentCell.HasValue
                && unit.CurrentCell.Value.DistanceTo(target.unit.CurrentCell.Value) <= unit.Stats.AttackRange;
        }

        public bool TryAttack(UnitCombat target)
        {
            if (!CanAttack || target == null || !target.CanFight || !IsInAttackRange(target))
                return false;

            // 피해 이벤트 안에서 같은 기물이 중복 공격하지 않도록 먼저 예약.
            float previousAttackTime = nextAttackTime;
            nextAttackTime = Time.time + unit.Stats.AttackCooldown;
            if (target.ReceiveAttack(unit.Stats.AttackDamage) > 0) return true;
            nextAttackTime = previousAttackTime;
            return false;
        }

        /// <summary>전투 조건을 확인한 뒤 체력 모듈에 피해 전달.</summary>
        public int ReceiveAttack(int attackDamage) => CanFight ? health.TakeDamage(attackDamage) : 0;

        [ContextMenu("테스트/공격 피해 적용")]
        private void ApplyTestDamage()
        {
            if (!Application.isPlaying || !CanFight)
            {
                Debug.Log("전투 단계에 전장에 배치된 살아 있는 기물만 테스트 가능.", this);
                return;
            }
            ReceiveAttack(testAttackDamage);
        }
    }
}
