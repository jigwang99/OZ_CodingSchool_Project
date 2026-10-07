using System;
using AutoBattler.Battle.Grid;

namespace AutoBattler.Battle.Placement
{
    /// <summary>기물의 고유 ID와 확정된 전장 또는 대기석 위치입니다.</summary>
    public sealed class UnitState
    {
        public int InstanceId { get; }
        public UnitLocation Location { get; internal set; }
        public HexCoord? Cell => Location.Kind == UnitLocationKind.Board ? (HexCoord?)Location.Cell : null;

        public UnitState(int instanceId)
        {
            if (instanceId <= 0) throw new ArgumentOutOfRangeException(nameof(instanceId));
            InstanceId = instanceId;
        }
    }
}
