using System;
using AutoBattler.Battle.Units;

namespace AutoBattler.Data.Items
{
    [Serializable]
    public sealed class ItemRow
    {
        public int itemId,iconId,dropWeight;
        public string name,description;
    }
    [Serializable]
    public sealed class ItemStatRow
    {
        public int entryId,itemId;
        public StatType stat;
        public ModifierOperation operation;
        public float value;
    }
    /// <summary>시트에서 읽는 공통 아이템 정의. 보유·장착 상태와 분리.</summary>
    public sealed class ItemDefinition
    {
        public int Id { get; }
        public int IconId { get; }
        public int DropWeight { get; }
        public string Name { get; }
        public string Description { get; }
        public StatModifierSpec[] Modifiers { get; }
        public ItemDefinition(ItemRow row,StatModifierSpec[] modifiers)
        { Id=row.itemId; IconId=row.iconId; DropWeight=row.dropWeight; Name=row.name; Description=row.description; Modifiers=modifiers; }
    }
}
