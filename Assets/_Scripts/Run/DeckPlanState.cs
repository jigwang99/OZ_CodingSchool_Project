using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Traits;
using AutoBattler.Data.Snapshot;

namespace AutoBattler.Run
{
    /// <summary>실제 보유 유닛·경제와 분리된 덱 구상용 배치와 시너지 집계.</summary>
    public sealed class DeckPlanState
    {
        public const int Capacity=10;
        private readonly DataRepository data;
        private readonly Dictionary<HexCoord,int> units=new Dictionary<HexCoord,int>();
        public IReadOnlyDictionary<HexCoord,int> Units { get; }
        public TraitSnapshot Synergies { get; private set; }
        public int Count=>units.Count;
        public event Action Changed;

        public DeckPlanState(DataRepository data)
        {
            this.data=data;
            Units=new ReadOnlyDictionary<HexCoord,int>(units);
            Synergies=TraitCounter.Count(Array.Empty<UnitDefinition>(),data.Traits);
        }
        public int UnitAt(HexCoord cell)=>units.TryGetValue(cell,out int id)?id:0;
        private static bool IsInside(HexCoord cell)
        {
            cell.ToOffset(out int col,out int row);
            return col>=0 && col<HexGridLayout.AllyColumns && row>=0 && row<HexGridLayout.Rows;
        }
        public bool TryPlace(int unitId,HexCoord cell)
        {
            if(!data.Units.ContainsKey(unitId) || !IsInside(cell) || !units.ContainsKey(cell) && Count>=Capacity) return false;
            if(UnitAt(cell)==unitId) return true;
            units[cell]=unitId; Notify(); return true;
        }
        public bool TryMove(HexCoord from,HexCoord to)
        {
            if(!IsInside(to) || !units.TryGetValue(from,out int unitId)) return false;
            if(from==to) return true;
            if(units.TryGetValue(to,out int other)) units[from]=other;
            else units.Remove(from);
            units[to]=unitId; Notify(); return true;
        }
        public bool Remove(HexCoord cell)
        {
            if(!units.Remove(cell)) return false;
            Notify(); return true;
        }
        public void Clear()
        {
            if(Count==0) return;
            units.Clear(); Notify();
        }
        public void Load(IEnumerable<KeyValuePair<HexCoord,int>> deployed)
        {
            KeyValuePair<HexCoord,int>[] copy=deployed.ToArray();
            units.Clear();
            foreach(KeyValuePair<HexCoord,int> entry in copy)
            {
                if(!IsInside(entry.Key) || !data.Units.ContainsKey(entry.Value)) continue;
                if(!units.ContainsKey(entry.Key) && Count>=Capacity) break;
                units[entry.Key]=entry.Value;
            }
            Notify();
        }
        private void Notify()
        {
            Synergies=TraitCounter.Count(units.Values.Select(id=>data.Units[id]),data.Traits);
            Changed?.Invoke();
        }
    }
}
