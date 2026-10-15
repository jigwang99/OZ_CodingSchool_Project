#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Snapshot;
using AutoBattler.Data.Units;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    public static class DamageEventChecks
    {
        private static int checks;
        private static void Expect(bool condition,string message)
        { if(!condition) throw new Exception("Damage event check: "+message); checks++; }
        private static void Configure(Unit unit,int id,UnitTeam team,HexCoord cell,RunController run,BattleFlowController flow,int hp=1000,int definitionId=1002)
        {
            unit.Configure(run.State.Data.Units[definitionId],1,id,team,run.State.Data,flow,run.Grid,run.Bench,team==UnitTeam.Enemy?cell:null);
            if(team==UnitTeam.Ally)
            {
                var board=new BoardState(); var placement=new PlacementService(board); var state=new UnitState(id);
                placement.TryRegisterUnit(state,UnitLocation.OnBoard(cell)); unit.View.Bind(state,board);
            }
            unit.transform.position=run.Grid.CellToWorld(cell); unit.gameObject.SetActive(true); unit.BeginBattle();
            unit.Health.Initialize(hp);
        }
        public static async UniTask RunPlay(RunController run)
        {
            using var assets=new AssetService(); using var lifetime=new CancellationTokenSource();
            var root=new GameObject("DamageEventProbes"); var flowRoot=new GameObject("DamageEventFlow",typeof(BattleFlowController));
            var flow=flowRoot.GetComponent<BattleFlowController>(); var pool=new UnitPrefabPool(assets,root.transform);
            var prefab=run.Catalog.VisualPrefab(run.State.Data.Units[1002].VisualId);
            Unit attacker=null,second=null,target=null;
            try
            {
                checks=0; await pool.PreloadAsync(prefab,lifetime.Token);
                attacker=pool.Rent(prefab); second=pool.Rent(prefab); target=pool.Rent(prefab);
                var cell=HexCoord.FromOffset(2,3); var targetCell=cell.GetNeighbor(0); flow.TryStartCombat();
                Action reset=()=>
                {
                    Configure(attacker,770001,UnitTeam.Ally,cell,run,flow);
                    Configure(second,770003,UnitTeam.Ally,cell.GetNeighbor(1),run,flow);
                    Configure(target,770002,UnitTeam.Enemy,targetCell,run,flow);
                };
                reset();
                var received=new List<DamageResult>(); var dealt=new List<DamageResult>();
                var deaths=new List<KillEvent>(); var kills=new List<KillEvent>(); var secondKills=new List<KillEvent>();
                int died=0,legacyDamage=0;
                target.Health.DamageResolved+=received.Add; target.Health.DeathResolved+=deaths.Add;
                attacker.Combat.DamageDealt+=dealt.Add; attacker.Combat.Killed+=kills.Add; second.Combat.Killed+=secondKills.Add;
                target.Health.Died+=()=>died++; target.Health.DamageTaken+=amount=>legacyDamage+=amount;
                var source=DamageContext.BasicAttack(attacker,true);
                int lost=target.Health.TakeDamage(source,100,1f,1f);
                Expect(lost==100 && received.Count==1 && dealt.Count==1,"기본 공격 출처와 한 번의 피해 결과");
                var hit=received[0];
                Expect(hit.Context.Attacker.InstanceId==770001 && hit.Target.InstanceId==770002
                    && hit.Context.Attacker.DefinitionId==1002 && hit.Context.Attacker.OriginId==2 && hit.Context.Attacker.ClassId==3,"정의·계열·직업·인스턴스 정보 고정");
                Expect(hit.Context.Kind==DamageKind.BasicAttack && hit.Context.SourceId==0 && hit.Context.IsCritical,"기본 공격·치명타 메타데이터");
                Expect(hit.HealthDamage==100 && hit.ShieldDamage==0 && hit.ResolvedDamage==100 && !hit.IsLethal && kills.Count==0,"비치명 피해는 처치 없음");
                Expect(target.Mana.Current==10 && legacyDamage==100,"기존 피격 MP와 HP 피해 이벤트 유지");

                var shieldSource=new ModifierSource(ModifierSourceKind.System,770004);
                target.Effects.Apply(new EffectDefinition(770004,EffectKind.Shield,80f,10f),shieldSource);
                target.Health.TakeDamage(DamageContext.Skill(attacker,2002),50,1f,1f);
                hit=received[received.Count-1];
                Expect(hit.ShieldDamage==50 && hit.HealthDamage==0 && hit.Context.SourceId==2002 && target.Mana.Current==10,"보호막만 감소한 스킬 피해와 MP 유지");
                target.Health.TakeDamage(DamageContext.Effect(second,3005),50,1f,1f);
                hit=received[received.Count-1];
                Expect(hit.ShieldDamage==30 && hit.HealthDamage==20 && hit.Context.Kind==DamageKind.Effect && hit.Context.Attacker.InstanceId==770003,"효과 출처와 보호막·체력 분리");
                int count=received.Count; target.Health.TakeDamage(source,0); target.Health.TakeDamage(source,-1);
                target.Health.Heal(10);
                Expect(received.Count==count,"0 피해·음수 피해·회복에는 피해 결과 없음");

                // 먼저 때린 공격자가 아니라 치명 피해를 준 유닛에 귀속.
                target.Health.TakeDamage(DamageContext.Skill(second,2008),int.MaxValue);
                Expect(died==1 && deaths.Count==1 && secondKills.Count==1 && kills.Count==0,"마지막 유효 피해의 처치자 한 번 확정");
                var death=deaths[0];
                Expect(death.IsCredited && death.Killer.InstanceId==770003 && death.Victim.InstanceId==770002
                    && death.Damage.Context.SourceId==2008 && death.Damage.IsLethal && death.Damage.OverkillDamage>0,"처치 결과의 원인·초과 피해 고정");
                target.Health.TakeDamage(source,100);
                Expect(died==1 && deaths.Count==1 && secondKills.Count==1,"시체 추가 타격에 중복 처치 없음");
                Expect(target.Mana.Current==20,"사망 피해에는 피격 MP 지급 없음");

                reset(); target.Health.TakeDamage(source,10); target.Health.TakeDamage(int.MaxValue);
                Expect(deaths.Count==2 && !deaths[1].IsCredited && deaths[1].Damage.Context.Kind==DamageKind.Environment
                    && kills.Count==0 && secondKills.Count==1,"출처 없는 치명 피해를 이전 공격자에게 귀속하지 않음");
                reset(); target.Health.TakeDamage(DamageContext.BasicAttack(attacker),100,1f,1f);
                target.Health.TakeDamage(DamageContext.BasicAttack(attacker),int.MaxValue);
                Expect(kills.Count==1,"새 수명에서는 처치 이벤트 다시 한 번 허용");
                reset(); target.Health.TakeDamage(DamageContext.BasicAttack(target),int.MaxValue);
                Expect(!deaths[deaths.Count-1].IsCredited,"자기 피해에 처치 보상 없음");

                reset(); Configure(second,770003,UnitTeam.Enemy,cell.GetNeighbor(1),run,flow);
                target.Health.TakeDamage(DamageContext.Effect(second,3005),int.MaxValue);
                Expect(!deaths[deaths.Count-1].IsCredited && secondKills.Count==1,"같은 팀 피해에 처치 보상 없음");

                reset(); count=received.Count; int before=dealt.Count;
                await attacker.Combat.AttackAsync(target.Combat,lifetime.Token);
                Expect(received.Count==count+1 && dealt.Count==before+1 && received[received.Count-1].Context.Kind==DamageKind.BasicAttack
                    && received[received.Count-1].Context.Attacker.InstanceId==attacker.InstanceId,"실제 기본 공격 경로 출처 전달");
                reset(); attacker.Mana.Add(100); count=received.Count;
                Expect(attacker.Skill.TryPrepare(new[]{attacker,target},target,out var cast),"실제 스킬 시전 준비");
                await attacker.Skill.CastAsync(cast,lifetime.Token);
                Expect(received.Count==count+1 && received[received.Count-1].Context.Kind==DamageKind.Skill
                    && received[received.Count-1].Context.SourceId==2002,"실제 스킬 경로 출처 전달");

                reset(); Configure(attacker,770001,UnitTeam.Ally,cell,run,flow,definitionId:1007);
                target.Health.Initialize(1); Configure(second,770003,UnitTeam.Enemy,targetCell.GetNeighbor(1),run,flow,hp:1);
                attacker.Mana.Add(100); int aoeKills=kills.Count;
                Expect(attacker.Skill.TryPrepare(new[]{attacker,target,second},target,out cast),"범위 스킬 시전 준비");
                await attacker.Skill.CastAsync(cast,lifetime.Token);
                Expect(kills.Count==aoeKills+2 && kills[kills.Count-1].Damage.Context.SourceId==2007
                    && kills[kills.Count-2].Victim.InstanceId!=kills[kills.Count-1].Victim.InstanceId,"범위 스킬의 대상별 독립 처치와 공통 출처");

                // 발사된 스킬은 시전자 사망 후 도착해도 원래 신원에 귀속.
                reset(); target.Health.Initialize(1); attacker.Mana.Add(100);
                attacker.Skill.TryPrepare(new[]{attacker,target},target,out cast);
                Action<AutoBattler.Battle.Skills.SkillCastContext> onRelease=context=>attacker.Health.TakeDamage(int.MaxValue);
                attacker.Skill.EffectReleased+=onRelease; int previousKills=kills.Count;
                await attacker.Skill.CastAsync(cast,lifetime.Token); attacker.Skill.EffectReleased-=onRelease;
                Expect(!attacker.Health.IsAlive && !target.Health.IsAlive && kills.Count==previousKills+1
                    && kills[kills.Count-1].Killer.InstanceId==770001,"발사 후 시전자 사망에도 처치 출처 유지");

                reset(); var oldSource=DamageContext.Skill(attacker,2002); var originalReference=oldSource.Attacker;
                attacker.gameObject.SetActive(false); Configure(attacker,770099,UnitTeam.Enemy,cell,run,flow);
                previousKills=kills.Count; before=dealt.Count;
                target.Health.TakeDamage(oldSource,int.MaxValue);
                Expect(deaths[deaths.Count-1].Killer.InstanceId==770001 && deaths[deaths.Count-1].Killer.Team==UnitTeam.Ally
                    && !originalReference.IsCurrent && originalReference.Unit==null,"재사용 이후에도 이전 출처 메타데이터 보존");
                Expect(kills.Count==previousKills && dealt.Count==before,"새 유닛에 이전 출처의 피해·처치 이벤트 전달 방지");

                reset(); var order=new List<string>();
                Action<int> hpChanged=amount=>order.Add("hp"); Action<DamageResult> resolved=result=>order.Add("damage");
                Action onDied=()=>order.Add("died"); Action<KillEvent> onDeath=info=>order.Add("death");
                Action<DamageResult> onDealt=result=>order.Add("dealt"); Action<KillEvent> onKill=info=>order.Add("kill");
                target.Health.DamageTaken+=hpChanged; target.Health.DamageResolved+=resolved; target.Health.Died+=onDied;
                target.Health.DeathResolved+=onDeath; attacker.Combat.DamageDealt+=onDealt; attacker.Combat.Killed+=onKill;
                target.Health.TakeDamage(DamageContext.BasicAttack(attacker),int.MaxValue);
                Expect(string.Join(",",order)=="hp,damage,died,death,dealt,kill","피격·피해·사망·처치 이벤트 순서 고정");
                target.Health.DamageTaken-=hpChanged; target.Health.DamageResolved-=resolved; target.Health.Died-=onDied;
                target.Health.DeathResolved-=onDeath; attacker.Combat.DamageDealt-=onDealt; attacker.Combat.Killed-=onKill;

                reset(); previousKills=kills.Count; int reentrant=-1;
                Action<DamageResult> nested=result=>reentrant=target.Health.TakeDamage(DamageContext.Skill(second,2002),int.MaxValue);
                target.Health.DamageResolved+=nested; target.Health.TakeDamage(DamageContext.BasicAttack(attacker),int.MaxValue);
                target.Health.DamageResolved-=nested;
                Expect(reentrant==0 && kills.Count==previousKills+1,"같은 대상의 피해 콜백 재진입에 중복 처치 없음");

                reset(); var oldVictim=new CombatUnitReference(target); var oldAttacker=new CombatUnitReference(attacker);
                Action returnVictim=()=>
                {
                    target.gameObject.SetActive(false); Configure(target,770088,UnitTeam.Enemy,targetCell,run,flow);
                };
                target.Health.Died+=returnVictim; previousKills=kills.Count;
                target.Health.TakeDamage(DamageContext.BasicAttack(attacker),int.MaxValue); target.Health.Died-=returnVictim;
                Expect(target.Health.IsAlive && target.InstanceId==770088 && !oldVictim.IsCurrent && kills.Count==previousKills+1
                    && kills[kills.Count-1].Victim.InstanceId==770002,"사망 중 대상 재사용이 새 생명으로 사건을 덮어쓰지 않음");
                attacker.Health.RestoreFullHealth();
                Expect(!oldAttacker.IsCurrent,"라운드 복원 이후 이전 전투 참조 무효");

                // 기본 투사체가 이전 대상의 재사용된 생명에 명중하지 않도록 확인.
                reset(); before=dealt.Count; int hp=0;
                var attack=attacker.Combat.AttackAsync(target.Combat,lifetime.Token);
                await UniTask.Delay(TimeSpan.FromSeconds(attacker.WindupSeconds+.02f));
                Configure(target,770002,UnitTeam.Enemy,targetCell,run,flow); hp=target.Health.CurrentHp;
                await attack;
                Expect(target.Health.CurrentHp==hp && dealt.Count==before,"기본 투사체의 대상 수명 고정");

                reset(); previousKills=kills.Count;
                Action<DamageResult> failListener=result=>throw new InvalidOperationException("DamageEvents.ExpectedListenerFailure");
                target.Health.DamageResolved+=failListener;
                target.Health.TakeDamage(DamageContext.Skill(attacker,2002),int.MaxValue);
                target.Health.DamageResolved-=failListener;
                Expect(!target.Health.IsAlive && kills.Count==previousKills+1,"피해 구독자 예외에도 사망·처치 진행");
                Debug.Log("DAMAGE_EVENTS_PLAY_PASS "+checks+" source-shield-hp-last-hit-once-environment-teams-projectile-pool-life-order-reentry");
            }
            finally
            {
                lifetime.Cancel(); if(attacker!=null) pool.Return(attacker); if(second!=null) pool.Return(second); if(target!=null) pool.Return(target);
                await pool.DisposeAsync(); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(flowRoot);
            }
        }
        [MenuItem("PixelTactics/Checks/Run Damage Event Checks")]
        public static void BuildAndPlay()=>SkillChecks.BuildAndPlay();
    }
}
#endif
