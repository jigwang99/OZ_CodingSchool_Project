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
        [SerializeField] private StageDefinition stage;
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
        private SaveManager saves;
        private bool saveFailed;
        public StageDefinition Stage=>stage;
        private int StageId=>GameNavigation.SelectedStage!=null ? GameNavigation.SelectedStage.Id : stage.Id;
        private UniTaskCompletionSource shutdownCompletion;
        private void Awake()
        {
            saves=SaveManager.instance;
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
        public void ConfigureStage(StageDefinition definition)=>stage=definition;
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
                bool augmentOpen=true;
                if(GameNavigation.ResumeRequested)
                {
                    if(!saves.TryLoad(assets.Data,StageId,out var restored,out augmentOpen))
                    { Report(saves.LastError); return; }
                    State=restored;
                }
                else State=new RunState(assets.Data,seed);
                units.Bind(State); shop.Bind(State); augments.Bind(State); synergies.Bind(State); battle.Bind(State);
                drag.BindBoard(State.Board,State.Placement); State.Changed+=OnStateChanged; IsReady=true;
                units.SyncAllies(); units.CreateEnemies(); synergies.Refresh();
                if(State.CanPrepare) augments.EnterPreparation(augmentOpen);
                else flow.TryRestoreResult();
                if(!GameNavigation.ResumeRequested) TrySaveProgress();
                Report(!State.CanPrepare?State.LastResult:State.Augments.HasPending?"증강 선택":"준비");
            }
            catch(OperationCanceledException) { }
            catch(Exception exception)
            {
                // 초기화가 끝나지 않은 상태로 기존 저장을 덮어쓰지 않도록 차단.
                IsReady=false;
                if(State!=null) State.Changed-=OnStateChanged;
                State=null;
                Report("불러오기에 실패했습니다. 다시 시도해 주세요."); Debug.LogException(exception,this);
            }
            finally { IsLoading=false; DisplayChanged?.Invoke(); }
        }
        private void Report(string message) { Message=saveFailed?saves.LastError:message; DisplayChanged?.Invoke(); }
        private void OnPlacementChanged()
        { if(IsReady) { synergies.Refresh(); TrySaveProgress(); } DisplayChanged?.Invoke(); }
        private void OnStateChanged()
        {
            drag.SetPlayerLevel(State.Level);
            if(State.CanPrepare) { units.SyncAllies(); synergies.Refresh(); }
            if(State.IsSettled) TrySaveProgress();
            DisplayChanged?.Invoke();
        }
        // 기존 HUD·씬의 공개 명령 유지. 실제 동작은 기능별 매니저에서 처리.
        private bool SaveAfter(bool changed) { if(changed) TrySaveProgress(); return changed; }
        public bool Buy(int slot)=>IsReady && SaveAfter(shop.Buy(slot));
        public bool RefreshShop()=>IsReady && SaveAfter(shop.RefreshShop());
        public bool BuyXp()=>IsReady && SaveAfter(shop.BuyXp());
        public bool SellSelected()=>IsReady && SaveAfter(shop.SellSelected());
        public bool SellUnit(int instanceId)=>IsReady && SaveAfter(shop.Sell(instanceId));
        public bool ToggleAugmentWindow()=>IsReady && SaveAfter(augments.ToggleWindow());
        public bool SelectAugment(int id)=>IsReady && SaveAfter(augments.Select(id));
        public bool StartBattle()
        {
            if(!IsReady) return false;
            if(State.CanPrepare && !State.Augments.HasPending && State.Board.DeployedCount>0 && !TrySaveProgress()) return false;
            return battle.StartBattle();
        }
        public bool Continue()=>IsReady && flow.CurrentPhase==BattlePhase.Result && !State.IsOver
            && TrySaveProgress() && SaveAfter(battle.Continue());
        public bool TrySaveProgress()
        {
            if(!IsReady || State==null || !State.CanPrepare && !State.IsSettled) return false;
            bool success=saves.TrySave(State,StageId,flow.IsAugmentWindowOpen);
            bool recovered=saveFailed && success;
            saveFailed=!success;
            if(!success) Report(saves.LastError);
            else if(recovered) Report(State.CanPrepare?State.Augments.HasPending?"증강 선택":"준비":State.LastResult);
            return success;
        }
        private void OnApplicationPause(bool paused) { if(paused) TrySaveProgress(); }
        private void OnApplicationQuit()=>TrySaveProgress();
        public UniTask ShutdownAsync()=>ShutdownAsync(true);
        private UniTask ShutdownAsync(bool saveProgress)
        {
            if(shutdownCompletion!=null) return shutdownCompletion.Task;
            shutdownCompletion=new UniTaskCompletionSource(); ShutdownCoreAsync(shutdownCompletion,saveProgress).Forget(); return shutdownCompletion.Task;
        }
        private async UniTask ShutdownCoreAsync(UniTaskCompletionSource completion,bool saveProgress)
        {
            try
            {
                if(saveProgress) TrySaveProgress();
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
        // 파괴 콜백에서는 자식 기물의 배치가 이미 해제됐을 수 있으므로 저장 없이 정리만 진행.
        private void OnDestroy()=>ShutdownAsync(false).Forget();
    }
}
