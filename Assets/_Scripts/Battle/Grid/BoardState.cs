using System.Collections.Generic;
using AutoBattler.Battle.Placement;

namespace AutoBattler.Battle.Grid
{
    /// <summary>전장과 대기석의 점유를 하나의 위치 체계로 관리.</summary>
    public sealed class BoardState
    {
        private readonly Dictionary<int, UnitState> units = new Dictionary<int, UnitState>();
        private readonly Dictionary<UnitLocation, UnitState> occupants = new Dictionary<UnitLocation, UnitState>();

        public int DeployedCount
        {
            get
            {
                int count = 0;
                foreach (UnitLocation location in occupants.Keys)
                    if (location.Kind == UnitLocationKind.Board) count++;
                return count;
            }
        }

        public UnitState GetOccupant(HexCoord cell) => GetOccupant(UnitLocation.OnBoard(cell));
        public UnitState GetBenchOccupant(int slot) => GetOccupant(UnitLocation.OnBench(slot));
        public UnitState GetOccupant(UnitLocation location)
        {
            occupants.TryGetValue(location, out UnitState unit);
            return unit;
        }

        public bool IsRegistered(UnitState unit) => unit != null
            && units.TryGetValue(unit.InstanceId, out UnitState stored) && ReferenceEquals(stored, unit);

        public bool HasValidLocation(UnitState unit) => IsRegistered(unit)
            && (unit.Location.Kind == UnitLocationKind.Unplaced || ReferenceEquals(GetOccupant(unit.Location), unit));

        public bool TryRegister(UnitState unit, HexCoord? cell) => TryRegister(unit,
            cell.HasValue ? UnitLocation.OnBoard(cell.Value) : UnitLocation.Unplaced);

        internal bool TryRegister(UnitState unit, UnitLocation location)
        {
            if (unit == null || units.ContainsKey(unit.InstanceId)
                || unit.Location.Kind != UnitLocationKind.Unplaced || GetOccupant(location) != null)
                return false;
            units.Add(unit.InstanceId, unit);
            if (location.Kind != UnitLocationKind.Unplaced) occupants.Add(location, unit);
            unit.Location = location;
            return true;
        }

        internal bool TryMove(UnitState unit, UnitLocation destination)
        {
            if (!HasValidLocation(unit) || destination.Kind == UnitLocationKind.Unplaced) return false;
            UnitState target = GetOccupant(destination);
            if (target != null && !ReferenceEquals(target, unit)) return false;
            if (unit.Location.Kind != UnitLocationKind.Unplaced) occupants.Remove(unit.Location);
            occupants[destination] = unit;
            unit.Location = destination;
            return true;
        }

        internal bool TrySwap(UnitState unit, UnitState target)
        {
            if (ReferenceEquals(unit, target) || !HasValidLocation(unit) || !HasValidLocation(target)
                || unit.Location.Kind == UnitLocationKind.Unplaced || target.Location.Kind == UnitLocationKind.Unplaced)
                return false;
            UnitLocation original = unit.Location;
            UnitLocation destination = target.Location;
            occupants[original] = target;
            occupants[destination] = unit;
            unit.Location = destination;
            target.Location = original;
            return true;
        }

        public bool TryUnregister(UnitState unit)
        {
            if (!IsRegistered(unit)) return false;
            if (ReferenceEquals(GetOccupant(unit.Location), unit)) occupants.Remove(unit.Location);
            units.Remove(unit.InstanceId);
            unit.Location = UnitLocation.Unplaced;
            return true;
        }
    }
}
