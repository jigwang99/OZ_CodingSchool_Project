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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

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
        [SerializeField] private GameObject timerRoot;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private RoundProgressView roundProgress;

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
        [SerializeField] private ItemHoverTooltip itemTooltip;
        [SerializeField] private GoldIncomeHover goldIncome;
        [SerializeField] private DeckPlannerPanel deckPlanner;
        [SerializeField] private BattleStatisticsPanel battleStatistics;
        public BattleStatisticsPanel BattleStatistics=>battleStatistics;
        public DeckPlannerPanel DeckPlanner=>deckPlanner;
        public ItemInventoryPanel Inventory=>inventory;
        public ItemHoverTooltip ItemTooltip => itemTooltip;
        public GoldIncomeHover GoldIncome => goldIncome;

        [Header("전투와 결과")]
        [SerializeField] private Button startBattleButton;
        [SerializeField] private ResultPanel resultPanel;
        [SerializeField] private Button retryLoadButton;
        [SerializeField] private Button loadTitleButton;
        [SerializeField, Min(0f)] private float fadeDuration = 0.2f;

        [Header("실패 사유 안내")]
        [SerializeField] private ActionFeedbackView feedback;
        [SerializeField] private FailureHint refreshHint,experienceHint;
        public ActionFeedbackView Feedback=>feedback;
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
        public TextMeshProUGUI TimeText => timeText;
        public RoundProgressView RoundProgress => roundProgress;
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
                if(gameSettings!=null) gameSettings.BindContext(interactionRoot,run,navigation);
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
                int slot=i;
                shopItems[i].FailureHint.Bind(()=>run.Shop.BuyFailureReason(slot),CanReportFailure,run.NotifyFailure);
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
            resultPanel.StageSelectionRequested += OpenStageSelection;
            retryLoadButton.onClick.AddListener(RetryLoad);
            loadTitleButton.onClick.AddListener(ReturnFromLoadError);
            run.DisplayChanged += Refresh;
            run.FailureReported+=feedback.ShowFailure;
            refreshHint.Bind(()=>run.Shop.RefreshFailureReason,CanReportFailure,run.NotifyFailure);
            experienceHint.Bind(()=>run.Shop.ExperienceFailureReason,CanReportFailure,run.NotifyFailure);
            if(inventory!=null) inventory.Bind(run,interactionRoot);
            if(unitInfo!=null) unitInfo.BindRun(run);
            itemTooltip.Bind(run, interactionRoot, inventory, inventory.StoredSlots.Concat(unitInfo.EquipmentSlots));
            goldIncome.Bind(run, interactionRoot, inventory);
            deckPlanner.Bind(run,interactionRoot,inventory);
            battleStatistics.Bind(run,interactionRoot);
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
            sellZone.Show(BattleText.UnitName(unit.Definition),unit.Star,run.State.SalePrice(view.State.InstanceId),run.IsReady && run.Shop.CanSell(view.State.InstanceId));
        }
        private void UpdateSellDrag(Vector2 pointer)=>sellZone.SetPointer(pointer);
        private void HideSellDrag()
        { sellZone.Hide(); shopContent.SetActive(true); }
        bool IUnitDropTarget.Contains(Vector2 screenPosition)=>sellZone.Contains(screenPosition);
        bool IUnitDropTarget.TryDrop(UnitView view)
        {
            Unit unit=view!=null ? view.GetComponent<Unit>() : null;
            return run.IsReady && unit!=null && unit.Team==UnitTeam.Ally && view.State!=null
                && run.SellUnit(view.State.InstanceId);
        }
        private void NextRound() => run.Continue();
        private void RetryLoad() => run.RetryLoad();
        private void ReturnFromLoadError()=>ReturnFromLoadErrorAsync().Forget();
        private async UniTask ReturnFromLoadErrorAsync()
        {
            if(run.IsReady || run.IsLoading || GameNavigation.IsTransitioning) return;
            try { await GameNavigation.OpenStageSelectionAsync(navigation,run); }
            catch(System.Exception exception) { Debug.LogException(exception); }
        }
        private void OpenStageSelection()=>NavigateAsync().Forget();
        private async Cysharp.Threading.Tasks.UniTask NavigateAsync()
        {
            if((!run.IsReady && !run.IsShuttingDown) || run.State==null || !run.State.IsOver || run.State.HasPendingRoundReward || GameNavigation.IsTransitioning) return;
            resultPanel.SetNavigationBusy(true);
            try
            {
                bool loaded=await GameNavigation.OpenStageSelectionAsync(navigation,run);
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
            RefreshTime();
            if (run.IsReady && run.Flow.CurrentPhase == BattlePhase.Combat)
                messageText.text = "전투 중";
            if(run.IsReady && run.Flow.CurrentPhase==BattlePhase.Result && (!run.State.IsOver || run.State.HasPendingRoundReward))
                resultPanel.ShowCountdown();
            RefreshSelection();
            HandleShopShortcuts();
        }

        private void HandleShopShortcuts()
        {
            Keyboard keyboard=Keyboard.current;
            if(keyboard==null || !keyboard.dKey.wasPressedThisFrame && !keyboard.fKey.wasPressedThisFrame) return;
            if(!run.IsReady || run.IsShuttingDown || GameNavigation.IsTransitioning
                || run.Flow.IsAugmentWindowOpen
                || interactionRoot==null || !interactionRoot.interactable || !interactionRoot.blocksRaycasts
                || gameSettings!=null && gameSettings.IsOpen) return;
            GameObject selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            TMP_InputField input=selected!=null?selected.GetComponentInParent<TMP_InputField>():null;
            if(input!=null && input.isFocused) return;
            if(inventory.IsDraggingItem || run.Drag.DraggedUnit!=null) return;
            if(keyboard.dKey.wasPressedThisFrame) TryShortcut(refreshButton,run.Shop.RefreshFailureReason);
            if(keyboard.fKey.wasPressedThisFrame) TryShortcut(buyExperienceButton,run.Shop.ExperienceFailureReason);
        }

        private void TryShortcut(Button button,string reason)
        {
            if(!button.IsActive()) return;
            if(button.IsInteractable()) button.onClick.Invoke();
            else if(!string.IsNullOrEmpty(reason))
            { run.NotifyFailure(reason); AutoBattler.Managers.SoundManager.PlayEffect(AutoBattler.Audio.SoundId.Denied); }
        }
        private bool CanReportFailure()=>run.IsReady && !run.IsShuttingDown && !GameNavigation.IsTransitioning
            && interactionRoot.interactable && interactionRoot.blocksRaycasts && !run.Flow.IsAugmentWindowOpen
            && (gameSettings==null || !gameSettings.IsOpen) && !inventory.IsDraggingItem && run.Drag.DraggedUnit==null;
        private void RefreshTime()
        {
            if (timerRoot == null || timeText == null) return;
            bool ready = run != null && run.IsReady;
            timerRoot.SetActive(ready);
            if (!ready) return;
            timeText.text = run.Flow.CurrentPhase == BattlePhase.Combat
                ? $"{Mathf.CeilToInt(run.RemainingSeconds)}"
                : run.Flow.CurrentPhase == BattlePhase.Result && (!run.State.IsOver || run.State.HasPendingRoundReward)
                    ? $"{Mathf.CeilToInt(run.Battle.NextPreparationSeconds)}" : "--";
        }

        private void RefreshSelection()
        {
            if (unitInfo != null)
            {
                Unit inspected = run.IsReady && !battleStatistics.IsOpen && run.Drag.CanInspectUnits ? run.Drag.InspectedUnit : null;
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
            bool canBuy = ready && run.Shop.CanBuy;
            bool canManage = prepare || canBuy || ready && run.Shop.CanTrade;
            if(synergyPanel!=null) synergyPanel.Show(ready?run.AllySynergies:null,ready,run.Flow.CurrentPhase);
            messageText.text = run.Message;
            messageText.gameObject.SetActive(!ready);
            roundProgress.Show(ready ? run.State : null, ready ? run.Stage.DisplayName : "--");
            roundText.text = ready ? $"{run.Stage.DisplayName} {run.Stage.LocationName}" : "--";
            goldText.text = ready ? run.State.Gold.ToString() : "--";
            RefreshTime();
            livesText.text = ready ? run.State.Lives.ToString() : "--";
            levelText.text = ready ? run.State.Level.ToString() : "--";
            experienceText.text = ready
                ? run.State.Level == 10 ? "MAX" : $"{run.State.Xp}/{run.State.Data.RequiredXp(run.State.Level)}"
                : "--";
            deploymentText.text = ready ? $"배치 {run.State.Board.DeployedCount}/{run.State.Level}" : "배치 --";

            for (int i = 0; i < shopItems.Length; i++)
            {
                int id = ready ? run.State.Shop[i] : 0;
                UnitDefinition unit = id == 0 ? null : run.State.Data.Units[id];
                Sprite icon = unit == null ? null : run.UnitIcon(unit.IconId);
                shopItems[i].Show(unit, icon, id == 0 ? 0 : run.State.Stock(id), ready ? run.State.Gold : 0, ready && run.Shop.CanBuySlot(i));
            }

            // 비용 표시도 시트의 경제 규칙을 사용.
            refreshLabel.text = ready ? $"새로고침 {CurrencyText.Cost(run.State.Data.IntRule(EconomyRule.RefreshCost),run.State.Gold)}" : "새로고침";
            buyExperienceLabel.text = ready ? $"경험치 {CurrencyText.Cost(run.State.Data.IntRule(EconomyRule.XpCost),run.State.Gold)}" : "경험치 구매";
            refreshButton.interactable = ready && run.Shop.CanTrade && run.Shop.RefreshFailureReason==null;
            buyExperienceButton.interactable = ready && run.Shop.CanTrade && run.Shop.ExperienceFailureReason==null;
            startBattleButton.interactable = prepare && run.State.Board.DeployedCount > 0;
            startBattleButton.gameObject.SetActive(ready);
            retryLoadButton.gameObject.SetActive(!ready && !run.IsLoading);
            loadTitleButton.gameObject.SetActive(!ready && !run.IsLoading);
            preparationPanel.interactable = canManage;
            RefreshSelection();

            if (ready && run.Flow.CurrentPhase == BattlePhase.Result)
                messageText.text = string.Empty;

            bool showResult = ready && run.Flow.CurrentPhase == BattlePhase.Result;
            bool phaseChanged = !hasDisplayedPhase || displayedPhase != run.Flow.CurrentPhase;
            bool animate = hasDisplayedPhase && phaseChanged;
            if (showResult)
            {
                resultPanel.Show(run.State.LastBattleWon, !run.State.IsOver || run.State.HasPendingRoundReward, hasDisplayedPhase);
                if(!run.State.IsOver || run.State.HasPendingRoundReward) resultPanel.ShowCountdown();
            }
            else
                resultPanel.Hide();
            if (phaseChanged)
            {
                preparationPanel.DOKill();
                if (animate)
                {
                    preparationPanel.DOFade(canManage ? 1f : 0.45f, fadeDuration).SetUpdate(true);
                }
                else
                {
                    preparationPanel.alpha = canManage ? 1f : 0.45f;
                }
                displayedPhase = run.Flow.CurrentPhase;
                hasDisplayedPhase = true;
            }
            else if (!showResult)
            {
                // 초기 로드 완료도 단계 전환 없이 준비 화면 갱신.
                preparationPanel.DOKill();
                preparationPanel.alpha = canManage ? 1f : 0.45f;
            }
        }

        private void Unbind()
        {
            if(battleStatistics!=null) battleStatistics.Bind(null,null);
            if(deckPlanner!=null) deckPlanner.Bind(null,null,null);
            if (goldIncome != null) goldIncome.Bind(null, null, null);
            if (itemTooltip != null) itemTooltip.Bind(null, null, null, null);
            if(inventory!=null) inventory.Bind(null,null);
            if (unitInfo != null) { unitInfo.Hide(); unitInfo.BindRun(null); }
            if (!isBound)
                return;
            refreshHint.Bind(null,null,null); experienceHint.Bind(null,null,null);
            foreach(ShopItem item in shopItems) item.FailureHint.Bind(null,null,null);
            if (run != null)
            { run.DisplayChanged -= Refresh; run.FailureReported-=feedback.ShowFailure; }
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
            resultPanel.StageSelectionRequested -= OpenStageSelection;
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
