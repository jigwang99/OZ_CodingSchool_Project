using System;

namespace AutoBattler.Data.Items
{
    [Serializable]
    public sealed class ItemRecipeRow
    {
        public int recipeId,firstItemId,secondItemId,resultItemId;
    }

    /// <summary>재료 순서와 관계없이 같은 상위 아이템으로 합성하는 조합.</summary>
    public sealed class ItemRecipeDefinition
    {
        public int Id { get; }
        public int FirstItemId { get; }
        public int SecondItemId { get; }
        public int ResultItemId { get; }
        public ItemRecipeDefinition(ItemRecipeRow row)
        { Id=row.recipeId; FirstItemId=row.firstItemId; SecondItemId=row.secondItemId; ResultItemId=row.resultItemId; }
        public static (int,int) Key(int first,int second)=>first<=second?(first,second):(second,first);
    }
}
