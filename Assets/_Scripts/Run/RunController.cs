using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Data.Snapshot;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Battle.Units;
using AutoBattler.Battle.Combat;
using AutoBattler.Presentation.Battle;
using AutoBattler.Presentation.Units;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Run
{
    /// <summary>데이터 로드 → 준비 → 자동 전투 → 정산 → 다음 준비 연결.</summary>
    [DisallowMultipleComponent]
    public sealed class RunController : MonoBehaviour
    {
        [SerializeField] private AssetCatalog catalog;
        [SerializeField] private BattleFlowController flow;
        [SerializeField] private UnitDragController drag;
        [SerializeField] private HexGridLayout grid;
        [SerializeField] private BenchLayout bench;
        [SerializeField] private int seed=20261012;
        [SerializeField,Min(0)] private int unitPoolSize=1;
        public RunState State { get; private set; }
        public BattleFlowController Flow=>flow;
        public UnitDragController Drag=>drag;
        public AssetCatalog Catalog=>catalog;
        public HexGridLayout Grid=>grid;
        public BenchLayout Bench=>bench;
        public bool IsReady { get; private set; }
        public bool IsLoading { get; private set; }
        public Sprite UnitIcon(int iconId) => catalog.Icon(iconId);
        public string Message { get; private set; }="Loading data...";
        public float RemainingSeconds=>Mathf.Max(0f,combatDeadline-Time.time);
        public event Action DisplayChanged;
        private AssetService assets;
        private AssetService.Lease<TextAsset> dataLease;
        private UnitPrefabPool pool;
        private readonly Dictionary<int,Unit> allies=new Dictionary<int,Unit>();
        private readonly List<Unit> enemies=new List<Unit>();
        private CancellationTokenSource combatCancellation;
        private CancellationTokenSource lifetime;
        private float combatDeadline;
        private void Awake()
        {
            lifetime=CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            assets=new AssetService();
            var poolRoot=new GameObject("UnitPool").transform; poolRoot.SetParent(transform,false);
            pool=new UnitPrefabPool(assets,poolRoot,unitPoolSize);
            drag.enabled=false;
            drag.PlacementChanged+=OnPlacementChanged;
        }
        private void Start()=>BootAsync().Forget();
        public void ConfigureScene(AssetCatalog assetsCatalog,BattleFlowController phase,UnitDragController input,HexGridLayout layout,BenchLayout seats)
        { catalog=assetsCatalog; flow=phase; drag=input; grid=layout; bench=seats; }
        public void RetryLoad() { if(!IsLoading && !IsReady) BootAsync().Forget(); }
        private async UniTask BootAsync()
        {
            if(IsLoading || IsReady) return;
            IsLoading=true; Message="Loading data and unit assets..."; DisplayChanged?.Invoke();
            try
            {
                if(dataLease==null) dataLease=await assets.AcquireAsync<TextAsset>(catalog.Snapshot,lifetime.Token);
                var repository=new DataRepository(JsonUtility.FromJson<GameSnapshot>(dataLease.Asset.text));
                foreach(var address in repository.Units.Values.Select(x=>catalog.VisualPrefab(x.VisualId)).GroupBy(x=>x.RuntimeKey).Select(x=>x.First()))
                    await pool.PreloadAsync(address,lifetime.Token);
                lifetime.Token.ThrowIfCancellationRequested();
                State=new RunState(repository,seed); drag.BindBoard(State.Board,State.Placement);
                State.Changed+=OnStateChanged; IsReady=true;
                SyncAllies(); CreateEnemies(); drag.enabled=true;
                Message="Drag a bench unit onto the blue board, then start battle.";
            }
            catch(OperationCanceledException) { }
            catch(Exception exception) { Message="Load failed. Check Console and retry."; Debug.LogException(exception,this); }
            finally { IsLoading=false; DisplayChanged?.Invoke(); }
        }
        private void OnPlacementChanged()=>DisplayChanged?.Invoke();
        private void OnStateChanged()
        {
            drag.SetPlayerLevel(State.Level);
            if(State.CanPrepare) SyncAllies();
            DisplayChanged?.Invoke();
        }
        private void SyncAllies()
        {
            drag.ClearSelection();
            foreach(var pair in allies.ToArray())
                if(!State.Units.Any(x=>x.Placement.InstanceId==pair.Key)) { pool.Return(pair.Value); allies.Remove(pair.Key); }
            foreach(var owned in State.Units)
            {
                int id=owned.Placement.InstanceId;
                if(!allies.TryGetValue(id,out Unit unit))
                {
                    unit=pool.Rent(catalog.VisualPrefab(State.Data.Units[owned.DefinitionId].VisualId));
                    ConfigureUnit(unit,owned.DefinitionId,owned.Star,id,UnitTeam.Ally);
                    drag.BindView(unit.View,owned.Placement);
                    unit.gameObject.SetActive(true); allies.Add(id,unit);
                }
                else if(unit.Star!=owned.Star) ConfigureUnit(unit,owned.DefinitionId,owned.Star,id,UnitTeam.Ally);
            }
            Physics2D.SyncTransforms();
        }
        private void ConfigureUnit(Unit unit,int definitionId,int star,int id,UnitTeam team,HexCoord? cell=null,int boss=0)
        {
            unit.Configure(State.Data.Units[definitionId],star,id,team,State.Data,flow,grid,bench,cell,boss);
            UnitIdentityView identity=unit.GetComponent<UnitIdentityView>();
            if(identity==null) identity=unit.gameObject.AddComponent<UnitIdentityView>();
            identity.Apply(unit);
            if(unit.GetComponent<UnitAnimationView>()==null) unit.gameObject.AddComponent<UnitAnimationView>();
        }
        private void CreateEnemies()
        {
            foreach(var unit in enemies) pool.Return(unit); enemies.Clear();
            int id=100000;
            foreach(var definition in State.Data.Rounds[State.Round].Enemies)
            {
                HexCoord cell=HexCoord.FromOffset(definition.Col,definition.Row);
                Unit unit=pool.Rent(catalog.VisualPrefab(State.Data.Units[definition.UnitId].VisualId));
                ConfigureUnit(unit,definition.UnitId,definition.Star,id++,UnitTeam.Enemy,cell,definition.BossProfileId);
                var displayState=new UnitState(unit.InstanceId) { Location=UnitLocation.OnBoard(cell) };
                unit.View.Bind(displayState,null);
                unit.transform.position=grid.CellToWorld(cell);
                unit.gameObject.SetActive(true); enemies.Add(unit);
            }
        }
        public bool Buy(int slot)=>PrepareCommand(()=>State.TryBuy(slot));
        public bool RefreshShop()=>PrepareCommand(()=>State.TryRefresh());
        public bool BuyXp()=>PrepareCommand(()=>State.TryBuyXp());
        public bool SellSelected()
        {
            int id=drag.SelectedUnit?.State?.InstanceId ?? 0;
            return PrepareCommand(()=>State.TrySell(id));
        }
        private bool PrepareCommand(Func<bool> command)
        {
            if(!IsReady || flow.CurrentPhase!=BattlePhase.Preparation || !State.CanPrepare || State.IsOver) return false;
            drag.CancelDrag();
            bool result=command(); Message=result?"Ready.":State.LastError;
            DisplayChanged?.Invoke(); return result;
        }
        public bool StartBattle()
        {
            if(!IsReady || flow.CurrentPhase!=BattlePhase.Preparation || State.IsOver) return false;
            drag.CancelDrag();
            if(State.Board.DeployedCount==0) { Message="Deploy at least one unit."; DisplayChanged?.Invoke(); return false; }
            var combatUnits=allies.Values.Where(x=>x.IsOnBoard).Concat(enemies).OrderBy(x=>x.InstanceId).ToArray();
            foreach(var unit in combatUnits) unit.BeginBattle();
            var battle=new AutoBattleController(combatUnits);
            if(!flow.TryStartCombat()) return false;
            State.CanPrepare=false; drag.enabled=false;
            combatCancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            combatDeadline=Time.time+State.Data.Rule(EconomyRule.CombatSeconds);
            Message="Automatic battle"; DisplayChanged?.Invoke();
            BattleAsync(battle,combatCancellation).Forget(); return true;
        }
        private async UniTask BattleAsync(AutoBattleController battle,CancellationTokenSource operation)
        {
            try
            {
                bool won=await battle.RunAsync(State.Data.Rule(EconomyRule.CombatSeconds),operation.Token);
                if(!flow.TryFinishCombat()) return;
                operation.Cancel();
                foreach(var unit in allies.Values.Concat(enemies)) { unit.Move.Stop(); unit.Combat.ResetForRound(); }
                if(State.TrySettle(won)) Message=State.LastResult;
                DisplayChanged?.Invoke();
            }
            catch(OperationCanceledException) { }
            catch(Exception exception)
            {
                operation.Cancel(); Debug.LogException(exception,this);
                if(flow.TryFinishCombat()) State.TrySettle(false);
                Message="Battle error. Check Console."; DisplayChanged?.Invoke();
            }
            finally { if(ReferenceEquals(combatCancellation,operation)) combatCancellation=null; operation.Dispose(); }
        }
        public bool Continue()
        {
            if(!IsReady || flow.CurrentPhase!=BattlePhase.Result || State.IsOver) return false;
            if(!State.TryNextRound()) return false;
            foreach(var unit in allies.Values) unit.RestorePreparation();
            CreateEnemies(); flow.TryBeginPreparation(); drag.enabled=true;
            Message="Army restored. Prepare the next round.";
            DisplayChanged?.Invoke(); return true;
        }
        private void OnDestroy()
        {
            lifetime?.Cancel(); combatCancellation?.Cancel();
            if(State!=null) State.Changed-=OnStateChanged;
            if(drag!=null) drag.PlacementChanged-=OnPlacementChanged;
            ReleaseAsync().Forget();
        }
        private async UniTask ReleaseAsync()
        {
            await pool.DisposeAsync(); dataLease?.Dispose(); assets.Dispose(); lifetime.Dispose();
        }
    }
}
