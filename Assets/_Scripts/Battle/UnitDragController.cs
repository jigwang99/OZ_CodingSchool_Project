using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Placement;
using AutoBattler.Presentation.Units;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace AutoBattler.Battle.Grid
{
    public class UnitDragController : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private HexGridLayout gridLayout;
        [SerializeField] private BenchLayout benchLayout;
        [SerializeField] private Camera boardCamera;
        [SerializeField] private BattleFlowController battleFlow;

        [Header("플레이어 레벨과 전장 배치 한도")]
        [SerializeField, Range(1, 10)] private int playerLevel = 1;

        [Header("드래그 가능한 아군 기물 레이어")]
        [SerializeField] private LayerMask unitLayer;

        private readonly BoardState boardState = new BoardState();
        private PlacementService placementService;
        private int nextInstanceId = 1;
        private UnitView draggedUnit;
        private Vector3 originalPosition;
        private Vector3 dragOffset;

        private void Awake()
        {
            if (boardCamera == null)
                boardCamera = Camera.main;
            placementService = new PlacementService(boardState, playerLevel: Mathf.Clamp(playerLevel, 1, 10));
            if (benchLayout == null && gridLayout != null)
            {
                benchLayout = gridLayout.GetComponent<BenchLayout>();
                if (benchLayout == null)
                    benchLayout = gridLayout.gameObject.AddComponent<BenchLayout>();
            }
        }

        private void OnEnable()
        {
            if (battleFlow != null)
                battleFlow.PhaseChanged += OnPhaseChanged;
            else
                Debug.LogError("UnitDragController에 BattleFlowController 연결 필요.", this);
        }

        private void OnPhaseChanged(BattlePhase phase)
        {
            if (phase != BattlePhase.Preparation)
                CancelDrag();
        }

        private void Start()
        {
            if (gridLayout == null)
                return;

            // 기존 씬의 기물도 등록하여 첫 드래그 전부터 점유를 검사.
            Collider2D[] colliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.InstanceID);
            foreach (Collider2D collider in colliders)
            {
                if (collider.enabled && collider.gameObject.scene == gameObject.scene
                    && (unitLayer.value & (1 << collider.gameObject.layer)) != 0)
                    RegisterUnit(collider);
            }
            Physics2D.SyncTransforms();
        }

        private UnitView RegisterUnit(Collider2D collider)
        {
            UnitView view = collider.GetComponentInParent<UnitView>();
            if (view == null)
                view = collider.gameObject.AddComponent<UnitView>();
            if (!view.isActiveAndEnabled)
                return null;
            if (boardState.IsRegistered(view.State))
                return view;

            var state = new UnitState(nextInstanceId++);
            HexCoord cell = gridLayout.WorldToCell(view.transform.position);
            bool registered = false;
            if (benchLayout != null && benchLayout.TryWorldToSlot(view.transform.position, out int initialSlot))
                registered = placementService.TryRegisterUnit(state, UnitLocation.OnBench(initialSlot));
            else if (placementService.IsInsideAllyArea(cell))
                registered = placementService.TryRegisterUnit(state, UnitLocation.OnBoard(cell));

            // 영역 밖, 중복 배치, 인원 초과 기물은 빈 대기석에 배치.
            if (!registered)
            {
                int freeSlot = placementService.FindFreeBenchSlot();
                registered = freeSlot >= 0 && benchLayout != null
                    && placementService.TryRegisterUnit(state, UnitLocation.OnBench(freeSlot));
            }
            if (!registered)
            {
                Debug.LogWarning("대기석이 가득 차 기물을 등록할 수 없습니다.", view);
                return null;
            }
            view.Bind(state, boardState);
            view.SnapToLocation(gridLayout, benchLayout);
            return view;
        }

        private void Update()
        {
            if (battleFlow == null || !battleFlow.CanPlaceUnits
                || Mouse.current == null || boardCamera == null || gridLayout == null)
            {
                CancelDrag();
                return;
            }

            if (placementService.PlayerLevel != playerLevel)
                placementService.SetPlayerLevel(Mathf.Clamp(playerLevel, 1, 10));

            Vector3 mouseWorldPosition = ScreenPositionUtility.ScreenToWorld(
                boardCamera, Mouse.current.position.ReadValue(), gridLayout.transform.position.z);

            if (draggedUnit == null)
            {
                // 드래그 대상이 파괴된 경우에도 강조를 해제.
                ClearDrag();
                if (Mouse.current.leftButton.wasPressedThisFrame)
                    TryBeginDrag(mouseWorldPosition);
            }
            if (draggedUnit == null)
                return;
            if (!draggedUnit.isActiveAndEnabled || !boardState.IsRegistered(draggedUnit.State))
            {
                CancelDrag();
                return;
            }

            UpdateDrag(mouseWorldPosition);
            if (Mouse.current.leftButton.wasReleasedThisFrame)
                EndDrag();
        }

        private void TryBeginDrag(Vector3 mouseWorldPosition)
        {
            // UI 클릭이 보드의 기물 선택으로 이어지지 않도록 차단.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            Collider2D hit = Physics2D.OverlapPoint(mouseWorldPosition, unitLayer);
            if (hit == null || hit.gameObject.scene != gameObject.scene)
                return;
            draggedUnit = RegisterUnit(hit);
            if (draggedUnit == null)
                return;
            originalPosition = draggedUnit.transform.position;
            dragOffset = originalPosition - mouseWorldPosition;
        }

        private void UpdateDrag(Vector3 mouseWorldPosition)
        {
            // 드래그 중에는 논리 좌표와 점유를 바꾸지 않음.
            draggedUnit.transform.position = mouseWorldPosition + dragOffset;
            ClearHighlights();
            if (!TryGetDestination(draggedUnit.transform.position, out UnitLocation destination)
                || !placementService.CanMoveUnit(draggedUnit.State, destination)) return;
            if (destination.Kind == UnitLocationKind.Board)
                gridLayout.SetHighlightTile(destination.Cell);
            else
                benchLayout.SetHighlightedSlot(destination.BenchSlot);
        }

        private bool TryGetDestination(Vector3 world, out UnitLocation destination)
        {
            if (benchLayout != null && benchLayout.TryWorldToSlot(world, out int slot))
            {
                destination = UnitLocation.OnBench(slot);
                return true;
            }
            HexCoord cell = gridLayout.WorldToCell(world);
            destination = UnitLocation.OnBoard(cell);
            return placementService.IsInsideAllyArea(cell);
        }

        private void EndDrag()
        {
            PlacementResult result = TryGetDestination(draggedUnit.transform.position, out UnitLocation destination)
                ? placementService.TryMoveUnit(draggedUnit.State, destination) : new PlacementResult(false);
            if (result.Success)
            {
                draggedUnit.SnapToLocation(gridLayout, benchLayout);
                if (result.SwappedUnit != null)
                {
                    UnitView targetView = FindUnitView(result.SwappedUnit);
                    if (targetView != null) targetView.SnapToLocation(gridLayout, benchLayout);
                }
            }
            else
                draggedUnit.transform.position = originalPosition;
            Physics2D.SyncTransforms();
            ClearDrag();
        }

        /// <summary>레벨 시스템에서 레벨 변경 시 호출.</summary>
        public void SetPlayerLevel(int level)
        {
            if (level < 1 || level > 10) throw new System.ArgumentOutOfRangeException(nameof(level));
            playerLevel = level;
            if (placementService != null) placementService.SetPlayerLevel(level);
        }

        public int DeployedCount => boardState.DeployedCount;

        private UnitView FindUnitView(UnitState state)
        {
            // 교환이 확정된 순간에만 상대의 화면 오브젝트를 조회.
            foreach (UnitView view in FindObjectsByType<UnitView>(FindObjectsSortMode.None))
            {
                if (ReferenceEquals(view.State, state))
                    return view;
            }
            return null;
        }

        private void CancelDrag()
        {
            if (draggedUnit != null)
            {
                draggedUnit.transform.position = originalPosition;
                Physics2D.SyncTransforms();
            }
            ClearDrag();
        }

        private void ClearDrag()
        {
            draggedUnit = null;
            ClearHighlights();
        }

        private void ClearHighlights()
        {
            if (gridLayout != null) gridLayout.SetHighlightTile(null);
            if (benchLayout != null) benchLayout.SetHighlightedSlot(null);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                CancelDrag();
        }

        private void OnDisable()
        {
            if (battleFlow != null)
                battleFlow.PhaseChanged -= OnPhaseChanged;
            CancelDrag();
        }
    }
}
