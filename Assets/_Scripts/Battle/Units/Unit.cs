using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Data.Units;
using AutoBattler.Presentation.Units;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    /// <summary>기물의 공통 데이터 초기화와 전투 단계 연결.</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent, RequireComponent(typeof(UnitView), typeof(UnitHealth))]
    public sealed class Unit : MonoBehaviour
    {
        [Header("기물 데이터")]
        [SerializeField] private UnitData unitData;
        [SerializeField] private UnitType unitType;
        [Header("씬 참조")]
        [SerializeField] private BattleFlowController battleFlow;
        [SerializeField] private HexGridLayout gridLayout;
        [SerializeField] private BenchLayout benchLayout;

        public UnitStat Stats { get; private set; }
        public UnitView View { get; private set; }
        public UnitHealth Health { get; private set; }
        public UnitMove Move { get; private set; }
        public HexGridLayout GridLayout => gridLayout;
        public BattleFlowController BattleFlow => battleFlow;
        public bool IsOnBoard => View != null && View.State != null
            && View.State.Location.Kind == UnitLocationKind.Board;
        public bool IsInCombat => isActiveAndEnabled && Stats != null && IsOnBoard
            && battleFlow != null && battleFlow.isActiveAndEnabled
            && battleFlow.CurrentPhase == BattlePhase.Combat;

        // 준비 배치와 전투 중 이동 좌표를 분리.
        private HexCoord? combatCell;
        public HexCoord? CurrentCell => combatCell ?? (View != null ? View.State?.Cell : null);

        private void Awake()
        {
            View = GetComponent<UnitView>();
            Health = GetComponent<UnitHealth>();
            Move = GetComponent<UnitMove>();
            if (unitData == null || !unitData.TryGetStat(unitType, out UnitStat stats))
            {
                Debug.LogError($"기물 스탯 연결 필요: {unitType}", this);
                return;
            }
            try
            {
                Stats = stats.Clone();
                Health.Initialize(Stats.MaxHp, Stats.Defense);
            }
            catch (System.ArgumentException exception)
            {
                Debug.LogError($"기물 스탯 오류: {unitType}, {exception.ParamName}", this);
            }
        }

        private void OnEnable()
        {
            if (battleFlow != null) battleFlow.PhaseChanged += OnPhaseChanged;
            else Debug.LogError("Unit에 BattleFlowController 연결 필요.", this);
        }

        private void OnDisable()
        {
            if (battleFlow != null) battleFlow.PhaseChanged -= OnPhaseChanged;
            if (Move != null) Move.Stop();
        }

        private void OnPhaseChanged(BattlePhase phase)
        {
            if (Move != null) Move.Stop();
            if (phase == BattlePhase.Combat)
            {
                combatCell = View != null ? View.State?.Cell : null;
                Health.RestoreFullHealth();
            }
            else if (phase == BattlePhase.Preparation)
            {
                combatCell = null;
                Health.RestoreFullHealth();
                if (View != null && gridLayout != null)
                    View.SnapToLocation(gridLayout, benchLayout);
            }
        }

        internal void SetCombatCell(HexCoord cell) => combatCell = cell;
    }
}
