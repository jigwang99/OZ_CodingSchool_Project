using System;
using System.Collections.Generic;
using System.Linq;
using AutoBattler.Data.Items;
using AutoBattler.Data.Snapshot;
using AutoBattler.Run;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    /// <summary>장착 아이템의 영구 능력치와 표시용 아이콘 관리.</summary>
    [DisallowMultipleComponent,RequireComponent(typeof(UnitStats))]
    public sealed class UnitEquipment : MonoBehaviour
    {
        private readonly int[] instances=new int[ItemInventoryState.EquipmentCapacity];
        private readonly Sprite[] icons=new Sprite[ItemInventoryState.EquipmentCapacity];
        private Unit unit;
        private UnitStats stats;
        private int version=-1;
        public int Count=>instances.Count(id=>id!=0);
        public Sprite Icon(int slot)=>icons[slot];
        public event Action Changed;
        private void Awake() { unit=GetComponent<Unit>(); stats=GetComponent<UnitStats>(); }
        public void Apply(IEnumerable<OwnedItem> items,DataRepository data,Func<int,Sprite> iconProvider)
        {
            OwnedItem[] equipped=items.ToArray();
            int[] current=new int[ItemInventoryState.EquipmentCapacity];
            foreach(OwnedItem item in equipped) current[item.Slot]=item.InstanceId;
            if(version==unit.SpawnVersion && current.SequenceEqual(instances)) return;
            int previousMana=Mathf.RoundToInt(stats.ManaBonus);
            Dictionary<ModifierSource,StatModifierSpec[]> replacements=new Dictionary<ModifierSource,StatModifierSpec[]>();
            Array.Clear(instances,0,instances.Length); Array.Clear(icons,0,icons.Length);
            foreach(OwnedItem item in equipped)
            {
                ItemDefinition definition=data.Items[item.DefinitionId];
                ModifierSource source=new ModifierSource(ModifierSourceKind.Equipment,definition.Id,item.InstanceId);
                replacements.Add(source,definition.Modifiers);
                instances[item.Slot]=item.InstanceId; icons[item.Slot]=iconProvider(definition.IconId);
            }
            stats.ReplaceSources(ModifierSourceKind.Equipment,replacements);
            if(unit.IsInCombat)
            {
                // 같은 장비 재적용은 지급하지 않고 합성·교체로 증가한 MP만 즉시 반영.
                int addedMana=Mathf.Max(0,Mathf.RoundToInt(stats.ManaBonus)-previousMana);
                if(addedMana>0) unit.Mana.Add(addedMana);
            }
            else unit.Mana.ResetForRound();
            version=unit.SpawnVersion; Changed?.Invoke();
        }
        private void ClearSources()
        { if(stats!=null) stats.ReplaceSources(ModifierSourceKind.Equipment,new Dictionary<ModifierSource,StatModifierSpec[]>()); }
        public void Clear()
        {
            ClearSources(); version=-1;
            Array.Clear(instances,0,instances.Length); Array.Clear(icons,0,icons.Length); Changed?.Invoke();
        }
        private void OnDisable()=>Clear();
    }
}
