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
        private readonly List<ModifierSource> sources=new List<ModifierSource>();
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
            if(version!=unit.SpawnVersion) ClearSources();
            // 유지 중인 장비를 제거하지 않아 전투 중 최대 HP의 중간 감소 방지.
            foreach(ModifierSource source in sources.ToArray())
            {
                if(equipped.Any(item=>item.InstanceId==source.CasterInstanceId)) continue;
                stats.RemoveSource(source); sources.Remove(source);
            }
            Array.Clear(instances,0,instances.Length); Array.Clear(icons,0,icons.Length);
            foreach(OwnedItem item in equipped)
            {
                ItemDefinition definition=data.Items[item.DefinitionId];
                ModifierSource source=new ModifierSource(ModifierSourceKind.Equipment,definition.Id,item.InstanceId);
                if(!sources.Contains(source)) { stats.ReplaceSource(source,definition.Modifiers); sources.Add(source); }
                instances[item.Slot]=item.InstanceId; icons[item.Slot]=iconProvider(definition.IconId);
            }
            version=unit.SpawnVersion; Changed?.Invoke();
        }
        private void ClearSources()
        { if(stats!=null) foreach(ModifierSource source in sources) stats.RemoveSource(source); sources.Clear(); }
        public void Clear()
        {
            ClearSources(); version=-1;
            Array.Clear(instances,0,instances.Length); Array.Clear(icons,0,icons.Length); Changed?.Invoke();
        }
        private void OnDisable()=>Clear();
    }
}
