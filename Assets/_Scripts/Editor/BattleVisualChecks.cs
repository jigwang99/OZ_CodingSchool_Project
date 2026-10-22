#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Units;
using AutoBattler.Data.Snapshot;
using AutoBattler.Presentation.Battle;
using AutoBattler.Presentation.Units;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    public static class BattleVisualChecks
    {
        private static int checks;
        private static void Expect(bool value,string message)
        { if(!value) throw new Exception("Battle visual check: "+message); checks++; }
        [MenuItem("PixelTactics/Checks/Run Battle Effects and Icons Checks")]
        public static void BuildAndPlay()
        {
            BattleVisualSetup.Connect();
            var setup=ProjectSetupSettings.Current;
            var before=File.ReadAllText(AssetDatabase.GetAssetPath(setup.BattleVisuals));
            var info=File.ReadAllText(setup.UnitInfoPath); BattleVisualSetup.Connect();
            Expect(before==File.ReadAllText(AssetDatabase.GetAssetPath(setup.BattleVisuals)) && info==File.ReadAllText(setup.UnitInfoPath),"셋업 중복 변경 없음");
            foreach(var entry in setup.BattleVisuals.Effects)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(entry.prefab.AssetGUID));
                Expect(prefab!=null && prefab.GetComponent<PooledBattleVisual>()!=null && !prefab.activeSelf,"풀 전용 비활성 프리팹");
                foreach(var system in prefab.GetComponentsInChildren<ParticleSystem>(true))
                    Expect(!system.main.loop && !system.main.playOnAwake && system.main.stopAction==ParticleSystemStopAction.None,"자동 생성·파괴 없는 파티클");
                foreach(var material in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true).SelectMany(renderer=>renderer.sharedMaterials))
                    Expect(material.shader.name=="Universal Render Pipeline/Particles/Unlit","URP 재질");
            }
            ShieldBarChecks.BuildAndPlay();
        }
        private static void Configure(Unit unit,UnitDefinition definition,int id,UnitTeam team,HexCoord cell,RunController run,BattleFlowController flow)
        {
            unit.Configure(definition,1,id,team,run.State.Data,flow,run.Grid,run.Bench,team==UnitTeam.Enemy?cell:null);
            if(team==UnitTeam.Ally)
            {
                var board=new BoardState(); var placement=new PlacementService(board); var state=new UnitState(id);
                Expect(placement.TryRegisterUnit(state,UnitLocation.OnBoard(cell)),"표현 검사 배치"); unit.View.Bind(state,board);
            }
            unit.transform.position=run.Grid.CellToWorld(cell); unit.gameObject.SetActive(true); unit.BeginBattle();
        }
        public static async UniTask RunPlay(RunController run,RunHUD hud)
        {
            using var assets=new AssetService();
            var root=new GameObject("BattleVisualProbes");
            var phase=new GameObject("BattleVisualProbeFlow").AddComponent<BattleFlowController>();
            var units=new UnitPrefabPool(assets,root.transform);
            var visuals=new BattleVisualService(assets,run.Catalog.BattleVisuals,root.transform,phase,run.Grid);
            float previousScale=Time.timeScale; bool hudEnabled=hud.enabled; hud.enabled=false; Time.timeScale=1f;
            try
            {
                await visuals.PreloadAsync(CancellationToken.None);
                foreach(var definition in run.State.Data.Units.Values) await units.PreloadAsync(run.Catalog.VisualPrefab(definition.VisualId),CancellationToken.None);
                int created=visuals.CreatedCount;
                Expect(visuals.PrefabLeaseCount==8 && created==156,"8종 효과 사전 로드와 고정 용량");
                var cell=HexCoord.FromOffset(2,3); var destination=cell.GetNeighbor(0);
                var target=units.Rent(run.Catalog.VisualPrefab(run.State.Data.Units[1001].VisualId));
                var friend=units.Rent(run.Catalog.VisualPrefab(run.State.Data.Units[1001].VisualId));
                Expect(phase.TryStartCombat(),"표현 검사 전투 단계");
                foreach(var definition in run.State.Data.Units.Values.OrderBy(unit=>unit.Id))
                {
                    var caster=units.Rent(run.Catalog.VisualPrefab(definition.VisualId));
                    Configure(caster,definition,810000+definition.Id,UnitTeam.Ally,cell,run,phase);
                    Configure(target,run.State.Data.Units[1001],820001,UnitTeam.Enemy,destination,run,phase);
                    Configure(friend,run.State.Data.Units[1001],820002,UnitTeam.Ally,cell.GetNeighbor(2),run,phase);
                    visuals.BeginBattle(new[]{caster,target,friend});
                    Expect(run.UnitIcon(definition.IconId)!=null && run.Catalog.BattleVisuals.Skill(definition.SkillId)?.icon!=null,"유닛·스킬 아이콘 "+definition.Id);
                    hud.UnitInfo.Show(caster,run.State.Data);
                    Expect(hud.UnitInfo.UnitIcon.sprite==run.UnitIcon(definition.IconId) && hud.UnitInfo.SkillIcon.sprite!=null,"유닛 정보 아이콘 "+definition.Id);
                    if(definition.Id==1003) friend.Health.TakeDamage(100);
                    int played=visuals.PlayedCount; caster.Mana.Add(100);
                    Expect(caster.Skill.TryPrepare(new[]{caster,target,friend},target,out var context),"스킬 준비 "+definition.Id);
                    var cast=caster.Skill.CastAsync(context,CancellationToken.None);
                    await cast;
                    Expect(visuals.PlayedCount>played,"실제 스킬 발동 표현 "+definition.Id);
                    if(definition.Id==1008)
                    {
                        var icon=visuals.Active.FirstOrDefault(item=>item.Id==BattleVisualId.Stun);
                        Expect(icon!=null && icon.SpriteRenderer.sprite==run.Catalog.BattleVisuals.Stun,"기절 상태 표시");
                        target.Effects.Clear(); Expect(!icon.IsRented,"기절 해제 즉시 반환");
                    }
                    if(definition.IsRanged)
                    {
                        Configure(target,run.State.Data.Units[1001],820001,UnitTeam.Enemy,destination,run,phase);
                        visuals.BeginBattle(new[]{caster,target,friend});
                        played=visuals.PlayedCount; await caster.Combat.AttackAsync(target.Combat,CancellationToken.None);
                        Expect(visuals.PlayedCount>=played+2,"기본 투사체 발사와 도착 타격 "+definition.Id);
                    }
                    await UniTask.Delay(200);
                    visuals.EndBattle(); Expect(visuals.ActiveCount==0,"스킬별 표현 정리"); units.Return(caster);
                }
                Configure(target,run.State.Data.Units[1001],820001,UnitTeam.Enemy,destination,run,phase);
                Configure(friend,run.State.Data.Units[1001],820002,UnitTeam.Ally,cell,run,phase);
                visuals.BeginBattle(new[]{target,friend});
                int count=visuals.PlayedCount; target.Health.TakeDamage(DamageContext.BasicAttack(friend),1);
                Expect(visuals.PlayedCount==count+1,"실제 기본 공격 피해에 타격");
                visuals.EndBattle(); visuals.BeginBattle(new[]{target,friend});
                var first=visuals.Play(BattleVisualId.Hit,run.Grid.CellToWorld(cell)); int identity=first.GetInstanceID();
                first.ReturnToPool(); first.ReturnToPool();
                var rented=Enumerable.Range(0,32).Select(_=>visuals.Play(BattleVisualId.Hit,Vector3.zero)).ToArray();
                Expect(rented.All(item=>item!=null) && rented.Select(item=>item.GetInstanceID()).Distinct().Count()==32,"중복 반환으로 중복 대여 없음");
                Expect(rented.Any(item=>item.GetInstanceID()==identity),"동일 인스턴스 재사용");
                Expect(visuals.Play(BattleVisualId.Hit,Vector3.zero)==null && visuals.SkippedCount==1 && visuals.CreatedCount==created,"풀 소진 시 생성 없이 표현만 생략");
                visuals.EndBattle(); visuals.BeginBattle(new[]{target,friend});
                target.Effects.Apply(new EffectDefinition(9901,EffectKind.Stun,1f,5f),new ModifierSource(ModifierSourceKind.Skill,9901));
                var stale=visuals.Active.First(item=>item.Id==BattleVisualId.Stun);
                units.Return(target); await UniTask.DelayFrame(1); Expect(!stale.IsRented,"유닛 반환 시 상태 표시 반환");
                hud.UnitInfo.Show(friend,run.State.Data); await UniTask.Delay(180);
                for(int i=0;i<6;i++)
                {
                    visuals.Play((BattleVisualId)i,run.Grid.CellToWorld(HexCoord.FromOffset(i+1,1)));
                }
                await UniTask.Delay(120);
                Canvas.ForceUpdateCanvases();
                if(Application.isBatchMode) SynergyUIChecks.Capture(1920,1080,"Combat_Effects_Icons.png");
                await UniTask.Delay(1800);
                Expect(visuals.ActiveCount==0 && visuals.CreatedCount==created,"재생 완료 자동 반환·전투 중 추가 생성 없음");
                visuals.Play(BattleVisualId.Fire,Vector3.zero); phase.TryFinishCombat();
                Expect(visuals.ActiveCount==0,"라운드 종료 즉시 전체 반환");
                hud.UnitInfo.Hide();
                Debug.Log("BATTLE_VISUAL_PLAY_PASS "+checks+" pooled-reuse-capacity-all-skills-projectiles-stun-icons-round-cleanup");
            }
            finally
            {
                hud.UnitInfo.Hide(); hud.enabled=hudEnabled; Time.timeScale=previousScale;
                await visuals.DisposeAsync(); await units.DisposeAsync();
                Expect(assets.ActiveHandleCount==0,"표현·유닛 핸들 해제");
                UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(phase.gameObject);
            }
        }
    }
}
#endif
