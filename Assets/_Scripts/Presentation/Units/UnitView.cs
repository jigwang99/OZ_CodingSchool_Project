using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using UnityEngine;

namespace AutoBattler.Presentation.Units
{
    /// <summary>전장 또는 대기석에 확정된 위치를 화면에 표현.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitView : MonoBehaviour
    {
        public UnitState State { get; private set; }
        private BoardState board;
        public void Bind(UnitState state, BoardState owner) { State = state; board = owner; }

        public void SnapToLocation(HexGridLayout grid, BenchLayout bench)
        {
            if (State == null || State.Location.Kind == UnitLocationKind.Unplaced) return;
            if (State.Location.Kind == UnitLocationKind.Bench && bench == null) return;
            Vector3 position = State.Location.Kind == UnitLocationKind.Board
                ? grid.CellToWorld(State.Location.Cell) : bench.SlotToWorld(State.Location.BenchSlot);
            position.z = transform.position.z;
            transform.position = position;
        }

        // 기존 전장 전용 호출도 계속 사용할 수 있습니다.
        public void SnapToCell(HexGridLayout layout) => SnapToLocation(layout, null);

        private void OnDisable()
        {
            if (board != null) board.TryUnregister(State);
            State = null;
            board = null;
        }
    }
}
