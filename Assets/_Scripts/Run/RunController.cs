using System;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Traits;
using AutoBattler.Managers;
using AutoBattler.Presentation.Units;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Run
{
    /// <summary>씬 참조 주입과 도전 초기화·종료 순서 조정. 기능 처리는 각 매니저에 위임.</summary>
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
        [Header("기능별 매니저")]
        [SerializeField] private AssetManager assets;
        [SerializeField] private UnitManager units;
        [SerializeField] private BattleManager battle;
        [SerializeField] private ShopManager shop;
        [SerializeField] private AugmentManager augments;
        [SerializeField] private SynergyManager synergies;
        public AssetManager Assets=>assets;
        public UnitManager Units=>units;
        public BattleManager Battle=>battle;
        public ShopManager Shop=>shop;
        public AugmentManager Augments=>augments;
        public SynergyManager Synergies=>synergies;
        public RunState State { get; private set; }
        public BattleFlowController Flow=>flow;
        public UnitDragController Drag=>drag;
        public AssetCatalog Catalog=>catalog;
        public HexGridLayout Grid=>grid;
        public BenchLayout Bench=>bench;
        public TraitSnapshot AllySynergies=>synergies!=null ? synergies.Allies : TraitSnapshot.Empty;
        public TraitSnapshot EnemySynergies=>synergies!=null ? synergies.Enemies : TraitSnapshot.Empty;
        public bool IsReady { get; private set; }
        public bool IsLoading { get; private set; }
        public bool IsShuttingDown=>shutdownCompletion!=null;
        public int ActiveAssetHandleCount=>assets!=null ? assets.ActiveHandleCount : 0;
        public BattleVisualService Visuals=>assets!=null ? assets.Visuals : null;
        public float RemainingSeconds=>battle!=null ? battle.RemainingSeconds : 0f;
        public Sprite UnitIcon(int iconId)=>catalog.Icon(iconId);
        public string Message { get; private set; }="불러오는 중...";
        public event Action DisplayChanged;
        private CancellationTokenSource lifetime;
        private UniTaskCompletionSource shutdownCompletion;
        private void Awake()
        {
            lifetime=CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            assets.Initialize(catalog,flow,grid,unitPoolSize);
            units.Initialize(assets.Units,catalog,flow,drag,grid,bench);
            shop.Initialize(flow,drag,Report);
            augments.Initialize(flow,drag,Report);
            synergies.Initialize(units,flow);
            battle.Initialize(flow,drag,units,assets,synergies,augments,Report,lifetime.Token);
            drag.enabled=false; drag.PlacementChanged+=OnPlacementChanged;
        }
        private void Start()=>BootAsync().Forget();
        public void ConfigureScene(AssetCatalog assetsCatalog,BattleFlowController phase,UnitDragController input,HexGridLayout layout,BenchLayout seats)
        { catalog=assetsCatalog; flow=phase; drag=input; grid=layout; bench=seats; }
#if UNITY_EDITOR
        public void ConfigureManagers(AssetManager content,UnitManager roster,BattleManager combat,ShopManager store,AugmentManager choices,SynergyManager traits)
        { assets=content; units=roster; battle=combat; shop=store; augments=choices; synergies=traits; }
#endif
        public void RetryLoad() { if(!IsShuttingDown && !IsLoading && !IsReady) BootAsync().Forget(); }
        private async UniTask BootAsync()
        {
            if(IsShuttingDown || IsLoading || IsReady) return;
            IsLoading=true; Report("불러오는 중...");
            try
            {
                await assets.LoadAsync(lifetime.Token); lifetime.Token.ThrowIfCancellationRequested();
                State=new RunState(assets.Data,seed); units.Bind(State); shop.Bind(State); augments.Bind(State); synergies.Bind(State); battle.Bind(State);
                drag.BindBoard(State.Board,State.Placement); State.Changed+=OnStateChanged; IsReady=true;
                units.SyncAllies(); units.CreateEnemies(); synergies.Refresh(); augments.EnterPreparation();
                Report(State.Augments.HasPending?"증강 선택":"준비");
            }
            catch(OperationCanceledException) { }
            catch(Exception exception) { Report("불러오기에 실패했습니다. 다시 시도해 주세요."); Debug.LogException(exception,this); }
            finally { IsLoading=false; DisplayChanged?.Invoke(); }
        }
        private void Report(string message) { Message=message; DisplayChanged?.Invoke(); }
        private void OnPlacementChanged() { if(IsReady) synergies.Refresh(); DisplayChanged?.Invoke(); }
        private void OnStateChanged()
        {
            drag.SetPlayerLevel(State.Level);
            if(State.CanPrepare) { units.SyncAllies(); synergies.Refresh(); }
            DisplayChanged?.Invoke();
        }
        // 기존 HUD·씬의 공개 명령 유지. 실제 동작은 기능별 매니저에서 처리.
        public bool Buy(int slot)=>IsReady && shop.Buy(slot);
        public bool RefreshShop()=>IsReady && shop.RefreshShop();
        public bool BuyXp()=>IsReady && shop.BuyXp();
        public bool SellSelected()=>IsReady && shop.SellSelected();
        public bool ToggleAugmentWindow()=>IsReady && augments.ToggleWindow();
        public bool SelectAugment(int id)=>IsReady && augments.Select(id);
        public bool StartBattle()=>IsReady && battle.StartBattle();
        public bool Continue()=>IsReady && battle.Continue();
        public UniTask ShutdownAsync()
        {
            if(shutdownCompletion!=null) return shutdownCompletion.Task;
            shutdownCompletion=new UniTaskCompletionSource(); ShutdownCoreAsync(shutdownCompletion).Forget(); return shutdownCompletion.Task;
        }
        private async UniTask ShutdownCoreAsync(UniTaskCompletionSource completion)
        {
            try
            {
                IsReady=false; lifetime?.Cancel();
                if(State!=null) State.Changed-=OnStateChanged;
                if(drag!=null) { drag.PlacementChanged-=OnPlacementChanged; drag.ClearSelection(); drag.enabled=false; }
                if(shop!=null) shop.Bind(null);
                if(augments!=null) augments.Bind(null);
                if(battle!=null) await battle.StopAsync();
                if(synergies!=null) { synergies.EndBattle(); synergies.Bind(null); }
                if(units!=null) units.Clear();
                if(assets!=null) await assets.ShutdownAsync();
                lifetime?.Dispose(); completion.TrySetResult();
            }
            catch(Exception exception) { completion.TrySetException(exception); }
        }
        private void OnDestroy()=>ShutdownAsync().Forget();
    }
}
