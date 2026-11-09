using AutoBattler.Data.Items;
using AutoBattler.Battle.Units;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AutoBattler.Data.Skills;
using AutoBattler.Data.Units;
using AutoBattler.Data.Traits;
using AutoBattler.Data.Stages;
using AutoBattler.Data.Augments;

namespace AutoBattler.Data.Snapshot
{
    [Serializable] public sealed class GameSnapshot
    {
        public int schemaVersion;
        public string dataVersion;
        public string previousDataVersion;
        public UnitRow[] units;
        public RoundRow[] rounds;
        public RoundUnitRow[] roundUnits;
        public LevelRow[] levels;
        public ShopRateRow[] shopRates;
        public PoolRow[] poolCounts;
        public EconomyRow[] economy;
        public SkillRow[] skills;
        public SkillRuleRow[] skillRules;
        public EffectRow[] effects;
        public TraitRow[] traits;
        public AugmentRow[] augments;
        public ItemRow[] items;
        public ItemStatRow[] itemStats;
        public ItemRecipeRow[] itemRecipes;
        public StageRow[] stages;
        public StageUnlockPlanRow[] stageUnlockPlans;
    }
    [Serializable] public sealed class UnitRow
    {
        public int unitId, cost, originId, classId, hp, attack, armor, attackRange, combatRole, skillId, visualId, iconId;
        public string name, attackType;
        public float attackInterval, critChance, critMultiplier;
    }
    [Serializable] public sealed class RoundRow { public int roundId, rewardGold,stageId,sequence; public bool isBoss, offersAugment; }
    [Serializable] public sealed class RoundUnitRow { public int entryId, roundId, unitId, star, col, row, bossProfileId; }
    [Serializable] public sealed class LevelRow { public int level, requiredXp, deploymentLimit; }
    [Serializable] public sealed class ShopRateRow { public int level; public int[] weights; }
    [Serializable] public sealed class PoolRow { public int cost, copiesPerUnit; }
    [Serializable] public sealed class EconomyRow { public int ruleId; public float value; }

    public enum EconomyRule { StartGold=1, StartLives, RefreshCost, XpCost, PurchasedXp, RoundXp, InterestDivisor, InterestCap, CombatSeconds, MoveSeconds, WindupSeconds, ProjectileSpeed, StarMultiplier }
    public sealed class UnitDefinition
    {
        public int Id { get; } public string Name { get; } public int Cost { get; }
        public int OriginId { get; } public int ClassId { get; } public int Hp { get; } public int Attack { get; }
        public int Armor { get; } public int Range { get; } public int Role { get; }
        public int VisualId { get; } public int IconId { get; } public int SkillId { get; }
        public float Interval { get; } public float CritChance { get; } public float CritMultiplier { get; }
        public bool IsRanged { get; }
        public UnitDefinition(UnitRow row)
        {
            Id=row.unitId; Name=row.name; Cost=row.cost; OriginId=row.originId; ClassId=row.classId;
            Hp=row.hp; Attack=row.attack; Armor=row.armor; Range=row.attackRange; Role=row.combatRole;
            VisualId=row.visualId; IconId=row.iconId; SkillId=row.skillId; Interval=row.attackInterval;
            CritChance=row.critChance; CritMultiplier=row.critMultiplier; IsRanged=row.attackType=="Ranged";
        }
    }
    public readonly struct EnemyDefinition
    {
        public readonly int UnitId, Star, Col, Row, BossProfileId;
        public EnemyDefinition(RoundUnitRow row) { UnitId=row.unitId; Star=row.star; Col=row.col; Row=row.row; BossProfileId=row.bossProfileId; }
    }
    public sealed class RoundDefinition
    {
        public int Id { get; } public int Reward { get; } public bool IsBoss { get; }
        public bool OffersAugment { get; } public IReadOnlyList<EnemyDefinition> Enemies { get; }
        public RoundDefinition(RoundRow row, IEnumerable<RoundUnitRow> enemies)
        {
            Id=row.sequence>0?row.sequence:row.roundId; Reward=row.rewardGold; IsBoss=row.isBoss; OffersAugment=row.offersAugment;
            Enemies=Array.AsReadOnly(enemies.OrderBy(x=>x.entryId).Select(x=>new EnemyDefinition(x)).ToArray());
        }
    }
    /// <summary>도전 시작 시 확정한 스냅샷을 정수 ID로 조회.</summary>
    public sealed class DataRepository
    {
        public string Version { get; }
        private readonly HashSet<string> previousVersions;
        private readonly GameSnapshot source;
        public int StageId { get; }
        public int RoundsPerSegment { get; }
        public IReadOnlyDictionary<int,StageRule> Stages { get; }
        public bool IsSaveCompatible(string version)=>version==Version || version!=null && previousVersions.Contains(version);
        public IReadOnlyDictionary<int, UnitDefinition> Units { get; }
        public IReadOnlyDictionary<int, RoundDefinition> Rounds { get; }
        public IReadOnlyDictionary<int,SkillDefinition> Skills { get; }
        public IReadOnlyDictionary<int,EffectDefinition> Effects { get; }
        public IReadOnlyList<TraitDefinition> Traits { get; }
        public IReadOnlyDictionary<int,AugmentDefinition> Augments { get; }
        public IReadOnlyDictionary<int,ItemDefinition> Items { get; }
        public IReadOnlyList<ItemRecipeDefinition> ItemRecipes { get; }
        private readonly Dictionary<int,int> levelXp, pool;
        private readonly Dictionary<int,int[]> rates;
        private readonly Dictionary<int,float> rules;
        public DataRepository(GameSnapshot data,int stageId=1)
        {
            if(data==null || data.schemaVersion<1 || data.schemaVersion>8) throw new ArgumentException("지원하지 않는 데이터 스키마.");
            source=data; StageId=stageId;
            Version=data.dataVersion;
            // 이전에 지원하던 저장 버전도 유지. Manifest에서 세미콜론으로 구분.
            previousVersions=new HashSet<string>((data.previousDataVersion ?? string.Empty).Split(';')
                .Select(version=>version.Trim()).Where(version=>version.Length>0));
            if(data.schemaVersion>=5 && (data.items==null || data.itemStats==null))
                throw new ArgumentException("아이템 정의 또는 능력치 표 없음.");
            ItemRow[] itemRows=data.items ?? Array.Empty<ItemRow>();
            ItemStatRow[] itemStats=data.itemStats ?? Array.Empty<ItemStatRow>();
            if(itemRows.Any(item=>item==null || item.itemId<=0 || item.iconId<=0 || item.dropWeight<0 || string.IsNullOrWhiteSpace(item.name))
                || itemRows.Sum(item=>(long)item.dropWeight)>int.MaxValue
                || itemStats.Any(stat=>stat==null || stat.entryId<=0 || !itemRows.Any(item=>item.itemId==stat.itemId) || !Enum.IsDefined(typeof(StatType),stat.stat)
                    || !Enum.IsDefined(typeof(ModifierOperation),stat.operation) || float.IsNaN(stat.value) || float.IsInfinity(stat.value)
                    || stat.stat==StatType.Mana && (stat.operation!=ModifierOperation.Flat || stat.value<0 || stat.value>100 || stat.value!=(int)stat.value)))
                throw new ArgumentException("아이템 정의 또는 능력치 참조 오류.");
            Items=new ReadOnlyDictionary<int,ItemDefinition>(itemRows.ToDictionary(item=>item.itemId,item=>new ItemDefinition(item,
                itemStats.Where(stat=>stat.itemId==item.itemId).OrderBy(stat=>stat.entryId)
                    .Select(stat=>new StatModifierSpec(stat.stat,stat.operation,stat.value)).ToArray())));
            ItemRecipeRow[] recipeRows=data.schemaVersion>=7
                ? data.itemRecipes ?? throw new ArgumentException("아이템 합성 조합표 없음.") : Array.Empty<ItemRecipeRow>();
            HashSet<int> recipeIds=new HashSet<int>();
            HashSet<(int,int)> recipePairs=new HashSet<(int,int)>();
            foreach(ItemRecipeRow recipe in recipeRows)
                if(recipe==null || recipe.recipeId<=0 || !recipeIds.Add(recipe.recipeId)
                    || !Items.ContainsKey(recipe.firstItemId) || !Items.ContainsKey(recipe.secondItemId) || !Items.ContainsKey(recipe.resultItemId)
                    || recipe.resultItemId==recipe.firstItemId || recipe.resultItemId==recipe.secondItemId
                    || !recipePairs.Add(ItemRecipeDefinition.Key(recipe.firstItemId,recipe.secondItemId)))
                    throw new ArgumentException("아이템 합성 조합 또는 참조 오류.");
            ItemRecipes=Array.AsReadOnly(recipeRows.OrderBy(recipe=>recipe.recipeId).Select(recipe=>new ItemRecipeDefinition(recipe)).ToArray());
            Augments=new ReadOnlyDictionary<int,AugmentDefinition>((data.schemaVersion>=4
                ? data.augments ?? throw new ArgumentException("강화 정의 없음.") : Array.Empty<AugmentRow>())
                .ToDictionary(row=>row.augmentId,row=>new AugmentDefinition(row)));
            Units=new ReadOnlyDictionary<int,UnitDefinition>(data.units.ToDictionary(x=>x.unitId,x=>new UnitDefinition(x)));
            StageRow[] stageRows=data.schemaVersion>=8?data.stages ?? throw new ArgumentException("스테이지 정의 없음."):Array.Empty<StageRow>();
            StageUnlockPlanRow[] plans=data.stageUnlockPlans ?? Array.Empty<StageUnlockPlanRow>();
            if(plans.Any(plan=>!stageRows.Any(stage=>stage.stageId==plan.stageId) || !Units.ContainsKey(plan.unitId))
                || plans.Select(plan=>plan.unitId).Distinct().Count()!=plans.Length)
                throw new ArgumentException("스테이지 유닛 해금 계획 참조 또는 중복 오류.");
            Stages=new ReadOnlyDictionary<int,StageRule>(stageRows.ToDictionary(stage=>stage.stageId,
                stage=>new StageRule(stage,plans.Where(plan=>plan.stageId==stage.stageId).Select(plan=>plan.unitId).ToArray())));
            IEnumerable<RoundRow> selected=data.rounds;
            if(data.schemaVersion>=8)
            {
                if(!Stages.TryGetValue(stageId,out StageRule stage) || stage.RoundsPerSegment<2
                    || string.IsNullOrWhiteSpace(stage.LocationName)) throw new ArgumentException("선택 스테이지 정의 오류.");
                RoundsPerSegment=stage.RoundsPerSegment;
                selected=selected.Where(round=>round.stageId==stageId);
            }
            else RoundsPerSegment=data.rounds.Length/3;
            Dictionary<int,RoundUnitRow[]> encounters=data.roundUnits.GroupBy(enemy=>enemy.roundId).ToDictionary(group=>group.Key,group=>group.ToArray());
            Rounds=new ReadOnlyDictionary<int,RoundDefinition>(selected.ToDictionary(row=>row.sequence>0?row.sequence:row.roundId,
                row=>new RoundDefinition(row,encounters.TryGetValue(row.roundId,out RoundUnitRow[] enemies)?enemies:Array.Empty<RoundUnitRow>())));
            if(data.schemaVersion>=8 && (Rounds.Count!=RoundsPerSegment*3
                || Rounds.Keys.OrderBy(id=>id).Select((id,index)=>id!=index+1).Any(value=>value)
                || Rounds.Values.Any(round=>round.Enemies.Count==0 || round.IsBoss!=(round.Id%RoundsPerSegment==0)
                    || round.OffersAugment!=(round.Id==2 || round.Id==RoundsPerSegment+1 || round.Id==RoundsPerSegment*2+1))))
                throw new ArgumentException("스테이지 전투 순서·보스·증강 일정 오류.");
            Dictionary<int,SkillRuleRow> skillRules=(data.skillRules ?? Array.Empty<SkillRuleRow>()).ToDictionary(rule=>rule.skillId);
            Skills=new ReadOnlyDictionary<int,SkillDefinition>((data.skills ?? Array.Empty<SkillRow>()).ToDictionary(x=>x.skillId,
                x=>new SkillDefinition(x,skillRules.TryGetValue(x.skillId,out SkillRuleRow rule)?rule:null)));
            Effects=new ReadOnlyDictionary<int,EffectDefinition>((data.effects ?? Array.Empty<EffectRow>()).ToDictionary(x=>x.effectId,x=>x.Create()));
            Traits=Array.AsReadOnly(data.schemaVersion>=3
                ? (data.traits ?? throw new ArgumentException("시너지 정의 없음.")).OrderBy(row=>row.traitId).Select(row=>new TraitDefinition(row)).ToArray()
                : Array.Empty<TraitDefinition>());
            if(data.units.Any(unit=>unit.skillId!=0 && !Skills.ContainsKey(unit.skillId))
                || Skills.Values.Any(skill=>skill.EffectId!=0 && !Effects.ContainsKey(skill.EffectId)
                    || skill.SecondaryEffectId!=0 && !Effects.ContainsKey(skill.SecondaryEffectId))
                || skillRules.Keys.Any(id=>!Skills.ContainsKey(id)))
                throw new ArgumentException("존재하지 않는 스킬·효과 ID 참조.");
            levelXp=data.levels.ToDictionary(x=>x.level,x=>x.requiredXp);
            pool=data.poolCounts.ToDictionary(x=>x.cost,x=>x.copiesPerUnit);
            rates=data.shopRates.ToDictionary(x=>x.level,x=>(int[])x.weights.Clone());
            rules=data.economy.ToDictionary(x=>x.ruleId,x=>x.value);
        }
        /// <summary>스테이지 해금 목록에 없는 유닛은 기본 사용 가능.</summary>
        public IReadOnlyList<int> InitialUnlockedUnitIds=>Array.AsReadOnly(Units.Keys
            .Except(Stages.Values.SelectMany(stage=>stage.PlannedUnlockUnitIds)).OrderBy(id=>id).ToArray());
        public int RequiredXp(int level)=>levelXp[level];
        public int PoolCopies(int cost)=>pool[cost];
        public IReadOnlyList<int> ShopWeights(int level)=>Array.AsReadOnly(rates[level]);
        public float Rule(EconomyRule rule)=>rules[(int)rule];
        public int IntRule(EconomyRule rule)=>(int)Rule(rule);
        public DataRepository ForStage(int stageId)=>source.schemaVersion<8 || stageId==StageId?this:new DataRepository(source,stageId);
    }
}
