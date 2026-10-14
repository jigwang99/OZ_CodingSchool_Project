#if UNITY_EDITOR
using System;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Battle.AI;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Units;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    public static class BehaviourTreeChecks
    {
        private static int checks;
        private static void Expect(bool value,string message)
        { if(!value) throw new Exception("Behaviour tree check: "+message); checks++; }

        [MenuItem("PixelTactics/Checks/Run Behaviour Tree Domain Checks")]
        public static void RunDomain()
        {
            checks=0;
            int reached=0;
            var failure=new ConditionNode(()=>false);
            var success=new ConditionNode(()=>true);
            var running=new ActionNode(()=>BehaviourStatus.Running);
            var last=new ActionNode(()=>{ reached++; return BehaviourStatus.Success; });
            Expect(failure.Tick()==BehaviourStatus.Failure && success.Tick()==BehaviourStatus.Success,"조건 결과");
            Expect(new SequenceNode(failure,last).Tick()==BehaviourStatus.Failure && reached==0,"순차 실패 시 중단");
            Expect(new SequenceNode(running,last).Tick()==BehaviourStatus.Running && reached==0,"순차 실행 중 하위 중단");
            Expect(new SequenceNode(success,last).Tick()==BehaviourStatus.Success && reached==1,"순차 성공 시 다음 행동");
            reached=0;
            Expect(new SelectorNode(success,last).Tick()==BehaviourStatus.Success && reached==0,"우선 선택 성공 시 중단");
            Expect(new SelectorNode(running,last).Tick()==BehaviourStatus.Running && reached==0,"우선 선택 실행 중 하위 중단");
            Expect(new SelectorNode(failure,last).Tick()==BehaviourStatus.Success && reached==1,"우선 선택 실패 시 다음 후보");
            Expect(new SelectorNode(failure,failure).Tick()==BehaviourStatus.Failure,"모든 후보 실패");
            bool stunned=false; int attacks=0;
            var reactive=new SelectorNode(new SequenceNode(new ConditionNode(()=>stunned),running),
                new ActionNode(()=>{ attacks++; return BehaviourStatus.Running; }));
            reactive.Tick(); stunned=true; reactive.Tick();
            Expect(attacks==1 && reactive.LastStatus==BehaviourStatus.Running,"실행 중에도 우선 조건 재평가");
            stunned=false; reactive.Tick(); Expect(attacks==2,"우선 조건 해제 뒤 하위 복귀");
            var children=new BehaviourNode[] {success}; var copied=new SequenceNode(children);
            children[0]=failure; Expect(copied.Tick()==BehaviourStatus.Success,"외부 배열 변경으로 트리 손상 방지");
            Debug.Log("BEHAVIOUR_TREE_DOMAIN_PASS "+checks);
        }

        public static async UniTask RunPlay(RunController run)
        {
            using var assets=new AssetService();
            var root=new GameObject("BehaviourTreeProbes");
            var flowRoot=new GameObject("BehaviourTreeProbeFlow",typeof(BattleFlowController));
            var flow=flowRoot.GetComponent<BattleFlowController>();
            var pool=new UnitPrefabPool(assets,root.transform);
            var definition=run.State.Data.Units.Values.First(x=>x.Range==1);
            var reference=run.Catalog.VisualPrefab(definition.VisualId);
            await pool.PreloadAsync(reference,CancellationToken.None);
            var ally=pool.Rent(reference); var enemy=pool.Rent(reference); var other=pool.Rent(reference);
            using var lifetime=new CancellationTokenSource();
            try
            {
                var board=new BoardState(); var placement=new PlacementService(board);
                var start=HexCoord.FromOffset(1,3); var state=new UnitState(710001);
                Expect(placement.TryRegisterUnit(state,UnitLocation.OnBoard(start)),"검사 배치");
                ally.Configure(definition,1,state.InstanceId,UnitTeam.Ally,run.State.Data,flow,run.Grid,run.Bench);
                ally.View.Bind(state,board); ally.transform.position=run.Grid.CellToWorld(start);
                var adjacent=start.GetNeighbor(0);
                enemy.Configure(definition,1,710002,UnitTeam.Enemy,run.State.Data,flow,run.Grid,run.Bench,adjacent);
                other.Configure(definition,1,710003,UnitTeam.Enemy,run.State.Data,flow,run.Grid,run.Bench,start.GetNeighbor(1));
                foreach(var unit in new[]{ally,enemy,other}) { unit.gameObject.SetActive(true); unit.BeginBattle(); }
                Expect(flow.TryStartCombat(),"검사 전투 시작");
                var controller=new AutoBattleController(new[]{other,enemy,ally});
                var tree=controller.Trees[0]; var blackboard=tree.Blackboard;
                Expect(blackboard.Unit==ally && controller.Trees[1].Blackboard.Unit==enemy,"입력 순서와 무관한 ID 순서");
                int attacks=0; ally.Combat.AttackStarted+=target=>attacks++;
                Expect(tree.Tick(lifetime.Token)==BehaviourStatus.Running && blackboard.Target==enemy,"거리·역할 동률이면 낮은 ID 대상");
                tree.Tick(lifetime.Token); Expect(attacks==1 && blackboard.Action==UnitBehaviourAction.Attacking,"반복 Tick 공격 중복 방지");
                var source=new ModifierSource(ModifierSourceKind.Skill,9901,enemy.InstanceId);
                var stun=ally.Effects.Apply(new EffectDefinition(9901,EffectKind.Stun,1f,10f),source);
                tree.Tick(lifetime.Token);
                Expect(blackboard.Action==UnitBehaviourAction.Stunned && attacks==1,"기절 우선 선택");
                await UniTask.WaitUntil(()=>!ally.Combat.IsAttacking).Timeout(TimeSpan.FromSeconds(3));
                ally.Effects.Remove(stun); ally.Combat.ResetForRound(); enemy.Health.TakeDamage(int.MaxValue);
                tree.Tick(lifetime.Token); Expect(blackboard.Target==other,"사망한 대상 교체");
                var enemyTree=controller.Trees[1]; int revision=blackboard.Board.Revision;
                enemyTree.Tick(lifetime.Token); enemyTree.Tick(lifetime.Token);
                Expect(blackboard.Board.IsFree(adjacent,int.MaxValue) && blackboard.Board.Revision==revision+1,"사망 점유 한 번 해제");
                await UniTask.WaitUntil(()=>!ally.Combat.IsAttacking).Timeout(TimeSpan.FromSeconds(3));
                other.Health.TakeDamage(int.MaxValue); tree.Tick(lifetime.Token);
                Expect(blackboard.Target==null && blackboard.Action==UnitBehaviourAction.WaitingForTarget,"대상이 없으면 대기");
                tree.Tick(lifetime.Token); Expect(blackboard.Action==UnitBehaviourAction.WaitingToRetry,"재시도 간격 유지");

                // 새 전투 보드로 이동 예약과 취소 검사.
                ally.RestorePreparation(); ally.BeginBattle(); ally.Combat.ResetForRound();
                enemy.Configure(definition,1,enemy.InstanceId,UnitTeam.Enemy,run.State.Data,flow,run.Grid,run.Bench,HexCoord.FromOffset(6,3));
                enemy.BeginBattle();
                var movement=new AutoBattleController(new[]{ally,enemy}).Trees[0];
                int moves=0; ally.Move.MoveStarted+=position=>moves++;
                movement.Tick(lifetime.Token); movement.Tick(lifetime.Token);
                Expect(ally.Move.IsMoving && moves==1 && movement.Blackboard.Action==UnitBehaviourAction.Moving,"사거리 밖 이동과 중복 요청 방지");
                var path=HexPathfinder.Find(new CombatBoard(),ally.InstanceId,start,HexCoord.FromOffset(6,3),1);
                HexCoord next=path[0];
                Expect(!movement.Blackboard.Board.IsFree(next,int.MaxValue),"이동 목적지 선점");
                ally.Move.Stop(); await UniTask.WaitUntil(()=>!ally.Move.IsMoving); await UniTask.Yield();
                Expect(ally.CurrentCell==start && movement.Blackboard.Board.IsFree(next,int.MaxValue),"취소 시 셀 복귀와 예약 해제");
                movement.Tick(lifetime.Token);
                await UniTask.WaitUntil(()=>!ally.Move.IsMoving).Timeout(TimeSpan.FromSeconds(3)); await UniTask.Yield();
                Expect(ally.CurrentCell==next && movement.Blackboard.Board.IsFree(start,int.MaxValue)
                    && !movement.Blackboard.Board.IsFree(next,int.MaxValue),"도착 시 점유 이전");
                lifetime.Cancel(); bool canceled=false;
                try { movement.Tick(lifetime.Token); } catch(OperationCanceledException) { canceled=true; }
                Expect(canceled && moves==2,"전투 취소 뒤 새 행동 차단");
            }
            finally
            {
                lifetime.Cancel(); ally.Move.Stop();
                pool.Return(ally); pool.Return(enemy); pool.Return(other);
                await pool.DisposeAsync(); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(flowRoot);
            }
            Expect(assets.ActiveHandleCount==0,"검사 핸들 해제");
            Debug.Log("BEHAVIOUR_TREE_PLAY_PASS target-attack-stun-retarget-death-wait-move-reserve-cancel");
        }

        public static void BuildAndPlay()
        {
            RunDomain(); UnitEffectsChecks.RunDomain(); MilestoneChecks.RunAll(); CharacterPrefabSetup.BuildForVerification();
        }
    }
}
#endif
