#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Battle.AI;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Battle.Skills;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Snapshot;
using AutoBattler.Data.Skills;
using AutoBattler.Data.Units;
using AutoBattler.EditorTools.Sheets;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    public static class SkillChecks
    {
        private static int checks;
        private static void Expect(bool condition,string message)
        { if(!condition) throw new Exception("Skill check: "+message); checks++; }
        private static Dictionary<string,CsvTable> Tables()=>SnapshotImporter.Tabs.ToDictionary(tab=>tab,
            tab=>new CsvTable(tab,File.ReadAllText("Docs/Sheets/"+tab+".csv")));
        public static void RunDomain()
        {
            checks=0; var tables=Tables(); var data=SnapshotImporter.Build(tables); var repository=new DataRepository(data);
            Expect(repository.Skills.Count==8 && repository.Effects.Count==3,"스킬·효과 CSV 연결");
            Expect(data.units.All(unit=>repository.Skills.ContainsKey(unit.skillId)),"유닛 8종 스킬 참조");
            Expect(DamageResolver.Calculate(132.5f,25)==106,"계수 소수점은 방어 계산 후 최종 피해에서 내림");
            var legacy=Tables(); legacy["Manifest"].Rows[1][0]="1";
            legacy.Remove("Skills"); legacy.Remove("Effects");
            // 열 위치를 외부 스키마 이름으로 찾아 기존 8개 표 호환 확인.
            int skillColumn=legacy["Units"].Rows[0].IndexOf("skill_id");
            foreach(var row in legacy["Units"].Rows.Skip(1)) if(row.Count>skillColumn) row[skillColumn]="0";
            var old=SnapshotImporter.Build(legacy);
            Expect(old.schemaVersion==1 && new DataRepository(old).Skills.Count==0,"기존 스키마 1 호환");
            var bad=Tables(); bad["Skills"].Rows[1][14]="99999";
            bool rejected=false; try { SnapshotImporter.Build(bad); } catch(FormatException) { rejected=true; }
            Expect(rejected,"없는 효과 참조 거부");
            bad=Tables(); bad["Skills"].Rows[1][2]="InvalidTarget"; rejected=false;
            try { SnapshotImporter.Build(bad); } catch(FormatException) { rejected=true; }
            Expect(rejected,"잘못된 대상 Enum 거부");
            bad=Tables(); bad.Remove("Effects"); rejected=false;
            try { SnapshotImporter.Build(bad); } catch(FormatException) { rejected=true; }
            Expect(rejected,"스키마 2 필수 표 누락 거부");
            Debug.Log("SKILLS_DOMAIN_PASS "+checks);
        }
        private static UnitDefinition TargetDefinition(UnitDefinition source)=>new UnitDefinition(new UnitRow {
            unitId=source.Id,name=source.Name,cost=source.Cost,originId=source.OriginId,classId=source.ClassId,
            hp=10000,attack=source.Attack,armor=100,attackRange=source.Range,combatRole=source.Role,skillId=source.SkillId,
            visualId=source.VisualId,iconId=source.IconId,attackType=source.IsRanged?"Ranged":"Melee",attackInterval=source.Interval,
            critChance=source.CritChance,critMultiplier=source.CritMultiplier });
        private static void Configure(Unit unit,UnitDefinition definition,int id,UnitTeam team,HexCoord cell,
            RunController run,BattleFlowController flow,int star=1,int boss=0)
        {
            unit.Configure(definition,star,id,team,run.State.Data,flow,run.Grid,run.Bench,team==UnitTeam.Enemy?cell:null,boss);
            if(team==UnitTeam.Ally)
            {
                var board=new BoardState(); var placement=new PlacementService(board); var state=new UnitState(id);
                Expect(placement.TryRegisterUnit(state,UnitLocation.OnBoard(cell)),"스킬 검사 배치");
                unit.View.Bind(state,board);
            }
            unit.transform.position=run.Grid.CellToWorld(cell); unit.gameObject.SetActive(true); unit.BeginBattle();
        }
        public static async UniTask RunPlay(RunController run)
        {
            using var assets=new AssetService(); using var lifetime=new CancellationTokenSource();
            var root=new GameObject("SkillProbes"); var flowRoot=new GameObject("SkillProbeFlow",typeof(BattleFlowController));
            var flow=flowRoot.GetComponent<BattleFlowController>(); var pool=new UnitPrefabPool(assets,root.transform);
            var rented=new List<Unit>();
            try
            {
                foreach(var definition in run.State.Data.Units.Values) await pool.PreloadAsync(run.Catalog.VisualPrefab(definition.VisualId),lifetime.Token);
                var shield=run.State.Data.Units[1001]; var common=run.Catalog.VisualPrefab(shield.VisualId);
                var target=pool.Rent(common); var other=pool.Rent(common); var friend=pool.Rent(common);
                rented.AddRange(new[]{target,other,friend});
                var cell=HexCoord.FromOffset(2,3); var targetCell=cell.GetNeighbor(0); var otherCell=cell.GetNeighbor(1);
                Expect(flow.TryStartCombat(),"스킬 검사 전투 시작");
                foreach(var definition in run.State.Data.Units.Values.OrderBy(unit=>unit.Id))
                {
                    var caster=pool.Rent(run.Catalog.VisualPrefab(definition.VisualId)); rented.Add(caster);
                    Configure(caster,definition,740000+definition.Id,UnitTeam.Ally,cell,run,flow);
                    Configure(target,TargetDefinition(shield),731001,UnitTeam.Enemy,targetCell,run,flow);
                    Configure(other,TargetDefinition(shield),731002,UnitTeam.Enemy,otherCell,run,flow);
                    Configure(friend,shield,731003,UnitTeam.Ally,HexCoord.FromOffset(1,3),run,flow);
                    if(definition.Id==1003)
                    {
                        caster.Mana.Add(100);
                        Expect(!caster.Skill.TryPrepare(new[]{caster,friend},null,out _) && caster.Mana.Current==100,"모든 아군 최대 체력 시 치유 보류");
                    }
                    if(definition.Id==1003) friend.Health.TakeDamage(100);
                    var participants=new[]{caster,target,other,friend}; caster.Mana.Add(100);
                    Expect(caster.Skill.TryPrepare(participants,target,out var context),"유닛별 시전 대상 "+definition.Id);
                    int targetHp=target.Health.CurrentHp,otherHp=other.Health.CurrentHp,friendHp=friend.Health.CurrentHp;
                    var cast=caster.Skill.CastAsync(context,lifetime.Token);
                    Expect(caster.Skill.IsCasting && caster.Mana.Current==0 && !caster.Combat.CanAttack && !caster.Move.CanMove,"준비 마나 차감과 행동 잠금");
                    await cast;
                    var skill=context.Definition;
                    if(definition.Id==1001) Expect(caster.Effects.Shield==80,"방패검사 보호막");
                    else if(definition.Id==1003) Expect(friend.Health.CurrentHp==Math.Min(friend.Health.MaxHp,friendHp+90),"최저 체력 비율 아군 치유");
                    else
                    {
                        int raw=Mathf.FloorToInt(skill.BaseDamage+caster.Stats.AttackDamage*skill.AttackRatio);
                        int damage=DamageResolver.Calculate(raw,target.Stats.Defense,caster.StatModifiers.SkillPower,skill.ArmorIgnore);
                        Expect(target.Health.CurrentHp==targetHp-damage,"스킬 피해·방어·치명타 미적용 "+definition.Id+" actual="+(targetHp-target.Health.CurrentHp)+" expected="+damage);
                        if(skill.Radius>0) Expect(other.Health.CurrentHp==otherHp-damage,"육각 범위 피해 "+definition.Id);
                        if(definition.Id==1008) Expect(target.Effects.IsStunned,"추적자 기절");
                    }
                }
                var unit=rented.First(actor=>actor.Definition.Id==1001 && actor.Team==UnitTeam.Ally);
                Configure(unit,shield,732001,UnitTeam.Ally,cell,run,flow);
                var one=new[]{unit}; unit.Mana.Add(100);
                Expect(unit.Skill.TryPrepare(one,null,out var shieldCast),"자기 대상 스킬에 적 불필요");
                var preparing=unit.Skill.CastAsync(shieldCast,lifetime.Token);
                var source=new ModifierSource(ModifierSourceKind.Skill,9991);
                var stun=unit.Effects.Apply(new EffectDefinition(9991,EffectKind.Stun,1f,10f),source);
                unit.Effects.Apply(new EffectDefinition(9991,EffectKind.Stun,1f,10f),source);
                await preparing;
                Expect(unit.Mana.Current==100 && !unit.Skill.IsCasting && unit.Effects.Shield==0,"기절 취소 시 마나 한 번 반환");
                unit.Effects.Remove(stun);
                var tree=new UnitBehaviourTree(new UnitBlackboard(unit,new CombatBoard(),one));
                tree.Tick(lifetime.Token);
                Expect(unit.Skill.IsCasting && tree.Blackboard.Action==UnitBehaviourAction.Casting,"행동트리 스킬 우선");
                await UniTask.WaitUntil(()=>!unit.Skill.IsCasting);
                unit.Effects.Apply(new EffectDefinition(9992,EffectKind.Stun,1f,10f),source);
                Expect(unit.Mana.Current==0,"효과 발동 뒤 기절에 마나 반환 없음");
                Configure(unit,shield,732001,UnitTeam.Ally,cell,run,flow,2);
                unit.Mana.Add(100); unit.Skill.TryPrepare(one,null,out shieldCast); await unit.Skill.CastAsync(shieldCast,lifetime.Token);
                Expect(unit.Effects.Shield==144,"2성 고정 보호막 1.8배");
                var archer=rented.First(actor=>actor.Definition.Id==1002 && actor.Team==UnitTeam.Ally);
                Configure(archer,run.State.Data.Units[1002],732002,UnitTeam.Ally,cell,run,flow);
                Configure(target,TargetDefinition(shield),731001,UnitTeam.Enemy,targetCell,run,flow);
                archer.Mana.Add(100); archer.Skill.TryPrepare(new[]{archer,target},target,out var arrow);
                var pending=archer.Skill.CastAsync(arrow,lifetime.Token); target.Health.TakeDamage(int.MaxValue); await pending;
                Expect(archer.Mana.Current==100,"준비 중 대상 소멸과 후보 없음 반환");
                Configure(target,TargetDefinition(shield),731001,UnitTeam.Enemy,targetCell,run,flow);
                Configure(other,TargetDefinition(shield),731002,UnitTeam.Enemy,otherCell,run,flow);
                archer.Skill.TryPrepare(new[]{archer,target,other},target,out arrow); pending=archer.Skill.CastAsync(arrow,lifetime.Token);
                target.Health.TakeDamage(int.MaxValue); int hp=other.Health.CurrentHp; await pending;
                Expect(other.Health.CurrentHp<hp && archer.Mana.Current==0,"준비 중 대상 사망 시 재선정");
                Configure(archer,run.State.Data.Units[1002],732002,UnitTeam.Ally,cell,run,flow);
                Configure(target,TargetDefinition(shield),731001,UnitTeam.Enemy,targetCell,run,flow);
                archer.Mana.Add(100); bool released=false; Action<SkillCastContext> onReleased=context=>released=true;
                archer.Skill.EffectReleased+=onReleased; archer.Skill.TryPrepare(new[]{archer,target},target,out arrow);
                pending=archer.Skill.CastAsync(arrow,lifetime.Token); await UniTask.WaitUntil(()=>released);
                Configure(target,TargetDefinition(shield),731001,UnitTeam.Enemy,otherCell,run,flow); hp=target.Health.CurrentHp;
                await pending; archer.Skill.EffectReleased-=onReleased;
                Expect(target.Health.CurrentHp==hp,"발사 후 같은 ID·오브젝트 재사용에 오적중 방지");
                Configure(unit,shield,732001,UnitTeam.Ally,cell,run,flow); unit.Mana.Add(100);
                unit.Skill.TryPrepare(one,null,out shieldCast); pending=unit.Skill.CastAsync(shieldCast,lifetime.Token);
                unit.Health.TakeDamage(int.MaxValue); await pending;
                Expect(unit.Mana.Current==0 && !unit.Skill.IsCasting,"시전자 사망 시 반환 없음");
                Configure(unit,shield,732001,UnitTeam.Ally,cell,run,flow); unit.Mana.Add(100);
                unit.Skill.TryPrepare(one,null,out shieldCast); pending=unit.Skill.CastAsync(shieldCast,lifetime.Token);
                pool.Return(unit); await pending;
                Expect(!unit.Skill.IsCasting && unit.Mana.Current==0,"준비 중 풀 반환 정리");
                Debug.Log("SKILLS_PLAY_PASS 8-skills-mana-stun-refund-retarget-projectile-version-star-death-pool");
            }
            finally
            {
                lifetime.Cancel(); foreach(var unit in rented) pool.Return(unit);
                await pool.DisposeAsync(); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(flowRoot);
            }
            Expect(assets.ActiveHandleCount==0,"스킬 검사 핸들 해제");
        }
        public static void BuildAndPlay()
        { RunDomain(); BehaviourTreeChecks.RunDomain(); UnitEffectsChecks.RunDomain(); MilestoneChecks.RunAll(); CharacterPrefabSetup.BuildForVerification(); }
    }
}
#endif
