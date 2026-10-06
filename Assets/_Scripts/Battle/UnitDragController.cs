using UnityEngine;
using UnityEngine.InputSystem;

namespace AutoBattler.Battle.Grid
{
    public class UnitDragController : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private HexGridLayout gridLayout;
        [SerializeField] private Camera boardCamera;

        [Header("드래그 가능한 아군 기물 레이어")]
        [SerializeField] private LayerMask unitLayer;

        private Transform draggedUnit;
        private Vector3 originalPosition;
        private Vector3 dragOffset;

        private void Awake()
        {
            if (boardCamera == null)
                boardCamera = Camera.main;
        }

        private void Update()
        {
            if (Mouse.current == null
                || boardCamera == null
                || gridLayout == null)
            {
                CancelDrag();
                return;
            }

            Vector3 mouseWorldPosition = ScreenPositionUtility.ScreenToWorld(
                boardCamera,
                Mouse.current.position.ReadValue(),
                gridLayout.transform.position.z);

            if (draggedUnit == null
                && Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryBeginDrag(mouseWorldPosition);
            }

            if (draggedUnit == null)
                return;

            UpdateDrag(mouseWorldPosition);

            if (Mouse.current.leftButton.wasReleasedThisFrame)
                EndDrag();
        }

        private void TryBeginDrag(Vector3 mouseWorldPosition)
        {
            Collider2D hit = Physics2D.OverlapPoint(
                mouseWorldPosition,
                unitLayer);

            if (hit == null)
                return;

            // 현재 단계에서는 Collider2D를 기물 루트에 붙입니다.
            draggedUnit = hit.transform;
            originalPosition = draggedUnit.position;
            dragOffset = originalPosition - mouseWorldPosition;
        }

        private void UpdateDrag(Vector3 mouseWorldPosition)
        {
            draggedUnit.position = mouseWorldPosition + dragOffset;

            HexCoord candidate =
                gridLayout.WorldToCell(draggedUnit.position);

            if (IsInsideAllyArea(candidate))
                gridLayout.SetHighlightTile(candidate);
            else
                gridLayout.SetHighlightTile(null);
        }

        private void EndDrag()
        {
            HexCoord candidate =
                gridLayout.WorldToCell(draggedUnit.position);

            if (IsInsideAllyArea(candidate))
            {
                Vector3 snappedPosition =
                    gridLayout.CellToWorld(candidate);

                // 기물이 사용하던 표시 깊이를 유지합니다.
                snappedPosition.z = originalPosition.z;
                draggedUnit.position = snappedPosition;

                // 이후 이곳에서 배치 시스템에 좌표 변경을 요청합니다.
            }
            else
            {
                draggedUnit.position = originalPosition;
            }

            ClearDrag();
        }

        private bool IsInsideAllyArea(HexCoord cell)
        {
            if (!gridLayout.IsInsideBoard(cell))
                return false;

            cell.ToOffset(out int col, out _);

            return col < 4;
        }

        private void CancelDrag()
        {
            if (draggedUnit != null)
                draggedUnit.position = originalPosition;

            ClearDrag();
        }

        private void ClearDrag()
        {
            draggedUnit = null;

            if (gridLayout != null)
                gridLayout.SetHighlightTile(null);
        }

        private void OnDisable()
        {
            CancelDrag();
        }
    }
}