#if UNITY_EDITOR
using System;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Battle.Units;
using AutoBattler.Presentation.Units;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    /// <summary>실제 프리팹의 이동·공격 이벤트, 방향 복원과 UI 보존 확인.</summary>
    public static class UnitFacingPlayChecks
    {
        private static void Expect(bool condition,string message)
        { if(!condition) throw new Exception("Facing check: "+message); }
        public static async UniTask Run(RunController run)
        {
            using var assets=new AssetService();
            var parent=new GameObject("FacingProbes");
            var pool=new UnitPrefabPool(assets,parent.transform);
            var flowRoot=new GameObject("FacingProbeFlow",typeof(BattleFlowController));
            var flow=flowRoot.GetComponent<BattleFlowController>();
            try
            {
                foreach(var definition in run.State.Data.Units.Values)
                {
                    var prefab=run.Catalog.VisualPrefab(definition.VisualId);
                    await pool.PreloadAsync(prefab,CancellationToken.None);
                    var ally=pool.Rent(prefab);
                    var enemy=pool.Rent(prefab);
                    HexCoord start=HexCoord.FromOffset(2,3);
                    var board=new BoardState();
                    var placement=new PlacementService(board);
                    var state=new UnitState(900000+definition.VisualId);
                    Expect(placement.TryRegisterUnit(state,UnitLocation.OnBoard(start)),"아군 검사 좌표 등록");
                    ally.Configure(definition,1,state.InstanceId,UnitTeam.Ally,run.State.Data,flow,run.Grid,run.Bench);
                    ally.View.Bind(state,board); ally.transform.position=run.Grid.CellToWorld(start);
                    enemy.Configure(definition,1,910000+definition.VisualId,UnitTeam.Enemy,run.State.Data,flow,run.Grid,run.Bench,start.GetNeighbor(0));
                    ally.gameObject.SetActive(true); enemy.gameObject.SetActive(true);
                    var facing=ally.GetComponent<UnitFacingView>();
                    var enemyFacing=enemy.GetComponent<UnitFacingView>();
                    Expect(facing!=null && facing.SpriteRenderers.Count>0,"외형 렌더러 연결");
                    // PIXEM 원본의 왼쪽 기본 방향을 기준으로 기대값 계산.
                    var source=ProjectSetupSettings.Current.Unit(definition.VisualId).SourcePrefab;
                    var rightFlips=source.GetComponentsInChildren<SpriteRenderer>(true)
                        .Select(renderer=>!renderer.flipX).ToArray();
                    ExpectFacing(facing,true,rightFlips);
                    ExpectFacing(enemyFacing,false,rightFlips);
                    var rootScale=ally.transform.localScale;
                    var canvas=ally.GetComponentInChildren<UnitHealthBar>(true).HealthCanvas.transform;
                    var canvasScale=canvas.localScale;
                    var collider=ally.GetComponent<Collider2D>();
                    var colliderScale=collider.transform.localScale;
                    ally.BeginBattle(); enemy.BeginBattle();
                    Expect(flow.TryStartCombat(),"검사 전투 시작");

                    // 실제 이동 요청 직후, 첫 이동 프레임부터 목적지를 바라봄.
                    HexCoord left=Neighbor(run,ally.CurrentCell.Value,-1);
                    var movement=ally.Move.MoveToCellAsync(left);
                    ExpectFacing(facing,false,rightFlips);
                    Expect(await movement,"왼쪽 이동 완료");
                    HexCoord vertical=Neighbor(run,ally.CurrentCell.Value,0);
                    Expect(await ally.Move.MoveToCellAsync(vertical),"수직 이동 완료");
                    ExpectFacing(facing,false,rightFlips);
                    HexCoord right=Neighbor(run,ally.CurrentCell.Value,1);
                    Expect(await ally.Move.MoveToCellAsync(right),"오른쪽 이동 완료");
                    ExpectFacing(facing,true,rightFlips);

                    foreach(int side in new[] {-1,1})
                    {
                        HexCoord target=Neighbor(run,ally.CurrentCell.Value,side);
                        enemy.Configure(definition,1,enemy.InstanceId,UnitTeam.Enemy,run.State.Data,flow,run.Grid,run.Bench,target);
                        enemy.transform.position=run.Grid.CellToWorld(target);
                        ally.Combat.ResetForRound();
                        using var cancel=new CancellationTokenSource();
                        var attack=ally.Combat.AttackAsync(enemy.Combat,cancel.Token);
                        Expect(ally.Combat.IsAttacking,"실제 공격 시작");
                        ExpectFacing(facing,side>0,rightFlips);
                        cancel.Cancel(); await attack;
                        await UniTask.DelayFrame(2);
                        ExpectFacing(facing,side>0,rightFlips);
                    }
                    Expect(ally.transform.localScale==rootScale && canvas.localScale==canvasScale
                        && collider.transform.localScale==colliderScale,"루트·체력바·콜라이더 반전 없음");
                    ally.Health.TakeDamage(int.MaxValue);
                    facing.FacePosition(ally.transform.position+Vector3.left);
                    ExpectFacing(facing,true,rightFlips);
                    Expect(flow.TryFinishCombat() && flow.TryBeginPreparation(),"다음 준비 단계 전환");
                    ExpectFacing(facing,true,rightFlips);
                    ExpectFacing(enemyFacing,false,rightFlips);

                    // 풀에서 같은 기물을 적군으로 재사용해도 이전 방향을 유지하지 않음.
                    pool.Return(ally);
                    var reused=pool.Rent(prefab);
                    Expect(reused==ally,"검사 기물 풀 재사용");
                    reused.Configure(definition,1,920000+definition.VisualId,UnitTeam.Enemy,run.State.Data,flow,run.Grid,run.Bench,start);
                    reused.gameObject.SetActive(true);
                    ExpectFacing(reused.GetComponent<UnitFacingView>(),false,rightFlips);
                    pool.Return(reused); pool.Return(enemy);
                }
            }
            finally
            {
                await pool.DisposeAsync();
                UnityEngine.Object.Destroy(parent); UnityEngine.Object.Destroy(flowRoot);
            }
            Expect(assets.ActiveHandleCount==0,"프리팹 핸들 해제");
            Debug.Log("UNIT_FACING_PLAY_PASS 8-prefabs-move-vertical-attack-death-phase-pool-ui");
        }
        private static HexCoord Neighbor(RunController run,HexCoord cell,int side)
        {
            float x=run.Grid.CellToWorld(cell).x;
            return Enumerable.Range(0,6).Select(cell.GetNeighbor).First(candidate=>
                run.Grid.IsInsideBoard(candidate) && (side==0
                    ? Mathf.Abs(run.Grid.CellToWorld(candidate).x-x)<.0001f
                    : (run.Grid.CellToWorld(candidate).x-x)*side>.0001f));
        }
        private static void ExpectFacing(UnitFacingView facing,bool right,bool[] rightFlips)
        {
            Expect(facing.IsFacingRight==right,"논리 방향 일치");
            for(int i=0;i<rightFlips.Length;i++)
                Expect(facing.SpriteRenderers[i].flipX==(rightFlips[i]^!right),"실제 SpriteRenderer 방향 일치");
        }
    }
}
#endif
