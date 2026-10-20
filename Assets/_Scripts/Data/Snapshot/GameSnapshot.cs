using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AutoBattler.Data.Skills;
using AutoBattler.Data.Units;
using AutoBattler.Data.Traits;
using AutoBattler.Data.Augments;

namespace AutoBattler.Data.Snapshot
{
    [Serializable] public sealed class GameSnapshot
    {
        public int schemaVersion;
        public string dataVersion;
        public UnitRow[] units;
        public RoundRow[] rounds;
        public RoundUnitRow[] roundUnits;
        public LevelRow[] levels;
        public ShopRateRow[] shopRates;
        public PoolRow[] poolCounts;
        public EconomyRow[] economy;
        public SkillRow[] skills;
        public EffectRow[] effects;
        public TraitRow[] traits;
        public AugmentRow[] augments;
    }
    [Serializable] public sealed class UnitRow
    {
        public int unitId, cost, originId, classId, hp, attack, armor, attackRange, combatRole, skillId, visualId, iconId;
        public string name, attackType;
        public float attackInterval, critChance, critMultiplier;
    }
    [Serializable] public sealed class RoundRow { public int roundId, rewardGold; public bool isBoss, offersAugment; }
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
            Id=row.roundId; Reward=row.rewardGold; IsBoss=row.isBoss; OffersAugment=row.offersAugment;
            Enemies=Array.AsReadOnly(enemies.OrderBy(x=>x.entryId).Select(x=>new EnemyDefinition(x)).ToArray());
        }
    }
    /// <summary>도전 시작 시 확정한 스냅샷을 정수 ID로 조회.</summary>
    public sealed class DataRepository
    {
        public string Version { get; }
        public IReadOnlyDictionary<int, UnitDefinition> Units { get; }
        public IReadOnlyDictionary<int, RoundDefinition> Rounds { get; }
        public IReadOnlyDictionary<int,SkillDefinition> Skills { get; }
        public IReadOnlyDictionary<int,EffectDefinition> Effects { get; }
        public IReadOnlyList<TraitDefinition> Traits { get; }
        public IReadOnlyDictionary<int,AugmentDefinition> Augments { get; }
        private readonly Dictionary<int,int> levelXp, pool;
        private readonly Dictionary<int,int[]> rates;
        private readonly Dictionary<int,float> rules;
        public DataRepository(GameSnapshot data)
        {
            if(data==null || data.schemaVersion<1 || data.schemaVersion>4) throw new ArgumentException("지원하지 않는 데이터 스키마.");
            Version=data.dataVersion;
            Augments=new ReadOnlyDictionary<int,AugmentDefinition>((data.schemaVersion>=4
                ? data.augments ?? throw new ArgumentException("강화 정의 없음.") : Array.Empty<AugmentRow>())
                .ToDictionary(row=>row.augmentId,row=>new AugmentDefinition(row)));
            Units=new ReadOnlyDictionary<int,UnitDefinition>(data.units.ToDictionary(x=>x.unitId,x=>new UnitDefinition(x)));
            Rounds=new ReadOnlyDictionary<int,RoundDefinition>(data.rounds.ToDictionary(x=>x.roundId,x=>new RoundDefinition(x,data.roundUnits.Where(e=>e.roundId==x.roundId))));
            Skills=new ReadOnlyDictionary<int,SkillDefinition>((data.skills ?? Array.Empty<SkillRow>()).ToDictionary(x=>x.skillId,x=>new SkillDefinition(x)));
            Effects=new ReadOnlyDictionary<int,EffectDefinition>((data.effects ?? Array.Empty<EffectRow>()).ToDictionary(x=>x.effectId,x=>x.Create()));
            Traits=Array.AsReadOnly(data.schemaVersion>=3
                ? (data.traits ?? throw new ArgumentException("시너지 정의 없음.")).OrderBy(row=>row.traitId).Select(row=>new TraitDefinition(row)).ToArray()
                : Array.Empty<TraitDefinition>());
            if(data.units.Any(unit=>unit.skillId!=0 && !Skills.ContainsKey(unit.skillId))
                || Skills.Values.Any(skill=>skill.EffectId!=0 && !Effects.ContainsKey(skill.EffectId)))
                throw new ArgumentException("존재하지 않는 스킬·효과 ID 참조.");
            levelXp=data.levels.ToDictionary(x=>x.level,x=>x.requiredXp);
            pool=data.poolCounts.ToDictionary(x=>x.cost,x=>x.copiesPerUnit);
            rates=data.shopRates.ToDictionary(x=>x.level,x=>(int[])x.weights.Clone());
            rules=data.economy.ToDictionary(x=>x.ruleId,x=>x.value);
        }
        public int RequiredXp(int level)=>levelXp[level];
        public int PoolCopies(int cost)=>pool[cost];
        public IReadOnlyList<int> ShopWeights(int level)=>Array.AsReadOnly(rates[level]);
        public float Rule(EconomyRule rule)=>rules[(int)rule];
        public int IntRule(EconomyRule rule)=>(int)Rule(rule);
    }
}
