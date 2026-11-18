using System;

namespace AutoBattler.Battle.Grid
{
    /// <summary>Flat-top, Odd-q 육각 전장에서 사용하는 Axial 좌표.</summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public int Q { get; }
        public int R { get; }
        public int S => -Q - R;

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public static HexCoord FromOffset(int col, int row)
        {
            return new HexCoord(col, row - (col - (col & 1)) / 2);
        }

        public void ToOffset(out int col, out int row)
        {
            col = Q;
            row = R + (Q - (Q & 1)) / 2;
        }

        /// <summary>경로 탐색 결과가 일관되도록 고정된 방향 순서로 이웃 칸을 반환.</summary>
        public HexCoord GetNeighbor(int direction)
        {
            switch (direction)
            {
                case 0: return new HexCoord(Q + 1, R);
                case 1: return new HexCoord(Q + 1, R - 1);
                case 2: return new HexCoord(Q, R - 1);
                case 3: return new HexCoord(Q - 1, R);
                case 4: return new HexCoord(Q - 1, R + 1);
                case 5: return new HexCoord(Q, R + 1);
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction,
                        "Hex direction must be between 0 and 5.");
            }
        }

        public int DistanceTo(HexCoord other)
        {
            int dq = Q - other.Q;
            int dr = R - other.R;
            int ds = -dq - dr;
            return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(ds)) / 2;
        }

        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => unchecked((Q * 397) ^ R);
        public override string ToString() => $"({Q}, {R})";

        public static bool operator ==(HexCoord left, HexCoord right) => left.Equals(right);
        public static bool operator !=(HexCoord left, HexCoord right) => !left.Equals(right);
    }
}
