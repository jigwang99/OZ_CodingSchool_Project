using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Units;
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
    public sealed class RunHUD : MonoBehaviour
    {
        [Header("진행 제어")]
        [SerializeField] private RunController run;
        [SerializeField] private GameNavigationSettings navigation;

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
        [SerializeField] private Button sellButton;
        [SerializeField] private TextMeshProUGUI selectedUnitText;
        [SerializeField] private SynergyPanel synergyPanel;
        [SerializeField] private AugmentSelectionPanel augmentPanel;
        [SerializeField] private SelectedAugmentsView selectedAugments;
        [SerializeField] private UnitInfoPanel unitInfo;

        [Header("전투와 결과")]
        [SerializeField] private Button startBattleButton;
        [SerializeField] private ResultPanel resultPanel;
        [SerializeField] private Button retryLoadButton;
        [SerializeField, Min(0f)] private float fadeDuration = 0.2f;

        private bool isBound;
        private bool hasDisplayedPhase;
        private BattlePhase displayedPhase;

        public RunController Controller => run;
        public GameNavigationSettings Navigation=>navigation;
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
        public Button SellButton => sellButton;
        public TextMeshProUGUI SelectedUnitText => selectedUnitText;
        public Button StartBattleButton => startBattleButton;
        public Button RetryLoadButton => retryLoadButton;
        public ResultPanel ResultPanel => resultPanel;
        public SynergyPanel SynergyPanel => synergyPanel;
        public AugmentSelectionPanel AugmentPanel=>augmentPanel;
        public SelectedAugmentsView SelectedAugments=>selectedAugments;
        public UnitInfoPanel UnitInfo => unitInfo;

#if UNITY_EDITOR
        // 셋업 도구는 필드 이름 조회 대신 명시적인 참조 주입 사용.
        public void ConfigureHeader(TextMeshProUGUI round, TextMeshProUGUI gold, TextMeshProUGUI lives,
            TextMeshProUGUI level, TextMeshProUGUI experience, TextMeshProUGUI deployment, TextMeshProUGUI message)
        {
            roundText=round; goldText=gold; livesText=lives; levelText=level;
            experienceText=experience; deploymentText=deployment; messageText=message;
        }
        public void ConfigurePreparation(CanvasGroup panel, RectTransform container, ShopItem[] items,
            Button refresh, TextMeshProUGUI refreshText, Button buyExperience, TextMeshProUGUI xpText,
            Button sell, TextMeshProUGUI selected)
        {
            preparationPanel=panel; shopContainer=container; shopItems=items;
            refreshButton=refresh; refreshLabel=refreshText; buyExperienceButton=buyExperience;
            buyExperienceLabel=xpText; sellButton=sell; selectedUnitText=selected;
        }
        public void ConfigureActions(Button start, Button retry, ResultPanel result)
        { startBattleButton=start; retryLoadButton=retry; resultPanel=result; }
        public void ConfigureShopContainer(RectTransform container) => shopContainer=container;
        public void ConfigureShopItems(ShopItem[] items) => shopItems=items;
        public void ConfigureResultPanel(ResultPanel panel) => resultPanel=panel;
        public void ConfigureSynergies(SynergyPanel panel) => synergyPanel=panel;
        public void ConfigureAugments(AugmentSelectionPanel panel)=>augmentPanel=panel;
        public void ConfigureSelectedAugments(SelectedAugmentsView view)=>selectedAugments=view;
        public void ConfigureUnitInfo(UnitInfoPanel panel) => unitInfo = panel;
        public void ConfigureNavigation(GameNavigationSettings settings)=>navigation=settings;
#endif

        public void ConfigureScene(RunController controller)
        {
            Unbind();
            run = controller;
            if (Application.isPlaying && isActiveAndEnabled)
                Bind();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                Bind();
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
            sellButton.onClick.AddListener(SellSelected);
            resultPanel.ContinueRequested += NextRound;
            resultPanel.RetryRequested += RetryRun;
            resultPanel.TitleRequested += ReturnToTitle;
            retryLoadButton.onClick.AddListener(RetryLoad);
            run.DisplayChanged += Refresh;
            if(augmentPanel!=null) { augmentPanel.Selected+=SelectAugment; augmentPanel.ToggleRequested+=ToggleAugments; }
            isBound = true;
            hasDisplayedPhase = false;
            Refresh();
        }

        private void StartBattle() => run.StartBattle();
        private void BuyShopItem(int slotIndex) => run.Buy(slotIndex);
        private void RefreshShop() => run.RefreshShop();
        private void BuyExperience() => run.BuyXp();
        private void SellSelected() => run.SellSelected();
        private void NextRound() => run.Continue();
        private void RetryLoad() => run.RetryLoad();
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
                if(!loaded && this!=null) messageText.text="Scene unavailable. Please retry.";
            }
            catch(System.Exception exception)
            { if(this!=null) messageText.text="Scene load failed. Please retry."; Debug.LogException(exception); }
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
                messageText.text = $"Automatic battle   {Mathf.CeilToInt(run.RemainingSeconds)}s remaining";
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            var selected = run.Drag.SelectedUnit;
            var unit = selected != null && selected.State != null
                ? selected.GetComponent<Unit>() : null;
            selectedUnitText.text = unit != null && unit.Definition != null
                ? unit.Definition.Name : "Select a unit";
            sellButton.interactable = CanPrepare() && unit != null;
            if (unitInfo != null)
            {
                var inspected = run.IsReady && run.Flow.CanInspectUnits ? run.Drag.InspectedUnit : null;
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
            roundText.text = ready ? $"Round {(run.State.Round - 1) / 4 + 1}-{(run.State.Round - 1) % 4 + 1}" : "Round --";
            goldText.text = ready ? $"Gold {run.State.Gold}" : "Gold --";
            livesText.text = ready ? $"Lives {run.State.Lives}" : "Lives --";
            levelText.text = ready ? $"Level {run.State.Level}" : "Level --";
            experienceText.text = ready
                ? run.State.Level == 10 ? "XP MAX" : $"XP {run.State.Xp}/{run.State.Data.RequiredXp(run.State.Level)}"
                : "XP --";
            deploymentText.text = ready ? $"Deployed {run.State.Board.DeployedCount}/{run.State.Level}" : "Deployed --";

            for (int i = 0; i < shopItems.Length; i++)
            {
                int id = ready ? run.State.Shop[i] : 0;
                var unit = id == 0 ? null : run.State.Data.Units[id];
                var icon = unit == null ? null : run.UnitIcon(unit.IconId);
                shopItems[i].Show(unit, icon, id == 0 ? 0 : run.State.Stock(id), ready ? run.State.Gold : 0, prepare);
            }

            // 비용 표시도 시트의 경제 규칙을 사용.
            refreshLabel.text = ready ? $"Refresh ({run.State.Data.IntRule(EconomyRule.RefreshCost)}G)" : "Refresh";
            buyExperienceLabel.text = ready ? $"XP ({run.State.Data.IntRule(EconomyRule.XpCost)}G)" : "Buy XP";
            refreshButton.interactable = prepare;
            buyExperienceButton.interactable = prepare && run.State.Level < 10;
            startBattleButton.interactable = prepare && run.State.Board.DeployedCount > 0;
            startBattleButton.gameObject.SetActive(ready);
            retryLoadButton.gameObject.SetActive(!ready && !run.IsLoading);
            preparationPanel.interactable = prepare;
            RefreshSelection();

            if (ready && run.State.IsOver)
                messageText.text = run.State.WonRun ? "Challenge complete!" : "Challenge ended. Retry or return to title.";

            bool showResult = ready && run.Flow.CurrentPhase == BattlePhase.Result;
            bool phaseChanged = !hasDisplayedPhase || displayedPhase != run.Flow.CurrentPhase;
            bool animate = hasDisplayedPhase && phaseChanged;
            if (showResult)
            {
                string title = run.State.IsOver
                    ? run.State.WonRun ? "Challenge complete!" : "Challenge ended"
                    : "Battle result";
                resultPanel.Show(title, run.State.LastResult, !run.State.IsOver, hasDisplayedPhase);
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
            if (unitInfo != null) unitInfo.Hide();
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
            sellButton.onClick.RemoveListener(SellSelected);
            resultPanel.ContinueRequested -= NextRound;
            resultPanel.RetryRequested -= RetryRun;
            resultPanel.TitleRequested -= ReturnToTitle;
            retryLoadButton.onClick.RemoveListener(RetryLoad);
            preparationPanel.DOKill();
            resultPanel.StopAnimation();
            isBound = false;
        }

        private void OnDisable() => Unbind();
    }
}
