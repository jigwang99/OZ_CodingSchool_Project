using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler.Data.Units
{
    [CreateAssetMenu(fileName = "UnitData", menuName = "AutoBattler/Unit Data")]
    public sealed class UnitData : ScriptableObject
    {
        [SerializeField] private List<UnitStat> unitList = new List<UnitStat>();
        public IReadOnlyList<UnitStat> UnitList => unitList.AsReadOnly();

        public bool TryGetStat(UnitType type, out UnitStat stat)
        {
            stat = null;
            foreach (UnitStat entry in unitList)
            {
                if (entry == null || entry.UnitType != type) continue;
                // 중복 타입의 데이터를 임의로 선택하지 않음.
                if (stat != null) { stat = null; return false; }
                stat = entry;
            }
            return stat != null;
        }
    }
}
