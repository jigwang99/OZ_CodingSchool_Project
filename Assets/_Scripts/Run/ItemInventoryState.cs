using System;
using System.Collections.Generic;
using System.Linq;
using AutoBattler.Data.Items;

namespace AutoBattler.Run
{
    public sealed class OwnedItem
    {
        public int InstanceId { get; }
        public int DefinitionId { get; }
        public int StageId { get; }
        public int OwnerUnitId { get; internal set; }
        public int Slot { get; internal set; }=-1;
        public OwnedItem(int instanceId,int definitionId,int stageId)
        { InstanceId=instanceId; DefinitionId=definitionId; StageId=stageId; }
    }
    /// <summary>한 도전의 스테이지 전용 아이템과 승리 보상 난수·장착 위치 관리.</summary>
    public sealed class ItemInventoryState
    {
        public const int EquipmentCapacity=3;
        private readonly IReadOnlyDictionary<int,ItemDefinition> definitions;
        private readonly Dictionary<(int,int),ItemRecipeDefinition> recipes;
        private readonly List<OwnedItem> owned=new List<OwnedItem>();
        private readonly HashSet<int> rewardedRounds=new HashSet<int>();
        private RunRandom random;
        private int nextId=1;
        public int StageId { get; }
        public int LastRewardInstanceId { get; private set; }
        public IReadOnlyList<OwnedItem> All=>owned.AsReadOnly();
        public IEnumerable<OwnedItem> Stored=>owned.Where(item=>item.OwnerUnitId==0);
        public IEnumerable<OwnedItem> Equipped(int unitId)=>owned.Where(item=>unitId>0 && item.OwnerUnitId==unitId).OrderBy(item=>item.Slot);
        public OwnedItem Find(int instanceId)=>owned.FirstOrDefault(item=>item.InstanceId==instanceId);
        public ItemInventoryState(IReadOnlyDictionary<int,ItemDefinition> data,int stageId,int seed,IEnumerable<ItemRecipeDefinition> itemRecipes=null)
        {
            if(stageId<1) throw new ArgumentOutOfRangeException(nameof(stageId));
            definitions=data; StageId=stageId; random=new RunRandom(seed);
            recipes=(itemRecipes ?? Array.Empty<ItemRecipeDefinition>()).ToDictionary(recipe=>ItemRecipeDefinition.Key(recipe.FirstItemId,recipe.SecondItemId));
        }
        public OwnedItem GrantVictory(int round)
        {
            if(round<1 || !rewardedRounds.Add(round)) return null;
            LastRewardInstanceId=0;
            int total=definitions.Values.Sum(item=>item.DropWeight);
            if(total==0) return null;
            int pick=random.Next(total);
            foreach(ItemDefinition definition in definitions.Values.OrderBy(item=>item.Id))
            {
                pick-=definition.DropWeight;
                if(pick>=0) continue;
                OwnedItem reward=new OwnedItem(nextId++,definition.Id,StageId);
                owned.Add(reward); LastRewardInstanceId=reward.InstanceId; return reward;
            }
            return null;
        }
        public void ClearResult()=>LastRewardInstanceId=0;
        private bool IsStored(OwnedItem item)=>item!=null && item.StageId==StageId && item.OwnerUnitId==0;
        private ItemDefinition RecipeResult(OwnedItem first,OwnedItem second)
        {
            if(first==null || second==null || first.InstanceId==second.InstanceId || first.StageId!=StageId || second.StageId!=StageId) return null;
            return recipes.TryGetValue(ItemRecipeDefinition.Key(first.DefinitionId,second.DefinitionId),out ItemRecipeDefinition recipe)
                && definitions.TryGetValue(recipe.ResultItemId,out ItemDefinition result)?result:null;
        }
        public ItemDefinition FusionResult(int firstId,int secondId)
        {
            OwnedItem first=Find(firstId),second=Find(secondId);
            return IsStored(first) && IsStored(second)?RecipeResult(first,second):null;
        }
        public bool TryGetEquipFusion(int itemId,int unitId,out OwnedItem ingredient,out ItemDefinition result)
        {
            ingredient=null; result=null;
            OwnedItem incoming=Find(itemId);
            if(!IsStored(incoming) || unitId<=0) return false;
            foreach(OwnedItem equipped in Equipped(unitId))
            {
                ItemDefinition candidate=RecipeResult(incoming,equipped);
                if(candidate==null) continue;
                ingredient=equipped; result=candidate; return true;
            }
            return false;
        }
        public bool CanEquip(int itemId,int unitId)=>IsStored(Find(itemId)) && unitId>0
            && (TryGetEquipFusion(itemId,unitId,out _,out _) || Equipped(unitId).Count()<EquipmentCapacity);
        private OwnedItem Fuse(OwnedItem first,OwnedItem second,ItemDefinition definition,int unitId=0,int slot=-1)
        {
            OwnedItem result=new OwnedItem(nextId++,definition.Id,StageId) { OwnerUnitId=unitId,Slot=slot };
            owned.Remove(first); owned.Remove(second); owned.Add(result);
            if(LastRewardInstanceId==first.InstanceId || LastRewardInstanceId==second.InstanceId) LastRewardInstanceId=result.InstanceId;
            return result;
        }
        public bool TryFuse(int firstId,int secondId,out OwnedItem result)
        {
            result=null;
            ItemDefinition definition=FusionResult(firstId,secondId);
            if(definition==null) return false;
            result=Fuse(Find(firstId),Find(secondId),definition); return true;
        }
        public bool TryEquip(int itemId,int unitId)
        {
            if(TryGetEquipFusion(itemId,unitId,out OwnedItem ingredient,out ItemDefinition result))
            { Fuse(Find(itemId),ingredient,result,unitId,ingredient.Slot); return true; }
            return EquipEmptySlot(itemId,unitId);
        }
        private bool EquipEmptySlot(int itemId,int unitId)
        {
            OwnedItem item=Find(itemId);
            if(!IsStored(item) || unitId<=0) return false;
            int slot=Enumerable.Range(0,EquipmentCapacity).Where(index=>!Equipped(unitId).Any(current=>current.Slot==index)).DefaultIfEmpty(-1).First();
            if(slot<0) return false;
            item.OwnerUnitId=unitId; item.Slot=slot; return true;
        }
        public bool TryUnequip(int itemId)
        {
            OwnedItem item=Find(itemId);
            if(item==null || item.StageId!=StageId || item.OwnerUnitId==0) return false;
            item.OwnerUnitId=0; item.Slot=-1; return true;
        }
        public void ReturnFromUnit(int unitId)
        { foreach(OwnedItem item in Equipped(unitId).ToArray()) TryUnequip(item.InstanceId); }
        public void MergeUnits(int keepId,int removedId)
        {
            foreach(OwnedItem item in Equipped(removedId).ToArray())
            { TryUnequip(item.InstanceId); EquipEmptySlot(item.InstanceId,keepId); }
        }
        public SavedItemInventory Capture()=>new SavedItemInventory {
            stageId=StageId,nextInstanceId=nextId,randomSeed=random.Seed,randomDrawCount=random.DrawCount,
            lastRewardInstanceId=LastRewardInstanceId,rewardedRounds=rewardedRounds.OrderBy(round=>round).ToArray(),
            owned=owned.Select(item=>new SavedItem {instanceId=item.InstanceId,definitionId=item.DefinitionId,
                stageId=item.StageId,ownerUnitId=item.OwnerUnitId,slot=item.Slot}).ToArray()
        };
        public static ItemInventoryState Restore(IReadOnlyDictionary<int,ItemDefinition> definitions,SavedItemInventory saved,
            int stageId,int seed,IReadOnlyCollection<int> unitIds,int round,IEnumerable<ItemRecipeDefinition> recipes=null)
        {
            ItemInventoryState result=new ItemInventoryState(definitions,stageId,seed,recipes);
            // 아이템 기능 추가 전의 저장은 빈 인벤토리로 이어하기.
            if(saved==null) return result;
            if(saved.stageId!=stageId || saved.randomSeed!=seed || saved.nextInstanceId<1 || saved.owned==null
                || saved.rewardedRounds==null || saved.randomDrawCount<0 || saved.owned.Length>round
                || saved.owned.Length>saved.rewardedRounds.Length
                || saved.rewardedRounds.Any(value=>value<1 || value>round)
                || saved.rewardedRounds.Distinct().Count()!=saved.rewardedRounds.Length)
                throw new ArgumentException("저장된 스테이지 아이템 정보가 올바르지 않습니다.");
            foreach(SavedItem item in saved.owned)
            {
                if(item==null || item.instanceId<1 || item.instanceId>=saved.nextInstanceId || item.stageId!=stageId
                    || !definitions.ContainsKey(item.definitionId) || result.Find(item.instanceId)!=null
                    || item.ownerUnitId==0 && item.slot!=-1 || item.ownerUnitId!=0 && (!unitIds.Contains(item.ownerUnitId)
                        || item.slot<0 || item.slot>=EquipmentCapacity || result.Equipped(item.ownerUnitId).Any(current=>current.Slot==item.slot)))
                    throw new ArgumentException("저장된 아이템의 소유자·장착 위치가 올바르지 않습니다.");
                result.owned.Add(new OwnedItem(item.instanceId,item.definitionId,item.stageId) { OwnerUnitId=item.ownerUnitId,Slot=item.slot });
            }
            if(saved.lastRewardInstanceId!=0 && result.Find(saved.lastRewardInstanceId)==null)
                throw new ArgumentException("저장된 아이템 보상 참조가 올바르지 않습니다.");
            result.nextId=saved.nextInstanceId; result.LastRewardInstanceId=saved.lastRewardInstanceId;
            result.rewardedRounds.UnionWith(saved.rewardedRounds);
            result.random=new RunRandom(seed,saved.randomDrawCount);
            return result;
        }
    }
}
