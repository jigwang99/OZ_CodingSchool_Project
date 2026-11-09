using System;
using AutoBattler.Managers;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>타이틀·지도 선택과 상세 패널의 이어하기·새로하기 명령 연결.</summary>
    [DisallowMultipleComponent]
    public sealed class TitleMenu : MonoBehaviour
    {
        [SerializeField] private GameNavigationSettings settings;
        [SerializeField] private CanvasGroup titlePanel;
        [SerializeField] private CanvasGroup stagePanel;
        [FormerlySerializedAs("openStagesButton")]
        [SerializeField] private Button gameStartButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button stageButton;
        [SerializeField] private StageSelectionView stageSelection;
        [SerializeField] private StageDetailsPanel detailsPanel;
        [SerializeField] private Button backButton;

        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private CanvasGroup interactionRoot;
        private SaveManager saves;
        private GameSettingsView gameSettings;
        private StageDefinition selection;
        private bool busy;
        public GameNavigationSettings Settings=>settings;
        public bool IsStageSelectionOpen=>stagePanel.gameObject.activeSelf;
        public bool IsLoading=>busy;
        public Button GameStartButton=>gameStartButton;
        public Button QuitButton=>quitButton;
        public Button StageButton=>stageButton;
        public StageSelectionView StageSelection=>stageSelection;
        public StageDefinition SelectedStage=>selection;
        public StageDetailsPanel DetailsPanel=>detailsPanel;
        public Button StartButton=>detailsPanel.NewRunButton;
        public Button BackButton=>backButton;
        public TextMeshProUGUI StageNameText=>detailsPanel.StageNameText;
        public TextMeshProUGUI StatusText=>statusText;
        public GameSettingsView GameSettings=>gameSettings;
#if UNITY_EDITOR
        // 이전 로컬 검사 도구의 버튼 참조 호환. 실제 화면에는 게임 시작 버튼 하나만 존재.
        public Button OpenStagesButton=>gameStartButton;
        public Button ContinueButton=>detailsPanel.ContinueButton;
#endif
        private void OnEnable()
        {
            if(!Application.isPlaying) return;
            SoundManager.instance.PlayMenuMusicAsync().Forget();
            saves=SaveManager.instance; saves.ConfigureProgression(settings); saves.Refresh();
            stageSelection.BindProgress(saves);
            detailsPanel.Bind(settings,saves);
            gameSettings=GameManager.instance.Settings;
            if(gameSettings!=null) gameSettings.BindContext(interactionRoot,null,settings,ShowStageSelection);
            gameStartButton.onClick.AddListener(StartGame);
            quitButton.onClick.AddListener(Quit);
            stageSelection.Selected+=SelectStage;
            detailsPanel.ContinueRequested+=ResumeSelected;
            detailsPanel.NewRunRequested+=StartSelected;
            backButton.onClick.AddListener(Back);
            selection=null; SetBusy(false); statusText.text=saves.LastError??"";
            if(GameNavigation.DestinationPage==MenuPage.StageSelection) OpenStageSelection(true);
            else Show(titlePanel,stagePanel);
        }
        private void StartGame()
        {
            if(busy || GameNavigation.IsTransitioning) return;
            saves.Refresh(); OpenStageSelection();
        }
        private void ShowStageSelection()=>OpenStageSelection();
        public bool OpenStageSelection(bool allowNavigationTransition=false)
        {
            if(busy || !allowNavigationTransition && GameNavigation.IsTransitioning) return false;
            selection=null; detailsPanel.Hide();
            Show(stagePanel,titlePanel); statusText.text=saves.LastError??"";
            int chapter=allowNavigationTransition?GameNavigation.MenuChapter
                :settings.TryGetStage(saves.SavedStageId,out StageDefinition saved)?saved.Chapter:settings.DefaultStage.Chapter;
            stageSelection.Open(chapter,allowNavigationTransition); return true;
        }
        private void SelectStage(StageDefinition stage)
        {
            if(busy || !IsStageSelectionOpen) return;
            selection=stage;
            if(selection==null) detailsPanel.Hide();
            else detailsPanel.Show(selection);
        }
        private void Back()
        {if(!busy && !GameNavigation.IsTransitioning) {selection=null; detailsPanel.Hide(); Show(titlePanel,stagePanel); statusText.text="";}}
        private void StartSelected()=>StartSelectedAsync(false).Forget();
        private void ResumeSelected()=>StartSelectedAsync(true).Forget();
        private async UniTask StartSelectedAsync(bool resume)
        {
            if(busy || selection==null || GameNavigation.IsTransitioning || !saves.IsStageUnlocked(settings,selection)) return;
            if(resume && (!saves.HasSavedRun || saves.SavedStageId!=selection.Id)) return;
            SetBusy(true); statusText.text="";
            try
            {
                bool loaded=resume?await GameNavigation.ResumeStageAsync(settings):await GameNavigation.StartStageAsync(settings,selection);
                if(!loaded && this!=null) statusText.text="스테이지를 불러올 수 없습니다. 다시 시도하세요.";
            }
            catch(Exception exception)
            {Debug.LogException(exception); if(this!=null) statusText.text="불러오기에 실패했습니다. 다시 시도하세요.";}
            finally {if(this!=null) SetBusy(false);}
        }
        private void SetBusy(bool value)
        {
            busy=value; titlePanel.interactable=!value; stagePanel.interactable=!value;
            gameStartButton.interactable=!value;
            detailsPanel.SetInteractable(!value);
            stageSelection.SetInteractable(!value);
        }
        private static void Show(CanvasGroup show,CanvasGroup hide)
        {
            hide.DOKill(); hide.gameObject.SetActive(false);
            show.DOKill(); show.gameObject.SetActive(true); show.alpha=0f;
            show.DOFade(1f,.18f).SetUpdate(true);
        }
        private void Quit()
        {if(!busy && !GameNavigation.IsTransitioning) GameManager.instance.QuitGame();}
        private void OnDisable()
        {
            if(gameSettings!=null) gameSettings.UnbindContext(interactionRoot);
            if(gameStartButton!=null) gameStartButton.onClick.RemoveListener(StartGame);
            if(quitButton!=null) quitButton.onClick.RemoveListener(Quit);
            if(stageSelection!=null) stageSelection.Selected-=SelectStage;
            if(detailsPanel!=null) {detailsPanel.ContinueRequested-=ResumeSelected; detailsPanel.NewRunRequested-=StartSelected;}
            if(backButton!=null) backButton.onClick.RemoveListener(Back);
            if(titlePanel!=null) titlePanel.DOKill();
            if(stagePanel!=null) stagePanel.DOKill();
        }
    }
}
