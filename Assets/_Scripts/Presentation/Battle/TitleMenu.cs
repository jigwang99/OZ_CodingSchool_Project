using System;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>타이틀 → 스테이지 선택 → 게임 시작 입력과 로딩 표시.</summary>
    [DisallowMultipleComponent]
    public sealed class TitleMenu : MonoBehaviour
    {
        [SerializeField] private GameNavigationSettings settings;
        [SerializeField] private CanvasGroup titlePanel;
        [SerializeField] private CanvasGroup stagePanel;
        [SerializeField] private Button openStagesButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button stageButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TextMeshProUGUI stageNameText;
        [SerializeField] private TextMeshProUGUI stageDescriptionText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject loadingOverlay;
        [SerializeField] private CanvasGroup interactionRoot;
        private GameSettingsView gameSettings;
        private StageDefinition selection;
        private bool busy;
        public GameNavigationSettings Settings=>settings;
        public bool IsStageSelectionOpen=>stagePanel.gameObject.activeSelf;
        public bool IsLoading=>busy;
        public Button OpenStagesButton=>openStagesButton;
        public Button QuitButton=>quitButton;
        public Button StageButton=>stageButton;
        public Button StartButton=>startButton;
        public Button BackButton=>backButton;
        public TextMeshProUGUI StageNameText=>stageNameText;
        public TextMeshProUGUI StatusText=>statusText;
        public GameSettingsView GameSettings=>gameSettings;
#if UNITY_EDITOR
        public void ConfigureSettingsRoot(CanvasGroup group)=>interactionRoot=group;
        public void Configure(GameNavigationSettings navigation,CanvasGroup title,CanvasGroup stages,
            Button open,Button quit,Button choose,Button start,Button back,TextMeshProUGUI stageName,
            TextMeshProUGUI description,TextMeshProUGUI status,GameObject loading)
        {
            settings=navigation; titlePanel=title; stagePanel=stages; openStagesButton=open; quitButton=quit;
            stageButton=choose; startButton=start; backButton=back; stageNameText=stageName;
            stageDescriptionText=description; statusText=status; loadingOverlay=loading;
        }
#endif
        private void OnEnable()
        {
            if(!Application.isPlaying) return;
            gameSettings=AutoBattler.Managers.GameManager.instance.Settings;
            if(gameSettings!=null) gameSettings.BindContext(interactionRoot,null);
            openStagesButton.onClick.AddListener(OpenStages);
            quitButton.onClick.AddListener(Quit);
            stageButton.onClick.AddListener(SelectStage);
            startButton.onClick.AddListener(StartSelected);
            backButton.onClick.AddListener(Back);
            selection=null; SetBusy(false); Show(titlePanel,stagePanel); statusText.text="";
        }
        private void OpenStages()
        {
            if(busy || GameNavigation.IsTransitioning) return;
            selection=null; startButton.interactable=false;
            stageNameText.text="스테이지를 선택하세요";
            stageDescriptionText.text="현재 1-1 스테이지에 도전할 수 있습니다.";
            stageButton.GetComponent<Image>().color=new Color(.12f,.22f,.3f);
            Show(stagePanel,titlePanel); statusText.text="";
        }
        private void SelectStage()
        {
            if(busy || !IsStageSelectionOpen) return;
            selection=settings.DefaultStage;
            stageNameText.text=$"STAGE {selection.DisplayName}";
            stageDescriptionText.text=selection.Description;
            stageButton.GetComponent<Image>().color=new Color(.14f,.42f,.43f);
            startButton.interactable=true;
        }
        private void Back()
        { if(!busy) { selection=null; Show(titlePanel,stagePanel); statusText.text=""; } }
        private void StartSelected()=>StartSelectedAsync().Forget();
        private async UniTask StartSelectedAsync()
        {
            if(busy || selection==null || GameNavigation.IsTransitioning) return;
            SetBusy(true); statusText.text="";
            try
            {
                if(!await GameNavigation.StartStageAsync(settings,selection) && this!=null)
                    statusText.text="스테이지를 불러올 수 없습니다. 다시 시도하세요.";
            }
            catch(Exception exception)
            {
                Debug.LogException(exception);
                if(this!=null) statusText.text="불러오기에 실패했습니다. 다시 시도하세요.";
            }
            finally { if(this!=null) SetBusy(false); }
        }
        private void SetBusy(bool value)
        {
            busy=value; loadingOverlay.SetActive(value);
            titlePanel.interactable=!value; stagePanel.interactable=!value;
            startButton.interactable=!value && selection!=null;
        }
        private static void Show(CanvasGroup show,CanvasGroup hide)
        {
            hide.DOKill(); hide.gameObject.SetActive(false);
            show.DOKill(); show.gameObject.SetActive(true); show.alpha=0f;
            show.DOFade(1f,.18f).SetUpdate(true);
        }
        private void Quit()
        {
            if(busy || GameNavigation.IsTransitioning) return;
            AutoBattler.Managers.GameManager.instance.QuitGame();
        }
        private void OnDisable()
        {
            if(gameSettings!=null) gameSettings.UnbindContext(interactionRoot);
            if(openStagesButton!=null) openStagesButton.onClick.RemoveListener(OpenStages);
            if(quitButton!=null) quitButton.onClick.RemoveListener(Quit);
            if(stageButton!=null) stageButton.onClick.RemoveListener(SelectStage);
            if(startButton!=null) startButton.onClick.RemoveListener(StartSelected);
            if(backButton!=null) backButton.onClick.RemoveListener(Back);
            if(titlePanel!=null) titlePanel.DOKill();
            if(stagePanel!=null) stagePanel.DOKill();
        }
    }
}
