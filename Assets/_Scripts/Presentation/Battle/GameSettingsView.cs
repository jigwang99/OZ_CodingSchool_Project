using AutoBattler.Managers;
using AutoBattler.Run;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>프레임 제한·효과음·배경음악·게임 종료 설정과 배경 입력 차단.</summary>
    [DisallowMultipleComponent]
    public sealed class GameSettingsView : MonoBehaviour
    {
        public const string Heading = "설정";
        public const string FrameRateHeading = "프레임 제한";
        public const string EffectVolumeHeading = "효과음";
        public const string MusicVolumeHeading = "배경음악";
        public const string QuitLabel = "게임 종료";
        public const string StageSelectionLabel = "스테이지 선택";
        public const string CloseLabel = "닫기";

        private CanvasGroup underlying;
        [SerializeField] private CanvasGroup modal;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button stageSelectionButton;
        [SerializeField] private TMP_Dropdown frameRateDropdown;
        [SerializeField] private Slider effectVolumeSlider;
        [SerializeField] private TextMeshProUGUI effectVolumeLabel;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private TextMeshProUGUI musicVolumeLabel;
        private SoundManager sound;
        private static readonly GameFrameRate[] FrameRates =
            { GameFrameRate.Fps30, GameFrameRate.Fps60, GameFrameRate.Fps144 };
        private RunController run;
        private GameManager manager;
        private GameObject previousSelection;
        private bool previousInteraction;
        private bool previousRaycasts;
        private bool previousDragEnabled;
        private GameNavigationSettings navigation;
        private System.Action showStageSelection;
        private bool navigating;

        public bool IsOpen { get; private set; }
        public CanvasGroup Modal => modal;
        public CanvasGroup Underlying => underlying;
        public Button OpenButton => openButton;
        public Button CloseButton => closeButton;
        public Button QuitButton => quitButton;
        public Button StageSelectionButton => stageSelectionButton;
        public TMP_Dropdown FrameRateDropdown => frameRateDropdown;
        public Slider EffectVolumeSlider => effectVolumeSlider;
        public TextMeshProUGUI EffectVolumeLabel => effectVolumeLabel;
        public Slider MusicVolumeSlider=>musicVolumeSlider;
        public TextMeshProUGUI MusicVolumeLabel=>musicVolumeLabel;

        public void BindContext(CanvasGroup group, RunController controller,GameNavigationSettings settings=null,System.Action openStageSelection=null)
        {
            if(underlying!=group) Close();
            underlying=group; run=controller; navigation=settings; showStageSelection=openStageSelection;
        }
        public void UnbindContext(CanvasGroup group)
        {
            if(underlying!=group) return;
            Close(); underlying=null; run=null; navigation=null; showStageSelection=null;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            manager=GameManager.instance;
            // 씬 재진입으로 생긴 중복 프리팹은 기존 설정 UI에 연결하지 않음.
            if(manager.Settings!=this) { gameObject.SetActive(false); return; }
            manager.FrameRateChanged+=RefreshFrameRate;
            sound=SoundManager.instance;
            sound.VolumeChanged+=RefreshEffectVolume;
            effectVolumeSlider.onValueChanged.AddListener(SelectEffectVolume);
            RefreshEffectVolume(sound.EffectVolume);
            sound.MusicVolumeChanged+=RefreshMusicVolume;
            musicVolumeSlider.onValueChanged.AddListener(SelectMusicVolume);
            RefreshMusicVolume(sound.MusicVolume);
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            quitButton.onClick.AddListener(Quit);
            stageSelectionButton.onClick.AddListener(OpenStageSelection);
            frameRateDropdown.onValueChanged.AddListener(SelectFrameRate);
            modal.gameObject.SetActive(false);
            RefreshFrameRate(manager.CurrentFrameRate);
        }

        private bool CanOpen => underlying!=null && !GameNavigation.IsTransitioning
            && (run==null || run.IsReady && !run.IsShuttingDown && !run.Flow.IsAugmentWindowOpen);
        private bool CanOpenStageSelection=>IsOpen && !navigating && !GameNavigation.IsTransitioning && navigation!=null;

        private void LateUpdate()
        {
            openButton.interactable=!IsOpen && CanOpen;
            stageSelectionButton.interactable=CanOpenStageSelection;
        }

        public void Open()
        {
            if (IsOpen || !CanOpen) return;
            run?.Drag.CancelDrag();
            previousDragEnabled=run!=null && run.Drag.enabled;
            if(run!=null) run.Drag.enabled=false;
            previousInteraction=underlying.interactable;
            previousRaycasts=underlying.blocksRaycasts;
            previousSelection=EventSystem.current!=null ? EventSystem.current.currentSelectedGameObject : null;
            underlying.interactable=false;
            underlying.blocksRaycasts=false;
            IsOpen=true;
            stageSelectionButton.interactable=CanOpenStageSelection;
            modal.ignoreParentGroups=true;
            modal.transform.SetAsLastSibling();
            modal.gameObject.SetActive(true);
            modal.interactable=true;
            modal.blocksRaycasts=true;
            modal.DOKill(); modal.alpha=0f;
            modal.DOFade(1f,.15f).SetUpdate(true);
            if (EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            frameRateDropdown.Hide();
            IsOpen=false;
            modal.DOKill(); modal.gameObject.SetActive(false);
            if(underlying!=null)
            { underlying.interactable=previousInteraction; underlying.blocksRaycasts=previousRaycasts; }
            if(run!=null && !run.IsShuttingDown) run.Drag.enabled=previousDragEnabled;
            openButton.interactable=CanOpen;
            if (EventSystem.current!=null)
                EventSystem.current.SetSelectedGameObject(previousSelection!=null && previousSelection.activeInHierarchy
                    ? previousSelection : null);
            previousSelection=null;
        }

        private void SelectEffectVolume(float value) => sound.SetEffectVolume(value);
        private void SelectMusicVolume(float value)=>sound.SetMusicVolume(value);
        private void RefreshMusicVolume(float value)
        {
            musicVolumeSlider.SetValueWithoutNotify(value);
            musicVolumeLabel.text=$"{Mathf.RoundToInt(value*100f)}%";
        }
        private void RefreshEffectVolume(float value)
        {
            effectVolumeSlider.SetValueWithoutNotify(value);
            effectVolumeLabel.text=$"{Mathf.RoundToInt(value*100f)}%";
        }

        private void SelectFrameRate(int index)
        {
            if(index<0 || index>=FrameRates.Length) return;
            manager.SetFrameRate(FrameRates[index]);
        }
        private void Quit() { if (IsOpen) manager.QuitGame(); }
        private void OpenStageSelection()=>OpenStageSelectionAsync().Forget();
        private async UniTask OpenStageSelectionAsync()
        {
            if(!IsOpen || navigating || GameNavigation.IsTransitioning || navigation==null) return;
            navigating=true;
            RunController current=run; GameNavigationSettings settings=navigation; System.Action open=showStageSelection;
            try
            {
                Close();
                if(current==null && open!=null) open();
                else if(!await GameNavigation.OpenStageSelectionAsync(settings,current) && underlying!=null) Open();
            }
            catch(System.Exception exception) { Debug.LogException(exception); if(underlying!=null) Open(); }
            finally {navigating=false;}
        }

        private void RefreshFrameRate(GameFrameRate value)
        {
            int index=System.Array.IndexOf(FrameRates,value);
            frameRateDropdown.SetValueWithoutNotify(index>=0 ? index : 1);
        }

        private void OnDisable()
        {
            Close();
            if (manager!=null) manager.FrameRateChanged-=RefreshFrameRate;
            if (sound!=null) sound.VolumeChanged-=RefreshEffectVolume;
            if(sound!=null)sound.MusicVolumeChanged-=RefreshMusicVolume;
            if(musicVolumeSlider!=null)musicVolumeSlider.onValueChanged.RemoveListener(SelectMusicVolume);
            if (effectVolumeSlider!=null) effectVolumeSlider.onValueChanged.RemoveListener(SelectEffectVolume);
            if (openButton!=null) openButton.onClick.RemoveListener(Open);
            if (closeButton!=null) closeButton.onClick.RemoveListener(Close);
            if (quitButton!=null) quitButton.onClick.RemoveListener(Quit);
            if (stageSelectionButton!=null) stageSelectionButton.onClick.RemoveListener(OpenStageSelection);
            if (frameRateDropdown!=null) frameRateDropdown.onValueChanged.RemoveListener(SelectFrameRate);
            if (modal!=null) modal.DOKill();
        }
    }
}
