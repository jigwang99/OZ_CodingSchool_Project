using System.Collections.Generic;
using AutoBattler.Battle.Grid;

namespace AutoBattler.Battle.Combat
{
    /// <summary>전투 점유와 다음 셀 예약. 이동 완료 전까지 두 셀 모두 유지.</summary>
    public sealed class CombatBoard
    {
        private readonly Dictionary<HexCoord,int> occupants=new Dictionary<HexCoord,int>();
        private readonly Dictionary<HexCoord,int> reservations=new Dictionary<HexCoord,int>();
        public int Revision { get; private set; }
        public static bool IsInside(HexCoord cell)
        {
            cell.ToOffset(out int col,out int row); return col>=0 && col<8 && row>=0 && row<7;
        }
        public bool IsFree(HexCoord cell,int self)
        {
            return IsInside(cell) && (!occupants.TryGetValue(cell,out int occupied) || occupied==self)
                && (!reservations.TryGetValue(cell,out int reserved) || reserved==self);
        }
        public bool TryAdd(int id,HexCoord cell)
        {
            if(!IsFree(cell,id)) return false;
            occupants[cell]=id; Revision++; return true;
        }
        public bool TryReserve(int id,HexCoord from,HexCoord to)
        {
            if(from.DistanceTo(to)!=1 || !occupants.TryGetValue(from,out int owner) || owner!=id || !IsFree(to,id)) return false;
            reservations[to]=id; Revision++; return true;
        }
        public bool TryArrive(int id,HexCoord from,HexCoord to)
        {
            if(!reservations.TryGetValue(to,out int owner) || owner!=id
                || !occupants.TryGetValue(from,out int current) || current!=id) return false;
            occupants.Remove(from); reservations.Remove(to); occupants[to]=id; Revision++; return true;
        }
        public void Release(int id)
        {
            var cells=new List<HexCoord>();
            foreach(var pair in occupants) if(pair.Value==id) cells.Add(pair.Key);
            foreach(var cell in cells) occupants.Remove(cell);
            cells.Clear(); foreach(var pair in reservations) if(pair.Value==id) cells.Add(pair.Key);
            foreach(var cell in cells) reservations.Remove(cell);
            Revision++;
        }
        public void CancelReservation(int id,HexCoord to)
        {
            if(reservations.TryGetValue(to,out int owner) && owner==id) { reservations.Remove(to); Revision++; }
        }
    }
}
