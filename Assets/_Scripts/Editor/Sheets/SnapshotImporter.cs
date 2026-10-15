#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoBattler.Data.Snapshot;
using AutoBattler.Data.Skills;
using AutoBattler.Data.Units;
using AutoBattler.Data.Traits;
using AutoBattler.Battle.Units;
using UnityEditor;
using UnityEngine;

namespace AutoBattler.EditorTools.Sheets
{
    /// <summary>전체 표가 정상일 때만 현재 스냅샷 교체. 런타임에는 실행하지 않음.</summary>
    public static class SnapshotImporter
    {
        public const string OutputPath="Assets/_Data/Snapshots/GameSnapshot.json";
        public static readonly string[] BaseTabs={"Manifest","Units","Rounds","RoundUnits","Levels","ShopRates","PoolCounts","Economy"};
        public static readonly string[] SkillTabs=BaseTabs.Concat(new[]{"Skills","Effects"}).ToArray();
        public static readonly string[] Tabs=SkillTabs.Concat(new[]{"Traits"}).ToArray();
        public static string[] TabsForSchema(int schema)=>schema==1?BaseTabs:schema==2?SkillTabs:schema==3?Tabs:throw new FormatException("지원하지 않는 스키마.");
        public static GameSnapshot Build(Dictionary<string,CsvTable> tables)
        {
            foreach(string tab in BaseTabs) if(!tables.ContainsKey(tab)) throw new FormatException(tab+": 표 없음.");
            var manifest=tables["Manifest"];
            if(manifest.Rows.Count!=2) throw manifest.Error(1,"schema_version","단일 Manifest 행 필요.");
            int schema=manifest.Int(1,"schema_version");
            if(manifest.Rows.Count!=2 || schema<1 || schema>3) throw manifest.Error(1,"schema_version","스키마 1~3의 단일 Manifest 행 필요.");
            foreach(string tab in TabsForSchema(schema)) if(!tables.ContainsKey(tab)) throw new FormatException(tab+": 표 없음.");
            var data=new GameSnapshot {schemaVersion=schema,dataVersion=manifest.Get(1,"data_version")};
            if(string.IsNullOrWhiteSpace(data.dataVersion)) throw manifest.Error(1,"data_version","버전 필요.");
            data.units=Read(tables["Units"],(t,r)=>new UnitRow {
                unitId=t.Int(r,"unit_id"),name=t.Get(r,"name"),cost=t.Int(r,"cost"),originId=t.Int(r,"origin_id"),classId=t.Int(r,"class_id"),
                hp=t.Int(r,"hp"),attack=t.Int(r,"attack"),armor=t.Int(r,"armor"),attackInterval=t.Float(r,"attack_interval"),attackType=t.Get(r,"attack_type"),
                attackRange=t.Int(r,"attack_range"),combatRole=t.Int(r,"combat_role"),critChance=t.Float(r,"crit_chance"),critMultiplier=t.Float(r,"crit_multiplier"),
                skillId=t.Int(r,"skill_id",true),visualId=t.Int(r,"visual_id"),iconId=t.Int(r,"icon_id") });
            data.rounds=Read(tables["Rounds"],(t,r)=>new RoundRow {roundId=t.Int(r,"round_id"),rewardGold=t.Int(r,"reward_gold"),isBoss=t.Bool(r,"is_boss"),offersAugment=t.Bool(r,"offers_augment")});
            data.roundUnits=Read(tables["RoundUnits"],(t,r)=>new RoundUnitRow {entryId=t.Int(r,"entry_id"),roundId=t.Int(r,"round_id"),unitId=t.Int(r,"unit_id"),star=t.Int(r,"star"),col=t.Int(r,"col"),row=t.Int(r,"row"),bossProfileId=t.Int(r,"boss_profile_id",true)});
            data.levels=Read(tables["Levels"],(t,r)=>new LevelRow {level=t.Int(r,"level"),requiredXp=t.Int(r,"required_xp"),deploymentLimit=t.Int(r,"deployment_limit")});
            data.shopRates=Read(tables["ShopRates"],(t,r)=>new ShopRateRow {level=t.Int(r,"level"),weights=Enumerable.Range(1,5).Select(c=>t.Int(r,"cost_"+c)).ToArray()});
            data.poolCounts=Read(tables["PoolCounts"],(t,r)=>new PoolRow {cost=t.Int(r,"cost"),copiesPerUnit=t.Int(r,"copies_per_unit")});
            data.economy=Read(tables["Economy"],(t,r)=>new EconomyRow {ruleId=t.Int(r,"rule_id"),value=t.Float(r,"value")});
            data.skills=schema==1?Array.Empty<SkillRow>():Read(tables["Skills"],(t,r)=>new SkillRow {
                skillId=t.Int(r,"skill_id"),name=t.Get(r,"name"),targetRule=ReadEnum<SkillTargetRule>(t,r,"target_rule"),
                delivery=ReadEnum<SkillDelivery>(t,r,"delivery"),animation=ReadEnum<SkillAnimation>(t,r,"animation"),
                manaCost=t.Int(r,"mana_cost"),castRange=t.Int(r,"cast_range"),windup=t.Float(r,"windup"),cooldown=t.Float(r,"cooldown"),
                radius=t.Int(r,"radius"),centerOnCaster=t.Bool(r,"center_on_caster"),baseDamage=t.Float(r,"base_damage"),
                attackRatio=t.Float(r,"attack_ratio"),armorIgnore=t.Float(r,"armor_ignore"),effectId=t.Int(r,"effect_id",true),projectileSpeed=t.Float(r,"projectile_speed") });
            data.effects=schema==1?Array.Empty<EffectRow>():Read(tables["Effects"],(t,r)=>new EffectRow {
                effectId=t.Int(r,"effect_id"),kind=ReadEnum<EffectKind>(t,r,"kind"),value=t.Float(r,"value"),duration=t.Float(r,"duration"),
                stacking=ReadEnum<EffectStacking>(t,r,"stacking"),stat=ReadEnum<StatType>(t,r,"stat"),operation=ReadEnum<ModifierOperation>(t,r,"operation") });
            data.traits=schema<3?Array.Empty<TraitRow>():Read(tables["Traits"],(t,r)=>new TraitRow {
                traitId=t.Int(r,"trait_id"),name=t.Get(r,"name"),category=ReadEnum<TraitCategory>(t,r,"category"),
                groupId=t.Int(r,"group_id"),requiredCount=t.Int(r,"required_count"),effect=ReadEnum<TraitEffect>(t,r,"effect"),
                value=t.Float(r,"value"),secondaryValue=t.Float(r,"secondary_value"),duration=t.Float(r,"duration"),threshold=t.Float(r,"threshold") });
            if(schema>=3)
            {
                var t=tables["Traits"]; CheckIds(data.traits,x=>x.traitId,t);
                var stages=new HashSet<(TraitCategory,int,int)>();
                foreach(var trait in data.traits)
                {
                    int r=FindRow(t,"trait_id",trait.traitId);
                    if(string.IsNullOrWhiteSpace(trait.name) || trait.groupId<1 || trait.groupId>4 || trait.requiredCount<1 || trait.requiredCount>10
                        || trait.value<0 || trait.secondaryValue<0 || trait.duration<0 || trait.threshold<0 || trait.threshold>1
                        || (trait.effect==TraitEffect.ShieldAtStart && trait.duration<=0)
                        || (trait.effect==TraitEffect.LowHealthAttack && trait.threshold<=0)
                        || (trait.effect==TraitEffect.StartingMana && (trait.value>100 || trait.value!=(int)trait.value)))
                        throw t.Error(r,"trait_id","시너지 수치·집계 범위 오류.");
                    if(!stages.Add((trait.category,trait.groupId,trait.requiredCount))) throw t.Error(r,"required_count","같은 시너지 단계 중복.");
                }
                if(data.traits.Select(x=>(x.category,x.groupId)).Distinct().Count()!=8) throw new FormatException("Traits: 계열 4종과 직업 4종 정의 필요.");
            }
            if(schema>=2)
            {
                CheckIds(data.skills,x=>x.skillId,tables["Skills"]); CheckIds(data.effects,x=>x.effectId,tables["Effects"]);
                foreach(var skill in data.skills)
                {
                    var t=tables["Skills"]; int r=FindRow(t,"skill_id",skill.skillId);
                    if(skill.manaCost<1 || skill.manaCost>100 || skill.castRange<0 || skill.castRange>14 || skill.windup<0 || skill.cooldown<0
                        || skill.radius<0 || skill.radius>14 || skill.baseDamage<0 || skill.attackRatio<0 || skill.armorIgnore<0 || skill.armorIgnore>1
                        || (skill.delivery==SkillDelivery.Projectile && skill.projectileSpeed<=0)) throw t.Error(r,"skill_id","시전 수치 범위 오류.");
                    if(skill.effectId!=0 && !data.effects.Any(x=>x.effectId==skill.effectId)) throw t.Error(r,"effect_id","없는 효과 참조.");
                }
                foreach(var effect in data.effects)
                {
                    var t=tables["Effects"]; int r=FindRow(t,"effect_id",effect.effectId);
                    if(effect.value<0 || (effect.kind==EffectKind.Slow && effect.value>1)
                        || (effect.kind!=EffectKind.Heal && effect.duration==0)) throw t.Error(r,"value/duration","효과 수치 범위 오류.");
                }
            }
            CheckIds(data.units,x=>x.unitId,tables["Units"]); CheckIds(data.rounds,x=>x.roundId,tables["Rounds"]);
            CheckIds(data.roundUnits,x=>x.entryId,tables["RoundUnits"]); CheckIds(data.levels,x=>x.level,tables["Levels"]);
            CheckIds(data.shopRates,x=>x.level,tables["ShopRates"]); CheckIds(data.poolCounts,x=>x.cost,tables["PoolCounts"]);
            CheckIds(data.economy,x=>x.ruleId,tables["Economy"]);
            foreach(var unit in data.units)
            {
                int r=FindRow(tables["Units"],"unit_id",unit.unitId); var t=tables["Units"];
                if(unit.cost<1 || unit.cost>5) throw t.Error(r,"cost","1~5 범위.");
                if(unit.hp<1 || unit.attack<1 || unit.armor<0 || unit.attackInterval<=0 || unit.attackRange<1 || unit.attackRange>14) throw t.Error(r,"hp/attack/armor/attack_interval/attack_range","전투 수치 범위 오류.");
                if(unit.combatRole<0 || unit.combatRole>3 || unit.originId<1 || unit.originId>4 || unit.classId<1 || unit.classId>4) throw t.Error(r,"combat_role/origin_id/class_id","정의 범위 오류.");
                if(unit.attackType!="Melee" && unit.attackType!="Ranged") throw t.Error(r,"attack_type","Melee 또는 Ranged.");
                if(unit.critChance<0 || unit.critChance>1 || unit.critMultiplier<1) throw t.Error(r,"crit_chance/crit_multiplier","확률 0~1, 배율 1 이상.");
                if(unit.skillId!=0 && !data.skills.Any(x=>x.skillId==unit.skillId)) throw t.Error(r,"skill_id","없는 스킬 참조.");
                if(unit.visualId<1 || unit.visualId>8 || unit.iconId<1 || unit.iconId>8) throw t.Error(r,"visual_id/icon_id","현재 AssetCatalog ID 1~8 필요.");
            }
            if(!data.units.Any(x=>x.cost==1)) throw new FormatException("Units: 시작용 1코스트 필요.");
            if(data.rounds.Length==0 || data.rounds.OrderBy(x=>x.roundId).Select((x,i)=>x.roundId!=i+1).Any(x=>x)) throw new FormatException("Rounds round_id: 1부터 연속된 라운드 필요.");
            foreach(var round in data.rounds)
                if(round.rewardGold<0 || !data.roundUnits.Any(x=>x.roundId==round.roundId)) throw tables["Rounds"].Error(FindRow(tables["Rounds"],"round_id",round.roundId),"reward_gold/round_id","음수 보상 또는 적 구성 없음.");
            var positions=new HashSet<string>();
            foreach(var enemy in data.roundUnits)
            {
                var t=tables["RoundUnits"]; int r=FindRow(t,"entry_id",enemy.entryId);
                if(!data.rounds.Any(x=>x.roundId==enemy.roundId) || !data.units.Any(x=>x.unitId==enemy.unitId)) throw t.Error(r,"round_id/unit_id","없는 ID 참조.");
                if(enemy.star<1 || enemy.star>2 || enemy.col<4 || enemy.col>=8 || enemy.row<0 || enemy.row>=7 || enemy.bossProfileId<0 || enemy.bossProfileId>3) throw t.Error(r,"star/col/row/boss_profile_id","성급·적 영역·보스 샘플 범위 오류.");
                if(!positions.Add($"{enemy.roundId}:{enemy.col}:{enemy.row}")) throw t.Error(r,"col/row","같은 라운드의 좌표 중복.");
            }
            if(data.levels.Length!=10 || data.shopRates.Length!=10) throw new FormatException("Levels/ShopRates: 레벨 1~10 각각 필요.");
            for(int level=1;level<=10;level++)
            {
                var l=data.levels.FirstOrDefault(x=>x.level==level); var rate=data.shopRates.FirstOrDefault(x=>x.level==level);
                if(l==null || rate==null || l.deploymentLimit!=level || (level<10?l.requiredXp<=0:l.requiredXp!=0)) throw new FormatException($"Levels/ShopRates level={level}: 누락 또는 경험치·인원 범위 오류.");
                if(rate.weights.Any(x=>x<0) || rate.weights.Sum()!=100) throw tables["ShopRates"].Error(FindRow(tables["ShopRates"],"level",level),"cost_1~5","확률 합계 100 필요.");
            }
            if(data.poolCounts.Length!=5 || Enumerable.Range(1,5).Any(c=>!data.poolCounts.Any(x=>x.cost==c && x.copiesPerUnit>0))) throw new FormatException("PoolCounts: 코스트 1~5의 양수 재고 필요.");
            if(data.economy.Length!=13 || Enumerable.Range(1,13).Any(id=>!data.economy.Any(x=>x.ruleId==id && x.value>0))) throw new FormatException("Economy: 규칙 ID 1~13의 양수 값 필요.");
            if(data.economy.Any(x=>x.ruleId<=9 && x.value!=(int)x.value)) throw new FormatException("Economy: ID 1~9는 정수 필요.");
            var repository=new DataRepository(data);
            return data;
        }
        private static T[] Read<T>(CsvTable table,Func<CsvTable,int,T> reader)
        {
            var result=new List<T>(); for(int r=1;r<table.Rows.Count;r++) if(!table.IsEmpty(r)) result.Add(reader(table,r)); return result.ToArray();
        }
        private static T ReadEnum<T>(CsvTable table,int row,string column) where T:struct,Enum
        {
            if(!Enum.TryParse<T>(table.Get(row,column),true,out var result) || !Enum.IsDefined(typeof(T),result))
                throw table.Error(row,column,"등록된 Enum 이름 필요.");
            return result;
        }
        private static int FindRow(CsvTable table,string column,int id)
        {
            for(int r=1;r<table.Rows.Count;r++) if(!table.IsEmpty(r) && table.Int(r,column)==id) return r;
            return 1;
        }
        private static void CheckIds<T>(T[] rows,Func<T,int> id,CsvTable table)
        {
            var ids=new HashSet<int>(); int row=1;
            foreach(var item in rows) { while(table.IsEmpty(row)) row++; int value=id(item);
                if(value<=0 || !ids.Add(value)) throw table.Error(row,table.Rows[0][0],"양수 고유 ID 필요."); row++; }
        }
        public static void Apply(GameSnapshot snapshot)
        {
            string directory=Path.GetDirectoryName(OutputPath); Directory.CreateDirectory(directory);
            string temporary=OutputPath+".pending";
            try
            {
                File.WriteAllText(temporary,JsonUtility.ToJson(snapshot,true)+"\n",new System.Text.UTF8Encoding(false));
                if(File.Exists(OutputPath)) File.Replace(temporary,OutputPath,null); else File.Move(temporary,OutputPath);
                AssetDatabase.ImportAsset(OutputPath);
            }
            finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
#endif
