using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Data.Snapshot;
using AutoBattler.Managers;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>선택 스테이지의 해금 유닛 미리보기와 이어하기·새로하기 표시.</summary>
    public sealed class StageDetailsPanel : MonoBehaviour
    {
        [SerializeField] private AssetCatalog catalog;
        [SerializeField] private TextMeshProUGUI stageNameText;
        [SerializeField] private TextMeshProUGUI unlockUnitsText;
        [SerializeField] private ScrollRect unitScroll;
        [SerializeField] private StageUnlockUnitView[] unitViews=Array.Empty<StageUnlockUnitView>();
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newRunButton;
        private SaveManager saves;
        private GameNavigationSettings navigation;
        private DataRepository data;
        private AssetService assets;
        private CancellationTokenSource lifetime;
        private readonly List<AssetService.Lease<Sprite>> portraits=new List<AssetService.Lease<Sprite>>();
        private bool interactable=true;
        public StageDefinition Stage { get; private set; }
        public TextMeshProUGUI StageNameText=>stageNameText;
        public TextMeshProUGUI UnlockUnitsText=>unlockUnitsText;
        public IReadOnlyList<StageUnlockUnitView> UnitViews=>Array.AsReadOnly(unitViews);
        public ScrollRect UnitScroll=>unitScroll;
        public Button ContinueButton=>continueButton;
        public Button NewRunButton=>newRunButton;
        public int ActiveAssetHandleCount=>assets?.ActiveHandleCount ?? 0;
        public bool IsLoadingUnits=>lifetime!=null;
        public event Action ContinueRequested;
        public event Action NewRunRequested;

        public void Bind(GameNavigationSettings settings,SaveManager progress)
        {
            if(saves!=null) saves.Changed-=RefreshProgress;
            navigation=settings; saves=progress;
            if(saves!=null && isActiveAndEnabled) saves.Changed+=RefreshProgress;
            RefreshButtons();
        }
        public void Show(StageDefinition stage)
        {
            if(stage==null) {Hide(); return;}
            Stage=stage; gameObject.SetActive(true);
            stageNameText.text=$"{stage.DisplayName}\n{stage.LocationName}";
            RefreshUnits(); RefreshButtons();
        }
        public void Hide() {Stage=null; gameObject.SetActive(false);}
        public void SetInteractable(bool value) {interactable=value; RefreshButtons();}
        private bool CanStart=>interactable && Stage!=null && navigation!=null && saves!=null
            && saves.IsStageUnlocked(navigation,Stage) && !GameNavigation.IsTransitioning;
        private bool CanContinue=>CanStart && saves.HasSavedRun && saves.SavedStageId==Stage.Id;
        private void RefreshButtons()
        {newRunButton.interactable=CanStart; continueButton.interactable=CanContinue;}
        private void RefreshProgress()
        {
            RefreshButtons();
            foreach(StageUnlockUnitView view in unitViews)
                if(view.Unit!=null) view.SetUnlocked(saves!=null && saves.IsUnitUnlocked(view.Unit.Id));
        }
        private void RequestContinue() {if(CanContinue) ContinueRequested?.Invoke();}
        private void RequestNewRun() {if(CanStart) NewRunRequested?.Invoke();}
        private void OnEnable()
        {
            if(!Application.isPlaying) return;
            if(saves!=null) saves.Changed+=RefreshProgress;
            continueButton.onClick.AddListener(RequestContinue); newRunButton.onClick.AddListener(RequestNewRun);
        }
        private void RefreshUnits()
        {
            CancelPreview();
            if(Stage==null) return;
            unlockUnitsText.gameObject.SetActive(true);
            if(Stage.PlannedUnlockUnitIds.Count==0) {unlockUnitsText.text="없음"; return;}
            unlockUnitsText.text="불러오는 중…";
            if(assets==null) assets=new AssetService();
            lifetime=new CancellationTokenSource();
            LoadPreviewAsync(Stage,assets,lifetime).Forget();
        }
        private async UniTask LoadPreviewAsync(StageDefinition stage,AssetService source,CancellationTokenSource operation)
        {
            CancellationToken token=operation.Token;
            try
            {
                if(data==null)
                {
                    using(AssetService.Lease<TextAsset> lease=await source.AcquireAsync<TextAsset>(catalog.Snapshot,token))
                    {token.ThrowIfCancellationRequested();data=new DataRepository(JsonUtility.FromJson<GameSnapshot>(lease.Asset.text));}
                }
                token.ThrowIfCancellationRequested();
                unlockUnitsText.text=string.Join("\n",stage.PlannedUnlockUnitIds.Select(id=>data.Units[id].Name));
                unlockUnitsText.gameObject.SetActive(false);
                for(int i=0;i<stage.PlannedUnlockUnitIds.Count;i++)
                {
                    UnitDefinition unit=data.Units[stage.PlannedUnlockUnitIds[i]];
                    unitViews[i].Show(unit,data);unitViews[i].SetUnlocked(saves!=null && saves.IsUnitUnlocked(unit.Id));
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(unitScroll.content);unitScroll.verticalNormalizedPosition=1f;
                for(int i=0;i<stage.PlannedUnlockUnitIds.Count;i++)
                {
                    UnitDefinition unit=data.Units[stage.PlannedUnlockUnitIds[i]];
                    AssetService.Lease<Sprite> portrait=await source.AcquireAsync<Sprite>(catalog.PortraitReference(unit.VisualId),token);
                    if(token.IsCancellationRequested) {portrait.Dispose();token.ThrowIfCancellationRequested();}
                    portraits.Add(portrait);unitViews[i].SetPortrait(portrait.Asset);
                }
            }
            catch(OperationCanceledException) { }
            catch(Exception exception)
            {
                if(!token.IsCancellationRequested && this!=null)
                {unlockUnitsText.text="정보를 불러올 수 없음";unlockUnitsText.gameObject.SetActive(true);Debug.LogWarning(exception.Message,this);}
            }
            finally
            {if(ReferenceEquals(operation,lifetime)) {lifetime=null;operation.Dispose();}}
        }
        private void CancelPreview()
        {
            CancellationTokenSource previous=lifetime;lifetime=null;previous?.Cancel();previous?.Dispose();
            foreach(StageUnlockUnitView view in unitViews) view.Clear();
            foreach(AssetService.Lease<Sprite> portrait in portraits) portrait.Dispose();portraits.Clear();
        }
        private void OnDisable()
        {
            if(saves!=null) saves.Changed-=RefreshProgress;
            continueButton?.onClick.RemoveListener(RequestContinue);newRunButton?.onClick.RemoveListener(RequestNewRun);
            Stage=null;CancelPreview();assets?.Dispose();assets=null;
        }
    }
}
