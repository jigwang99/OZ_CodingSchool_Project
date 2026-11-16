using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AutoBattler.Battle.Combat;
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
        private readonly Dictionary<int,int> effectStacks=new();
        private readonly List<(int InstanceId, ItemDefinition Definition)> equippedEffects=new();
        private ReadOnlyCollection<(int InstanceId, ItemDefinition Definition)> effectView;
        private Unit unit;
        private UnitStats stats;
        private int version=-1;
        public int Count=>instances.Count(id=>id!=0);
        internal IReadOnlyList<(int InstanceId, ItemDefinition Definition)> EquippedEffects=>effectView ??= equippedEffects.AsReadOnly();
        internal bool AllowsSkillCritical=>equippedEffects.Exists(item=>item.Definition.CombatEffect.Kind==ItemEffectKind.SkillCritical);
        public Sprite Icon(int slot)=>icons[slot];
        internal int ItemIdAt(int slot)=>instances[slot];
        public int EffectStacksAt(int slot)=>effectStacks.TryGetValue(instances[slot],out int count)?count:0;
        public event Action Changed;
        public event Action<int> EffectStacksChanged;
        public event Action<int> EffectActivated;
        private void Awake() { unit=GetComponent<Unit>(); stats=GetComponent<UnitStats>(); }

        internal void SetEffectStacks(int itemId,int count)
        {
            int slot=Array.IndexOf(instances,itemId);
            if(slot<0 || EffectStacksAt(slot)==count) return;
            if(count==0) effectStacks.Remove(itemId);
            else effectStacks[itemId]=count;
            CombatEventDispatcher.Publish(EffectStacksChanged,slot);
        }

        internal void NotifyEffectActivated(int itemId)
        {
            int slot=Array.IndexOf(instances,itemId);
            if(slot>=0) CombatEventDispatcher.Publish(EffectActivated,slot);
        }

        public void Apply(IEnumerable<OwnedItem> items,DataRepository data,Func<int,Sprite> iconProvider)
        {
            OwnedItem[] equipped=items.ToArray();
            int[] current=new int[ItemInventoryState.EquipmentCapacity];
            foreach(OwnedItem item in equipped) current[item.Slot]=item.InstanceId;
            if(version==unit.SpawnVersion && current.SequenceEqual(instances)) return;
            if(version!=unit.SpawnVersion) effectStacks.Clear();
            else foreach(int itemId in instances)
                if(itemId!=0 && Array.IndexOf(current,itemId)<0) effectStacks.Remove(itemId);
            int previousMana=Mathf.RoundToInt(stats.ManaBonus);
            Dictionary<ModifierSource,StatModifierSpec[]> replacements=new Dictionary<ModifierSource,StatModifierSpec[]>();
            Array.Clear(instances,0,instances.Length); Array.Clear(icons,0,icons.Length);
            equippedEffects.Clear();
            foreach(OwnedItem item in equipped)
            {
                ItemDefinition definition=data.Items[item.DefinitionId];
                ModifierSource source=new ModifierSource(ModifierSourceKind.Equipment,definition.Id,item.InstanceId);
                ItemCombatEffect effect=definition.CombatEffect;
                StatModifierSpec[] modifiers=effect.Kind switch
                {
                    ItemEffectKind.BonusArmor=>AppendStat(definition,StatType.Defense,ModifierOperation.Percent,effect.Value),
                    ItemEffectKind.BonusHealth=>AppendStat(definition,StatType.MaxHp,ModifierOperation.Percent,effect.Value),
                    ItemEffectKind.SkillCritical=>AppendStat(definition,StatType.CritChance,ModifierOperation.Flat,effect.Value),
                    _=>definition.Modifiers
                };
                replacements.Add(source,modifiers);
                if(effect.Kind!=ItemEffectKind.None) equippedEffects.Add((item.InstanceId,definition));
                instances[item.Slot]=item.InstanceId; icons[item.Slot]=iconProvider(definition.IconId);
            }
            stats.ReplaceSources(ModifierSourceKind.Equipment,replacements);
            if(unit.IsInCombat)
            {
                // 같은 장비 재적용은 지급하지 않고 합성·교체로 증가한 MP만 즉시 반영.
                int addedMana=Mathf.Max(0,Mathf.RoundToInt(stats.ManaBonus)-previousMana);
                if(addedMana>0) unit.Mana.Add(addedMana);
            }
            else if(unit.BattleFlow==null || unit.BattleFlow.CurrentPhase!=AutoBattler.Battle.Flow.BattlePhase.Result)
                unit.Mana.ResetForRound();
            version=unit.SpawnVersion; Changed?.Invoke();
        }
        private static StatModifierSpec[] AppendStat(ItemDefinition definition,StatType stat,ModifierOperation operation,float value)
            =>definition.Modifiers.Append(new StatModifierSpec(stat,operation,value)).ToArray();
        private void ClearSources()
        { if(stats!=null) stats.ReplaceSources(ModifierSourceKind.Equipment,new Dictionary<ModifierSource,StatModifierSpec[]>()); }
        public void Clear()
        {
            ClearSources(); version=-1;
            equippedEffects.Clear();
            effectStacks.Clear();
            Array.Clear(instances,0,instances.Length); Array.Clear(icons,0,icons.Length); Changed?.Invoke();
        }
        private void OnDisable()=>Clear();
    }
}
