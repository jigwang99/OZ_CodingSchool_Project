using System;
using AutoBattler.Battle.Grid;

namespace AutoBattler.Battle.Placement
{
    /// <summary>영역, 슬롯, 최종 배치 인원을 검사한 뒤 이동 또는 교환.</summary>
    public sealed class PlacementService
    {
        public const int BenchCapacity = 8;
        private readonly BoardState board;
        private readonly int columns;
        private readonly int rows;
        private readonly int allyColumns;
        public int PlayerLevel { get; private set; }

        public PlacementService(BoardState board, int columns = 8, int rows = 7, int allyColumns = 4, int playerLevel = 1)
        {
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            if (columns <= 0 || rows <= 0 || allyColumns <= 0 || allyColumns > columns)
                throw new ArgumentOutOfRangeException(nameof(allyColumns));
            this.columns = columns;
            this.rows = rows;
            this.allyColumns = allyColumns;
            SetPlayerLevel(playerLevel);
        }

        public void SetPlayerLevel(int level)
        {
            if (level < 1 || level > 10) throw new ArgumentOutOfRangeException(nameof(level));
            PlayerLevel = level;
        }

        public bool IsInsideAllyArea(HexCoord cell)
        {
            cell.ToOffset(out int col, out int row);
            return col >= 0 && col < columns && col < allyColumns && row >= 0 && row < rows;
        }

        public bool IsValidLocation(UnitLocation location) => location.Kind == UnitLocationKind.Board
            ? IsInsideAllyArea(location.Cell)
            : location.Kind == UnitLocationKind.Bench && location.BenchSlot >= 0 && location.BenchSlot < BenchCapacity;

        public bool TryRegisterUnit(UnitState unit, UnitLocation location)
        {
            if (!IsValidLocation(location)
                || (location.Kind == UnitLocationKind.Board && board.DeployedCount >= PlayerLevel)) return false;
            return board.TryRegister(unit, location);
        }

        public int FindFreeBenchSlot()
        {
            for (int slot = 0; slot < BenchCapacity; slot++)
                if (board.GetBenchOccupant(slot) == null) return slot;
            return -1;
        }

        public bool CanMoveUnit(UnitState unit, HexCoord destination) => CanMoveUnit(unit, UnitLocation.OnBoard(destination));
        public bool CanMoveUnit(UnitState unit, UnitLocation destination)
        {
            if (!board.HasValidLocation(unit) || !IsValidLocation(destination)) return false;
            if (unit.Location.Kind != UnitLocationKind.Unplaced && !IsValidLocation(unit.Location)) return false;
            UnitState target = board.GetOccupant(destination);
            if (ReferenceEquals(target, unit)) return true;
            if (target != null)
            {
                // 교환 전후 전장 인원은 같지만 미배치 기물은 교환할 원래 슬롯이 없습니다.
                return unit.Location.Kind != UnitLocationKind.Unplaced && board.HasValidLocation(target)
                    && target.Location == destination && board.DeployedCount <= PlayerLevel;
            }

            int finalCount = board.DeployedCount
                - (unit.Location.Kind == UnitLocationKind.Board ? 1 : 0)
                + (destination.Kind == UnitLocationKind.Board ? 1 : 0);
            return finalCount <= PlayerLevel;
        }

        public PlacementResult TryMoveUnit(UnitState unit, HexCoord destination) => TryMoveUnit(unit, UnitLocation.OnBoard(destination));
        public PlacementResult TryMoveUnit(UnitState unit, UnitLocation destination)
        {
            if (!CanMoveUnit(unit, destination)) return new PlacementResult(false);
            UnitState target = board.GetOccupant(destination);
            if (target == null || ReferenceEquals(target, unit))
                return new PlacementResult(board.TryMove(unit, destination));
            return board.TrySwap(unit, target) ? new PlacementResult(true, target) : new PlacementResult(false);
        }
    }
}
