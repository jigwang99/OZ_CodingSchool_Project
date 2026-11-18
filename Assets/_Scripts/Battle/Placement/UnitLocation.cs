using System;
using AutoBattler.Battle.Grid;

namespace AutoBattler.Battle.Placement
{
    public enum UnitLocationKind { Unplaced, Board, Bench }

    /// <summary>전장 좌표와 대기석 슬롯을 서로 다른 위치로 구분.</summary>
    public readonly struct UnitLocation : IEquatable<UnitLocation>
    {
        public UnitLocationKind Kind { get; }
        public HexCoord Cell { get; }
        public int BenchSlot { get; }
        public static UnitLocation Unplaced => default;

        private UnitLocation(UnitLocationKind kind, HexCoord cell, int slot)
        {
            Kind = kind;
            Cell = cell;
            BenchSlot = slot;
        }

        public static UnitLocation OnBoard(HexCoord cell) => new UnitLocation(UnitLocationKind.Board, cell, -1);
        public static UnitLocation OnBench(int slot) => new UnitLocation(UnitLocationKind.Bench, default, slot);

        public bool Equals(UnitLocation other)
        {
            return Kind == other.Kind && (Kind == UnitLocationKind.Board ? Cell == other.Cell
                : Kind != UnitLocationKind.Bench || BenchSlot == other.BenchSlot);
        }

        public override bool Equals(object obj) => obj is UnitLocation other && Equals(other);
        public override int GetHashCode() => unchecked((int)Kind * 397 ^ (Kind == UnitLocationKind.Board
            ? Cell.GetHashCode() : Kind == UnitLocationKind.Bench ? BenchSlot : 0));
        public static bool operator ==(UnitLocation a, UnitLocation b) => a.Equals(b);
        public static bool operator !=(UnitLocation a, UnitLocation b) => !a.Equals(b);
    }
}
