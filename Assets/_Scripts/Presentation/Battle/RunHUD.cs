using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Units;
using AutoBattler.Battle.Placement;
using AutoBattler.Presentation.Units;
using AutoBattler.Data.Snapshot;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>BattleHUD 프리팹의 표시 갱신과 플레이 명령 연결.</summary>
    [DisallowMultipleComponent]
    public sealed class RunHUD : MonoBehaviour,IUnitDropTarget
    {
        [Header("진행 제어")]
        [SerializeField] private RunController run;
        [SerializeField] private GameNavigationSettings navigation;
        [SerializeField] private CanvasGroup interactionRoot;
        private GameSettingsView gameSettings;

        [Header("상단 정보")]
        [SerializeField] private TextMeshProUGUI roundText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI livesText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI experienceText;
        [SerializeField] private TextMeshProUGUI deploymentText;
        [SerializeField] private TextMeshProUGUI messageText;

        [Header("준비 화면")]
        [SerializeField] private CanvasGroup preparationPanel;
        [SerializeField] private RectTransform shopContainer;
        [SerializeField] private ShopItem[] shopItems = new ShopItem[5];
        [SerializeField] private Button refreshButton;
        [SerializeField] private TextMeshProUGUI refreshLabel;
        [SerializeField] private Button buyExperienceButton;
        [SerializeField] private TextMeshProUGUI buyExperienceLabel;
        [SerializeField] private GameObject shopContent;
        [SerializeField] private UnitSellZone sellZone;
        [SerializeField] private SynergyPanel synergyPanel;
        [SerializeField] private AugmentSelectionPanel augmentPanel;
        [SerializeField] private SelectedAugmentsView selectedAugments;
        [SerializeField] private UnitInfoPanel unitInfo;
        [SerializeField] private ItemInventoryPanel inventory;
        public ItemInventoryPanel Inventory=>inventory;

        [Header("전투와 결과")]
        [SerializeField] private Button startBattleButton;
        [SerializeField] private ResultPanel resultPanel;
        [SerializeField] private Button retryLoadButton;
        [SerializeField] private Button loadTitleButton;
        [SerializeField, Min(0f)] private float fadeDuration = 0.2f;

        private bool isBound;
        private bool hasDisplayedPhase;
        private BattlePhase displayedPhase;

        public RunController Controller => run;
        public GameNavigationSettings Navigation=>navigation;
        public GameSettingsView GameSettings=>gameSettings;
        public TextMeshProUGUI RoundText => roundText;
        public TextMeshProUGUI GoldText => goldText;
        public TextMeshProUGUI LivesText => livesText;
        public TextMeshProUGUI LevelText => levelText;
        public TextMeshProUGUI ExperienceText => experienceText;
        public TextMeshProUGUI DeploymentText => deploymentText;
        public TextMeshProUGUI MessageText => messageText;
        public CanvasGroup PreparationPanel => preparationPanel;
        public RectTransform ShopContainer => shopContainer;
        public IReadOnlyList<ShopItem> ShopItems => System.Array.AsReadOnly(shopItems);
        public Button RefreshButton => refreshButton;
        public Button BuyExperienceButton => buyExperienceButton;
        public GameObject ShopContent=>shopContent;
        public UnitSellZone SellZone=>sellZone;
        public Button StartBattleButton => startBattleButton;
        public Button RetryLoadButton => retryLoadButton;
        public Button LoadTitleButton=>loadTitleButton;
        public ResultPanel ResultPanel => resultPanel;
        public SynergyPanel SynergyPanel => synergyPanel;
        public AugmentSelectionPanel AugmentPanel=>augmentPanel;
        public SelectedAugmentsView SelectedAugments=>selectedAugments;
        public UnitInfoPanel UnitInfo => unitInfo;


        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                gameSettings=AutoBattler.Managers.GameManager.instance.Settings;
                if(gameSettings!=null) gameSettings.BindContext(interactionRoot,run);
                Bind();
            }
        }

        private void Bind()
        {
            if (isBound || run == null)
                return;

            for (int i = 0; i < shopItems.Length; i++)
            {
                shopItems[i].BindSlot(i);
                shopItems[i].PurchaseRequested += BuyShopItem;
            }
            startBattleButton.onClick.AddListener(StartBattle);
            refreshButton.onClick.AddListener(RefreshShop);
            buyExperienceButton.onClick.AddListener(BuyExperience);
            run.Drag.DragStarted+=ShowSellDrag;
            run.Drag.DragMoved+=UpdateSellDrag;
            run.Drag.DragEnded+=HideSellDrag;
            run.Drag.BindDropTarget(this);
            HideSellDrag();
            resultPanel.ContinueRequested += NextRound;
            resultPanel.RetryRequested += RetryRun;
            resultPanel.TitleRequested += ReturnToTitle;
            retryLoadButton.onClick.AddListener(RetryLoad);
            loadTitleButton.onClick.AddListener(ReturnFromLoadError);
            run.DisplayChanged += Refresh;
            if(inventory!=null) inventory.Bind(run,interactionRoot);
            if(unitInfo!=null) unitInfo.BindRun(run);
            if(augmentPanel!=null) { augmentPanel.Selected+=SelectAugment; augmentPanel.ToggleRequested+=ToggleAugments; }
            isBound = true;
            hasDisplayedPhase = false;
            Refresh();
        }

        private void StartBattle() => run.StartBattle();
        private void BuyShopItem(int slotIndex) => run.Buy(slotIndex);
        private void RefreshShop() => run.RefreshShop();
        private void BuyExperience() => run.BuyXp();
        private void ShowSellDrag(UnitView view)
        {
            Unit unit=view.GetComponent<Unit>();
            if(unit==null || unit.Definition==null || view.State==null) return;
            shopContent.SetActive(false);
            sellZone.Show(BattleText.UnitName(unit.Definition),unit.Star,run.State.SalePrice(view.State.InstanceId),CanPrepare());
        }
        private void UpdateSellDrag(Vector2 pointer)=>sellZone.SetPointer(pointer);
        private void HideSellDrag()
        { sellZone.Hide(); shopContent.SetActive(true); }
        bool IUnitDropTarget.Contains(Vector2 screenPosition)=>sellZone.Contains(screenPosition);
        bool IUnitDropTarget.TryDrop(UnitView view)
        {
            Unit unit=view!=null ? view.GetComponent<Unit>() : null;
            return CanPrepare() && unit!=null && unit.Team==UnitTeam.Ally && view.State!=null
                && run.SellUnit(view.State.InstanceId);
        }
        private void NextRound() => run.Continue();
        private void RetryLoad() => run.RetryLoad();
        private void ReturnFromLoadError()=>ReturnFromLoadErrorAsync().Forget();
        private async UniTask ReturnFromLoadErrorAsync()
        {
            if(run.IsReady || run.IsLoading || GameNavigation.IsTransitioning) return;
            try { await GameNavigation.ReturnToTitleAsync(navigation,run); }
            catch(System.Exception exception) { Debug.LogException(exception); }
        }
        private void RetryRun()=>NavigateAsync(false).Forget();
        private void ReturnToTitle()=>NavigateAsync(true).Forget();
        private async Cysharp.Threading.Tasks.UniTask NavigateAsync(bool title)
        {
            if((!run.IsReady && !run.IsShuttingDown) || run.State==null || !run.State.IsOver || GameNavigation.IsTransitioning) return;
            resultPanel.SetNavigationBusy(true);
            try
            {
                bool loaded=title ? await GameNavigation.ReturnToTitleAsync(navigation,run)
                    : await GameNavigation.RetryAsync(navigation,run);
                if(!loaded && this!=null) messageText.text="장면을 열 수 없습니다. 다시 시도해 주세요.";
            }
            catch(System.Exception exception)
            { if(this!=null) messageText.text="장면을 불러오지 못했습니다. 다시 시도해 주세요."; Debug.LogException(exception); }
            finally { if(this!=null) resultPanel.SetNavigationBusy(false); }
        }
        private void SelectAugment(int id)=>run.SelectAugment(id);
        private void ToggleAugments()=>run.ToggleAugmentWindow();

        private bool CanPrepare() => run.IsReady && run.State.CanPrepare
            && !run.State.IsOver && run.Flow.CurrentPhase == BattlePhase.Preparation;

        private void Update()
        {
            if (!isBound)
                return;
            if (run.IsReady && run.Flow.CurrentPhase == BattlePhase.Combat)
                messageText.text = $"전투 중   남은 시간 {Mathf.CeilToInt(run.RemainingSeconds)}초";
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            if (unitInfo != null)
            {
                Unit inspected = run.IsReady && run.Flow.CanInspectUnits ? run.Drag.InspectedUnit : null;
                unitInfo.Show(inspected, run.IsReady ? run.State.Data : null);
            }
        }

        private void Refresh()
        {
            if (!isBound)
                return;
            bool ready = run.IsReady;
            if(augmentPanel!=null) augmentPanel.Show(run);
            if(selectedAugments!=null) selectedAugments.Show(run);
            bool prepare = CanPrepare();
            if(synergyPanel!=null) synergyPanel.Show(ready?run.AllySynergies:null,ready,run.Flow.CurrentPhase);
            messageText.text = run.Message;
            roundText.text = ready ? $"라운드 {(run.State.Round - 1) / 4 + 1}-{(run.State.Round - 1) % 4 + 1}" : "라운드 --";
            goldText.text = ready ? $"골드 {run.State.Gold}" : "골드 --";
            livesText.text = ready ? $"목숨 {run.State.Lives}" : "목숨 --";
            levelText.text = ready ? $"레벨 {run.State.Level}" : "레벨 --";
            experienceText.text = ready
                ? run.State.Level == 10 ? "경험치 최대" : $"경험치 {run.State.Xp}/{run.State.Data.RequiredXp(run.State.Level)}"
                : "경험치 --";
            deploymentText.text = ready ? $"배치 {run.State.Board.DeployedCount}/{run.State.Level}" : "배치 --";

            for (int i = 0; i < shopItems.Length; i++)
            {
                int id = ready ? run.State.Shop[i] : 0;
                UnitDefinition unit = id == 0 ? null : run.State.Data.Units[id];
                Sprite icon = unit == null ? null : run.UnitIcon(unit.IconId);
                shopItems[i].Show(unit, icon, id == 0 ? 0 : run.State.Stock(id), ready ? run.State.Gold : 0, prepare);
            }

            // 비용 표시도 시트의 경제 규칙을 사용.
            refreshLabel.text = ready ? $"새로고침 ({run.State.Data.IntRule(EconomyRule.RefreshCost)}골드)" : "새로고침";
            buyExperienceLabel.text = ready ? $"경험치 ({run.State.Data.IntRule(EconomyRule.XpCost)}골드)" : "경험치 구매";
            refreshButton.interactable = prepare;
            buyExperienceButton.interactable = prepare && run.State.Level < 10;
            startBattleButton.interactable = prepare && run.State.Board.DeployedCount > 0;
            startBattleButton.gameObject.SetActive(ready);
            retryLoadButton.gameObject.SetActive(!ready && !run.IsLoading);
            loadTitleButton.gameObject.SetActive(!ready && !run.IsLoading);
            preparationPanel.interactable = prepare;
            RefreshSelection();

            if (ready && run.State.IsOver)
                messageText.text = run.State.WonRun ? "도전 성공!" : "도전 종료";

            bool showResult = ready && run.Flow.CurrentPhase == BattlePhase.Result;
            bool phaseChanged = !hasDisplayedPhase || displayedPhase != run.Flow.CurrentPhase;
            bool animate = hasDisplayedPhase && phaseChanged;
            if (showResult)
            {
                string title = run.State.IsOver
                    ? run.State.WonRun ? "도전 성공!" : "도전 종료"
                    : "전투 결과";
                resultPanel.Show(title, run.State.LastResult, !run.State.IsOver, hasDisplayedPhase);
                OwnedItem reward=run.State.Items.Find(run.State.Items.LastRewardInstanceId);
                if(reward!=null) resultPanel.ShowItemReward(run.State.Data.Items[reward.DefinitionId].Name);
            }
            else
                resultPanel.Hide();
            if (phaseChanged)
            {
                preparationPanel.DOKill();
                if (animate)
                {
                    preparationPanel.DOFade(prepare ? 1f : 0.45f, fadeDuration).SetUpdate(true);
                }
                else
                {
                    preparationPanel.alpha = prepare ? 1f : 0.45f;
                }
                displayedPhase = run.Flow.CurrentPhase;
                hasDisplayedPhase = true;
            }
            else if (!showResult)
            {
                // 초기 로드 완료도 단계 전환 없이 준비 화면 갱신.
                preparationPanel.DOKill();
                preparationPanel.alpha = prepare ? 1f : 0.45f;
            }
        }

        private void Unbind()
        {
            if(inventory!=null) inventory.Bind(null,null);
            if (unitInfo != null) { unitInfo.Hide(); unitInfo.BindRun(null); }
            if (!isBound)
                return;
            if (run != null)
                run.DisplayChanged -= Refresh;
            if(augmentPanel!=null) { augmentPanel.Selected-=SelectAugment; augmentPanel.ToggleRequested-=ToggleAugments; }
            for (int i = 0; i < shopItems.Length; i++)
                shopItems[i].PurchaseRequested -= BuyShopItem;
            startBattleButton.onClick.RemoveListener(StartBattle);
            refreshButton.onClick.RemoveListener(RefreshShop);
            buyExperienceButton.onClick.RemoveListener(BuyExperience);
            if(run!=null)
            {
                run.Drag.UnbindDropTarget(this);
                run.Drag.DragStarted-=ShowSellDrag;
                run.Drag.DragMoved-=UpdateSellDrag;
                run.Drag.DragEnded-=HideSellDrag;
            }
            HideSellDrag();
            resultPanel.ContinueRequested -= NextRound;
            resultPanel.RetryRequested -= RetryRun;
            resultPanel.TitleRequested -= ReturnToTitle;
            retryLoadButton.onClick.RemoveListener(RetryLoad);
            loadTitleButton.onClick.RemoveListener(ReturnFromLoadError);
            preparationPanel.DOKill();
            resultPanel.StopAnimation();
            isBound = false;
        }

        private void OnDisable()
        {
            if(gameSettings!=null) gameSettings.UnbindContext(interactionRoot);
            Unbind();
        }
    }
}
