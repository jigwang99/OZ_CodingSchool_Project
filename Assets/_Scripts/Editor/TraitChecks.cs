#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Battle.Traits;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Snapshot;
using AutoBattler.Data.Traits;
using AutoBattler.EditorTools.Sheets;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    public static class TraitChecks
    {
        private static int checks;
        private static void Expect(bool value,string message)
        { if(!value) throw new Exception("Trait check: "+message); checks++; }
        private static bool Near(float a,float b)=>Mathf.Abs(a-b)<.001f;
        private static Dictionary<string,CsvTable> Tables()=>SnapshotImporter.Tabs.ToDictionary(tab=>tab,tab=>new CsvTable(tab,File.ReadAllText("Docs/Sheets/"+tab+".csv")));
        public static void RunDomain()
        {
            checks=0; var data=SnapshotImporter.Build(Tables()); var repository=new DataRepository(data);
            Expect(repository.Traits.Count==8 && data.schemaVersion==4,"시너지 데이터 8종과 스키마 4");
            var all=TraitCounter.Count(repository.Units.Values,repository.Traits);
            Expect(all.Active.Count==8 && all.Statuses.All(status=>status.Count==2),"계열·직업 각각 2종 독립 집계");
            var human=repository.Units[1001];
            var duplicates=TraitCounter.Count(new[]{human,human,human},repository.Traits);
            Expect(duplicates.Count(TraitCategory.Origin,1)==1 && duplicates.Count(TraitCategory.Class,1)==1 && duplicates.Active.Count==0,"동일 종류 여러 기는 1종");
            var mixed=TraitCounter.Count(new[]{human,repository.Units[1007],repository.Units[1003]},repository.Traits);
            Expect(mixed.Count(TraitCategory.Origin,1)==2 && mixed.Count(TraitCategory.Class,2)==2 && mixed.Active.Count==2,"인간과 마법사 동시 활성");
            var stage=new TraitDefinition(new TraitRow {traitId=4901,name="Human 3",category=TraitCategory.Origin,groupId=1,requiredCount=3,effect=TraitEffect.ShieldAtStart,value=.3f,duration=5});
            var third=new UnitDefinition(new UnitRow {unitId=1999,originId=1,classId=1});
            var highest=TraitCounter.Count(new[]{human,repository.Units[1007],third},repository.Traits.Concat(new[]{stage}));
            Expect(highest.Active.Count(definition=>definition.Category==TraitCategory.Origin && definition.GroupId==1)==1
                && highest.Active.Any(definition=>definition.Id==4901),"같은 시너지의 최고 단계만 적용");
            var legacy=Tables(); legacy["Manifest"].Rows[1][0]="2"; legacy.Remove("Traits");
            Expect(new DataRepository(SnapshotImporter.Build(legacy)).Traits.Count==0,"이전 스키마 2는 시너지 없이 호환");
            Reject(tables=>tables.Remove("Traits"),"Traits 누락");
            Reject(tables=>tables["Traits"].Rows.Add(new List<string>(tables["Traits"].Rows[1])),"시너지 ID 중복");
            Reject(tables=>tables["Traits"].Rows[1][5]="UnknownEffect","알 수 없는 효과");
            Reject(tables=>tables["Traits"].Rows[4][9]="0","오크 발동 기준 오류");
            Reject(tables=>tables["Traits"].Rows[2][6]="20.5","시작 마나 소수 금지");
            Reject(tables=>tables["Traits"].Rows[1][8]="0","보호막 유지 시간 필요");
            Debug.Log("TRAITS_DOMAIN_PASS "+checks);
        }
        private static void Reject(Action<Dictionary<string,CsvTable>> change,string message)
        {
            var tables=Tables(); change(tables); bool rejected=false;
            try { SnapshotImporter.Build(tables); } catch(FormatException) { rejected=true; }
            Expect(rejected,message);
        }
        private static void Configure(Unit unit,int definitionId,int id,UnitTeam team,RunController run,BattleFlowController flow,int star=1,int boss=0,bool bench=false)
        {
            var cell=HexCoord.FromOffset(team==UnitTeam.Ally?1:5,(id%5)+1);
            unit.Configure(run.State.Data.Units[definitionId],star,id,team,run.State.Data,flow,run.Grid,run.Bench,team==UnitTeam.Enemy?cell:null,boss);
            if(team==UnitTeam.Ally)
            {
                var state=new UnitState(id); var board=new BoardState(); var placement=new PlacementService(board);
                placement.TryRegisterUnit(state,bench?UnitLocation.OnBench(0):UnitLocation.OnBoard(cell)); unit.View.Bind(state,board);
            }
            unit.transform.position=run.Grid.CellToWorld(cell); unit.gameObject.SetActive(true); unit.BeginBattle();
        }
        public static async UniTask RunPlay(RunController run)
        {
            checks=0; using var assets=new AssetService(); using var lifetime=new CancellationTokenSource();
            var root=new GameObject("TraitProbes"); var flowRoot=new GameObject("TraitFlow",typeof(BattleFlowController));
            var flow=flowRoot.GetComponent<BattleFlowController>(); var pool=new UnitPrefabPool(assets,root.transform);
            var units=new List<Unit>(); TraitBattleSession session=null;
            try
            {
                foreach(var definition in run.State.Data.Units.Values) await pool.PreloadAsync(run.Catalog.VisualPrefab(definition.VisualId),lifetime.Token);
                foreach(var definition in run.State.Data.Units.Values.OrderBy(definition=>definition.Id))
                {
                    var unit=pool.Rent(run.Catalog.VisualPrefab(definition.VisualId)); units.Add(unit);
                    Configure(unit,definition.Id,780000+definition.Id,UnitTeam.Ally,run,flow);
                }
                var human=units.First(unit=>unit.Definition.Id==1001); var elf=units.First(unit=>unit.Definition.Id==1002);
                var healer=units.First(unit=>unit.Definition.Id==1003); var orc=units.First(unit=>unit.Definition.Id==1004);
                var undead=units.First(unit=>unit.Definition.Id==1005); var assassin=units.First(unit=>unit.Definition.Id==1006);
                var mage=units.First(unit=>unit.Definition.Id==1007); var tracker=units.First(unit=>unit.Definition.Id==1008);
                var duplicate=pool.Rent(run.Catalog.VisualPrefab(human.Definition.VisualId)); units.Add(duplicate);
                Configure(duplicate,1001,790001,UnitTeam.Ally,run,flow,star:2);
                var enemy=pool.Rent(run.Catalog.VisualPrefab(human.Definition.VisualId)); units.Add(enemy);
                Configure(enemy,1001,790002,UnitTeam.Enemy,run,flow);
                Configure(mage,1007,mage.InstanceId,UnitTeam.Ally,run,flow,bench:true);
                var allyCount=TraitCounter.CountTeam(units,UnitTeam.Ally,run.State.Data.Traits);
                Expect(allyCount.Count(TraitCategory.Origin,1)==1 && allyCount.Count(TraitCategory.Class,2)==1,"대기석 제외 및 성급 다른 동일 종류 중복 제외");
                Configure(mage,1007,mage.InstanceId,UnitTeam.Ally,run,flow);
                allyCount=TraitCounter.CountTeam(units,UnitTeam.Ally,run.State.Data.Traits);
                var enemyCount=TraitCounter.CountTeam(units,UnitTeam.Enemy,run.State.Data.Traits);
                Expect(allyCount.Active.Count==8 && enemyCount.Active.Count==0,"아군·적 집계 독립");
                Expect(human.Effects.Shield==0 && elf.Mana.Current==0 && Near(healer.StatModifiers.SkillPower,1f),"준비 집계만으로 시작 효과 지급하지 않음");
                flow.TryStartCombat(); session=new TraitBattleSession(units,allyCount,enemyCount); session.Start();
                Expect(human.Effects.Shield==Mathf.FloorToInt(human.Health.MaxHp*.15f)
                    && duplicate.Effects.Shield==Mathf.FloorToInt(duplicate.Health.MaxHp*.15f),"인간 보호막은 동일 종류와 2성에도 각각 적용");
                Expect(elf.Mana.Starting==20 && elf.Mana.Current==20 && healer.Mana.Current==20,"요정 2종 시작 마나");
                Expect(Near(human.StatModifiers.BasicAttackPower,1.15f) && Near(undead.StatModifiers.BasicAttackPower,1.15f),"검사 기본 공격 위력");
                Expect(Near(healer.StatModifiers.SkillPower,1.2f) && Near(mage.StatModifiers.SkillPower,1.2f),"마법사 스킬 위력");
                Expect(Near(elf.Stats.AttackCooldown,elf.StatModifiers.State.Base.AttackCooldown/1.2f)
                    && Near(orc.Stats.AttackCooldown,orc.StatModifiers.State.Base.AttackCooldown/1.2f),"궁수 공격 속도");
                Expect(Near(assassin.StatModifiers.CritChance,.3f) && Near(assassin.StatModifiers.CritMultiplier,1.8f)
                    && Near(tracker.StatModifiers.CritChance,.3f),"암살자 확률·배율");
                Expect(enemy.Effects.Shield==0 && Near(enemy.StatModifiers.BasicAttackPower,1f),"비활성 적에게 아군 시너지 미적용");
                int modifiers=human.StatModifiers.State.ModifierCount,shield=human.Effects.Shield;
                session.Start();
                Expect(human.StatModifiers.State.ModifierCount==modifiers && human.Effects.Shield==shield && elf.Mana.Current==20,"중복 시작에 효과·마나 누적 없음");

                var unrelated=new ModifierSource(ModifierSourceKind.System,799999);
                orc.StatModifiers.ReplaceSource(unrelated,new StatModifierSpec(StatType.AttackDamage,ModifierOperation.Flat,7));
                int baseAttack=orc.StatModifiers.State.Base.AttackDamage;
                orc.Health.TakeDamage((orc.Health.MaxHp+1)/2,1f,1f);
                Expect(orc.Stats.AttackDamage==Mathf.FloorToInt((baseAttack+7)*1.25f),"오크 체력 50% 이하 발동과 다른 출처 합산");
                orc.Health.Heal(orc.Health.MaxHp);
                Expect(orc.Stats.AttackDamage==baseAttack+7,"오크 회복 시 해제 및 다른 버프 유지");

                undead.Health.TakeDamage(undead.Health.MaxHp/2,1f,1f); int hp=undead.Health.CurrentHp;
                enemy.Health.TakeDamage(DamageContext.BasicAttack(undead),int.MaxValue);
                int expected=Math.Min(undead.Health.MaxHp,hp+Mathf.FloorToInt(undead.Health.MaxHp*.15f));
                Expect(undead.Health.CurrentHp==expected,"언데드 처치 회복");
                enemy.Health.TakeDamage(DamageContext.BasicAttack(undead),int.MaxValue);
                Expect(undead.Health.CurrentHp==expected,"시체 재타격에 중복 회복 없음");
                mage.Health.TakeDamage(int.MaxValue);
                Expect(allyCount.Active.Count==8 && Near(healer.StatModifiers.SkillPower,1.2f),"전투 중 사망해도 확정 단계 유지");

                var shadowPrefab=run.Catalog.VisualPrefab(assassin.Definition.VisualId);
                pool.Return(assassin); var reused=pool.Rent(shadowPrefab);
                Expect(reused==assassin,"시너지 보유 유닛 풀 재사용");
                Configure(reused,1006,790099,UnitTeam.Ally,run,flow);
                Expect(Near(reused.StatModifiers.CritChance,.1f),"재사용 유닛에 이전 직업 버프 없음");
                reused.StatModifiers.ReplaceSource(new ModifierSource(ModifierSourceKind.OriginTrait,4003),new StatModifierSpec(StatType.AttackDamage,ModifierOperation.Flat,33));
                session.Dispose(); session.Dispose();
                Expect(reused.Stats.AttackDamage==reused.StatModifiers.State.Base.AttackDamage+33,"이전 전투 정리가 새 수명의 같은 출처를 제거하지 않음");
                Expect(Near(healer.StatModifiers.SkillPower,1f) && Near(human.StatModifiers.BasicAttackPower,1f)
                    && human.Effects.Shield==0 && elf.Mana.Starting==0,"종료 시 시너지 출처만 제거");
                Expect(orc.Stats.AttackDamage==baseAttack+7,"종료 후 다른 출처 유지");
                foreach(var unit in units) unit.BeginBattle();
                allyCount=TraitCounter.CountTeam(units,UnitTeam.Ally,run.State.Data.Traits);
                session=new TraitBattleSession(units,allyCount,enemyCount); session.Start();
                Expect(elf.Mana.Current==20 && Near(healer.StatModifiers.SkillPower,1.2f),"새 전투에 시너지 다시 적용");
                await UniTask.WaitUntil(()=>human.Effects.Shield==0).Timeout(TimeSpan.FromSeconds(10));
                Expect(human.Effects.Shield==0,"인간 보호막 5초 만료");
                session.Dispose();

                // 적 요정 시너지와 보스 시작 마나 상한 확인.
                Configure(enemy,1002,790002,UnitTeam.Enemy,run,flow,boss:1);
                Configure(mage,1003,790003,UnitTeam.Enemy,run,flow);
                var enemyRoster=new[]{enemy,mage}; enemyCount=TraitCounter.CountTeam(enemyRoster,UnitTeam.Enemy,run.State.Data.Traits);
                session=new TraitBattleSession(enemyRoster,TraitSnapshot.Empty,enemyCount); session.Start();
                Expect(enemy.Mana.Current==100 && mage.Mana.Current==20,"적 시너지 적용 및 보스 마나 100 상한");
                session.Dispose();
                Debug.Log("TRAITS_PLAY_PASS "+checks+" all-eight-count-duplicate-bench-teams-orc-heal-kill-shield-expiry-pool-reset");
            }
            finally
            {
                session?.Dispose(); lifetime.Cancel(); foreach(var unit in units) pool.Return(unit);
                await pool.DisposeAsync(); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(flowRoot);
            }
        }
        [MenuItem("PixelTactics/Checks/Run Trait Checks")]
        public static void BuildAndPlay() { RunDomain(); SkillChecks.BuildAndPlay(); }
    }
}
#endif
