using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Units;
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
        [SerializeField,Min(0f)] private float dragThreshold=8f;

        private BoardState boardState = new BoardState();
        private PlacementService placementService;
        private int nextInstanceId = 1;
        private UnitView draggedUnit;
        private Vector3 originalPosition;
        private Vector3 dragOffset;
        private Vector2 pressPosition;
        private UnitState dragState;
        private bool isDragging;
        private bool itemDragging;
        public bool IsItemDragging => itemDragging;
        public Camera BoardCamera => boardCamera;
        private IUnitDropTarget dropTarget;
        private Unit inspectedUnit;
        private int inspectedSpawnVersion;
        private int inspectedInstanceId;

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

        public UnitView SelectedUnit { get; private set; }
        // 풀 반환 후 같은 오브젝트가 재사용되어도 이전 선택을 이어받지 않도록 구분.
        public Unit InspectedUnit => inspectedUnit != null && inspectedUnit.isActiveAndEnabled
            && inspectedUnit.SpawnVersion == inspectedSpawnVersion
            && inspectedUnit.InstanceId == inspectedInstanceId ? inspectedUnit : null;
        public event System.Action PlacementChanged;
        public event System.Action<UnitView> DragStarted;
        public event System.Action<Vector2> DragMoved;
        public event System.Action DragEnded;
        public UnitView DraggedUnit=>isDragging ? draggedUnit : null;
        public void BindDropTarget(IUnitDropTarget target)=>dropTarget=target;
        public void UnbindDropTarget(IUnitDropTarget target)
        { if(ReferenceEquals(dropTarget,target)) { CancelDrag(); dropTarget=null; } }
        public void SetItemDragging(bool active)
        { if (active) CancelDrag(); itemDragging = active; }
        public void Inspect(Unit unit)
        {
            inspectedUnit = unit;
            inspectedSpawnVersion = unit != null ? unit.SpawnVersion : 0;
            inspectedInstanceId = unit != null ? unit.InstanceId : 0;
            SelectedUnit = unit != null && unit.Team == UnitTeam.Ally ? unit.View : null;
        }
        public void ClearSelection() { CancelDrag(); SelectedUnit = null; inspectedUnit = null; }
        public void BindBoard(BoardState board, PlacementService service)
        {
            CancelDrag(); boardState = board; placementService = service;
            playerLevel = service.PlayerLevel;
        }
        public void BindView(UnitView view, UnitState state)
        {
            view.Bind(state, boardState); view.SnapToLocation(gridLayout, benchLayout);
        }
        private void Start()
        {
            if (gridLayout == null || GetComponent<AutoBattler.Run.RunController>() != null)
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

            UnitState state = new UnitState(nextInstanceId++);
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
            if (itemDragging || AutoBattler.Run.GameNavigation.IsTransitioning || battleFlow == null || !battleFlow.CanInspectUnits
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
            if (!battleFlow.CanPlaceUnits)
            {
                CancelDrag();
                return;
            }
            if (draggedUnit == null)
                return;
            if (!draggedUnit.isActiveAndEnabled || !ReferenceEquals(draggedUnit.State,dragState)
                || !boardState.IsRegistered(draggedUnit.State))
            {
                CancelDrag();
                return;
            }

            UpdateDrag(mouseWorldPosition);
            if (draggedUnit!=null && Mouse.current.leftButton.wasReleasedThisFrame)
                EndDrag();
        }

        private void TryBeginDrag(Vector3 mouseWorldPosition)
        {
            // UI 클릭이 보드의 기물 선택으로 이어지지 않도록 차단.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            Collider2D hit = Physics2D.OverlapPoint(mouseWorldPosition, unitLayer);
            Unit unit = hit != null ? hit.GetComponentInParent<Unit>() : null;
            if (hit == null)
            {
                // 적군도 정보 확인 가능. 드래그 등록과 배치 변경은 아군만 허용.
                foreach (Collider2D candidate in Physics2D.OverlapPointAll(mouseWorldPosition))
                {
                    Unit owner = candidate.GetComponentInParent<Unit>();
                    if (candidate.gameObject.scene != gameObject.scene || owner == null
                        || !owner.isActiveAndEnabled || owner.Definition == null) continue;
                    hit = candidate; unit = owner; break;
                }
            }
            if (hit == null || hit.gameObject.scene != gameObject.scene)
            {
                ClearSelection();
                return;
            }
            inspectedUnit = unit;
            inspectedSpawnVersion = unit != null ? unit.SpawnVersion : 0;
            inspectedInstanceId = unit != null ? unit.InstanceId : 0;
            SelectedUnit = unit != null && unit.Team == UnitTeam.Ally ? unit.View : null;
            if (!battleFlow.CanPlaceUnits || unit != null && unit.Team == UnitTeam.Enemy)
                return;
            draggedUnit = RegisterUnit(hit);
            if (draggedUnit == null)
                return;
            SelectedUnit = draggedUnit;
            originalPosition = draggedUnit.transform.position;
            dragOffset = originalPosition - mouseWorldPosition;
            pressPosition=Mouse.current.position.ReadValue(); dragState=draggedUnit.State;
        }

        private void UpdateDrag(Vector3 mouseWorldPosition)
        {
            Vector2 screenPosition=Mouse.current.position.ReadValue();
            if(!isDragging)
            {
                if((screenPosition-pressPosition).sqrMagnitude<dragThreshold*dragThreshold) return;
                isDragging=true; DragStarted?.Invoke(draggedUnit);
                if(draggedUnit==null) return;
            }
            // 드래그 중에는 논리 좌표와 점유를 바꾸지 않음.
            draggedUnit.transform.position = mouseWorldPosition + dragOffset;
            ClearHighlights();
            DragMoved?.Invoke(screenPosition);
            if(dropTarget!=null && dropTarget.Contains(screenPosition)) return;
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
            if(!isDragging) { CancelDrag(); return; }
            IUnitDropTarget target=dropTarget;
            if(target!=null && target.Contains(Mouse.current.position.ReadValue()))
            {
                UnitView unit=draggedUnit;
                unit.transform.position=originalPosition;
                // 판매 콜백에서 풀로 반환되기 전에 드래그 참조와 강조 해제.
                ClearDrag();
                target.TryDrop(unit);
                Physics2D.SyncTransforms(); return;
            }
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
            PlacementChanged?.Invoke();
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

        public void CancelDrag()
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
            bool ended=isDragging;
            isDragging=false; dragState=null;
            draggedUnit = null;
            ClearHighlights();
            if(ended) DragEnded?.Invoke();
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
