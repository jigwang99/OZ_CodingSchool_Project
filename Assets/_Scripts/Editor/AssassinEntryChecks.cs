#if UNITY_EDITOR
using System;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
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
    public static class AssassinEntryChecks
    {
        private static int checks;
        private static void Expect(bool value,string message)
        { if(!value) throw new Exception("Assassin entry check: "+message); checks++; }

        [MenuItem("PixelTactics/Checks/Run Assassin Entry Checks")]
        public static void BuildAndPlay()=>SynergyUIChecks.BuildAndPlay();

        private static void Place(Unit unit,int definitionId,int id,UnitTeam team,int col,int row,
            RunController run,BattleFlowController flow,int boss=0)
        {
            unit.gameObject.SetActive(false);
            var cell=HexCoord.FromOffset(col,row);
            unit.Configure(run.State.Data.Units[definitionId],1,id,team,run.State.Data,flow,run.Grid,run.Bench,
                team==UnitTeam.Enemy?cell:null,boss);
            var state=new UnitState(id); var board=new BoardState();
            Expect(board.TryRegister(state,cell),"검사 유닛 배치");
            unit.View.Bind(state,board); unit.transform.position=run.Grid.CellToWorld(cell);
            unit.gameObject.SetActive(true); unit.BeginBattle();
        }

        private static CombatBoard Board(params Unit[] units)
        {
            var board=new CombatBoard();
            foreach(var unit in units) Expect(board.TryAdd(unit.InstanceId,unit.CurrentCell.Value),"시작 점유 등록");
            return board;
        }

        public static async UniTask RunPlay(RunController run)
        {
            checks=0; float previousScale=Time.timeScale; Time.timeScale=1f;
            using var assets=new AssetService();
            var root=new GameObject("AssassinEntryProbes");
            var flowRoot=new GameObject("AssassinEntryFlow",typeof(BattleFlowController));
            var flow=flowRoot.GetComponent<BattleFlowController>();
            var pool=new UnitPrefabPool(assets,root.transform);
            Unit first=null,second=null,front=null,rear=null;
            using var lifetime=new CancellationTokenSource();
            try
            {
                foreach(int id in new[]{1006,1008,1001,1002})
                    await pool.PreloadAsync(run.Catalog.VisualPrefab(run.State.Data.Units[id].VisualId),lifetime.Token);
                first=pool.Rent(run.Catalog.VisualPrefab(run.State.Data.Units[1006].VisualId));
                second=pool.Rent(run.Catalog.VisualPrefab(run.State.Data.Units[1008].VisualId));
                front=pool.Rent(run.Catalog.VisualPrefab(run.State.Data.Units[1001].VisualId));
                rear=pool.Rent(run.Catalog.VisualPrefab(run.State.Data.Units[1002].VisualId));
                void Reset()
                {
                    Place(first,1006,810001,UnitTeam.Ally,1,2,run,flow);
                    Place(second,1008,810002,UnitTeam.Ally,1,4,run,flow,boss:1);
                    Place(front,1001,810003,UnitTeam.Enemy,4,3,run,flow);
                    Place(rear,1002,810004,UnitTeam.Enemy,6,3,run,flow);
                }
                Reset(); var units=new[]{rear,second,front,first};
                Expect(AssassinEntryPlanner.SelectTarget(first,units)==rear,"거리·역할보다 적 후방 우선");
                Place(front,1001,810003,UnitTeam.Enemy,6,4,run,flow);
                Expect(AssassinEntryPlanner.SelectTarget(first,units)==front,"후방 깊이 동률이면 역할 우선");
                Place(front,1002,810003,UnitTeam.Enemy,6,4,run,flow);
                Expect(AssassinEntryPlanner.SelectTarget(first,units)==front,"역할도 동률이면 낮은 ID 우선");
                front.Health.TakeDamage(int.MaxValue);
                Expect(AssassinEntryPlanner.SelectTarget(first,units)==rear,"사망 대상 제외");
                Reset(); var board=Board(units);
                var from=first.CurrentCell.Value;
                Expect(AssassinEntryPlanner.TryChooseLanding(first,rear,board,out var landing)
                    && landing==HexCoord.FromOffset(7,2),"후방 인접 칸과 Offset 순서 선택");
                Expect(!board.TryReserve(first.InstanceId,from,landing),"일반 이동은 비인접 예약 금지");
                Expect(board.TryReserveJump(first.InstanceId,from,landing)
                    && !board.IsFree(from,int.MaxValue) && !board.IsFree(landing,int.MaxValue),"도약은 시작·목적지 동시 점유");
                Expect(!board.TryReserveJump(second.InstanceId,second.CurrentCell.Value,landing),"다른 암살자의 목적지 중복 예약 금지");
                Expect(AssassinEntryPlanner.TryChooseLanding(second,rear,board,out var secondLanding)
                    && secondLanding==HexCoord.FromOffset(7,3),"다음 암살자는 예약을 피한 다음 칸 선택");
                Expect(!board.TryReserveJump(first.InstanceId,from,rear.CurrentCell.Value)
                    && !board.TryReserveJump(first.InstanceId,from,HexCoord.FromOffset(8,0)),"적 점유·보드 외부 착지 금지");
                board.CancelReservation(first.InstanceId,landing);
                var dense=new CombatBoard(); var fallback=HexCoord.FromOffset(3,6);
                for(int col=0;col<HexGridLayout.Columns;col++)
                    for(int row=0;row<HexGridLayout.Rows;row++)
                    {
                        var cell=HexCoord.FromOffset(col,row);
                        if(cell!=fallback) dense.TryAdd(cell==from?first.InstanceId:900000+col*7+row,cell);
                    }
                Expect(AssassinEntryPlanner.TryChooseLanding(first,rear,dense,out var only) && only==fallback,"인접 칸이 없으면 비인접 빈칸 사용");
                dense.TryAdd(999999,fallback);
                Expect(!AssassinEntryPlanner.TryChooseLanding(first,rear,dense,out var stay) && stay==from,"빈칸이 없으면 원래 칸 유지");

                Expect(flow.TryStartCombat(),"검사 전투 시작");
                Reset(); var controller=new AutoBattleController(units);
                int jumps=0; first.Move.MoveStarted+=position=>{ if(first.Move.IsJumping) jumps++; };
                second.Move.MoveStarted+=position=>{ if(second.Move.IsJumping) jumps++; };
                using(var combat=new CancellationTokenSource())
                {
                    var battle=controller.RunAsync(60f,combat.Token);
                    var firstBoard=controller.Trees[0].Blackboard.Board;
                    Expect(first.Move.IsJumping && second.Move.IsJumping && !front.Move.IsMoving
                        && !rear.Move.IsMoving,"양 팀 시작 도약 동안 일반 행동 대기");
                    Expect(!first.Combat.CanAttack && !second.Skill.CanCast && first.CurrentCell==from,"도약 중 공격·보스 스킬 차단과 출발 좌표 유지");
                    await UniTask.Delay(100);
                    float progress=(first.transform.position.x-run.Grid.CellToWorld(from).x)
                        /(run.Grid.CellToWorld(landing).x-run.Grid.CellToWorld(from).x);
                    Expect(first.Move.IsJumping && first.transform.position.y
                        >Vector3.Lerp(run.Grid.CellToWorld(from),run.Grid.CellToWorld(landing),progress).y+.05f,"도약 포물선 표현");
                    await UniTask.WaitUntil(()=>!first.Move.IsJumping && !second.Move.IsJumping).Timeout(TimeSpan.FromSeconds(3));
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                    Expect(first.CurrentCell==landing && second.CurrentCell==secondLanding
                        && firstBoard.IsFree(from,int.MaxValue) && !firstBoard.IsFree(landing,int.MaxValue),"고정 ID 순서 착지와 점유 이전");
                    Expect(controller.Trees[0].Blackboard.Target==rear && controller.Trees[1].Blackboard.Target==rear
                        && (first.Combat.IsAttacking || first.Skill.IsCasting),"착지 후 후방 대상에 기존 행동트리 연결");
                    Expect(first.View.State.Cell==from,"도약으로 준비 배치 변경 없음");
                    await UniTask.Delay(200);
                    Expect(jumps==2,"전투 시작에 각 암살자 한 번만 도약");
                    combat.Cancel();
                    try { await battle; } catch(OperationCanceledException) { }
                }
                await UniTask.DelayFrame(2);
                first.RestorePreparation();
                Expect(first.CurrentCell==from && first.transform.position==run.Grid.CellToWorld(from)
                    && !first.Move.IsJumping,"준비 복원 시 원래 배치와 위치 복구");
                Reset(); var canceled=new AutoBattleController(units);
                using(var combat=new CancellationTokenSource())
                {
                    var battle=canceled.RunAsync(60f,combat.Token); combat.Cancel();
                    try { await battle; } catch(OperationCanceledException) { }
                    Expect(first.CurrentCell==from && first.transform.position==run.Grid.CellToWorld(from)
                        && !first.Move.IsMoving && canceled.Trees[0].Blackboard.Board.IsFree(landing,int.MaxValue),"도약 취소는 출발 칸 복귀와 예약 해제");
                }
                Reset(); var stunned=new AutoBattleController(units);
                var shortBattle=stunned.RunAsync(.01f,lifetime.Token);
                first.Effects.Apply(new EffectDefinition(99001,EffectKind.Stun,1f,1f),new ModifierSource(ModifierSourceKind.Skill,99001,rear.InstanceId));
                await shortBattle;
                Expect(first.CurrentCell==from && !first.Move.IsMoving && stunned.Trees[0].Blackboard.Board.IsFree(landing,int.MaxValue),"기절로 도약 중단 시 예약 누수 없음");
                Reset(); var killed=new AutoBattleController(units);
                var deathBattle=killed.RunAsync(.01f,lifetime.Token);
                first.Health.TakeDamage(int.MaxValue); await deathBattle;
                Expect(killed.Trees[0].Blackboard.Board.IsFree(from,int.MaxValue)
                    && killed.Trees[0].Blackboard.Board.IsFree(landing,int.MaxValue),"도약 중 사망은 출발·목적지 점유 해제");

                // 적 암살자도 반대 방향의 아군 후방으로 도약.
                Place(first,1006,810001,UnitTeam.Enemy,6,2,run,flow);
                Place(second,1001,810002,UnitTeam.Ally,1,3,run,flow);
                Place(front,1002,810003,UnitTeam.Ally,2,4,run,flow);
                Place(rear,1002,810004,UnitTeam.Ally,3,3,run,flow);
                Expect(AssassinEntryPlanner.SelectTarget(first,units)==second,"적 암살자는 아군 왼쪽 후방 선택");
                var reverse=new AutoBattleController(units); await reverse.RunAsync(.01f,lifetime.Token);
                Expect(first.CurrentCell==HexCoord.FromOffset(0,3),"적 암살자의 반대 방향 실제 착지");
                first.RestorePreparation();
                Expect(first.Move.IsMoving==false && first.Move.IsJumping==false,"준비 복원 시 도약 상태 초기화");

                // 이전 이동의 finally가 같은 풀 인스턴스의 다음 이동을 덮어쓰지 않는지 확인.
                Reset(); var oldJump=first.Move.JumpToCellAsync(landing,lifetime.Token);
                pool.Return(first);
                first=pool.Rent(run.Catalog.VisualPrefab(run.State.Data.Units[1006].VisualId));
                Place(first,1006,820001,UnitTeam.Ally,2,2,run,flow);
                var newDestination=HexCoord.FromOffset(7,5);
                var newJump=first.Move.JumpToCellAsync(newDestination,lifetime.Token);
                Expect(!await oldJump && first.Move.IsJumping && first.CurrentCell==HexCoord.FromOffset(2,2),"풀 재사용 후 이전 작업이 새 이동 상태를 해제하지 않음");
                Expect(await newJump && first.CurrentCell==newDestination && !first.Move.IsMoving,"풀 재사용 후 새 도약 정상 완료");
            }
            finally
            {
                lifetime.Cancel();
                foreach(var unit in new[]{first,second,front,rear}) if(unit!=null) { unit.Move.Stop(); unit.Combat.ResetForRound(); pool.Return(unit); }
                await UniTask.DelayFrame(2); await pool.DisposeAsync();
                UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(flowRoot); Time.timeScale=previousScale;
            }
            Expect(assets.ActiveHandleCount==0,"도약 검사 풀·Addressables 정리");
            Debug.Log("ASSASSIN_ENTRY_PLAY_PASS "+checks+" rear-ties-reserve-fallback-both-teams-arc-action-lock-cancel-stun-death-pool-once");
        }
    }
}
#endif
