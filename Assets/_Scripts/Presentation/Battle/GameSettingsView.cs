using AutoBattler.Managers;
using AutoBattler.Run;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>프레임 제한·효과음·게임 종료 설정과 배경 입력 차단.</summary>
    [DisallowMultipleComponent]
    public sealed class GameSettingsView : MonoBehaviour
    {
        public const string Heading = "설정";
        public const string FrameRateHeading = "프레임 제한";
        public const string EffectVolumeHeading = "효과음";
        public const string QuitLabel = "게임 종료";
        public const string CloseLabel = "닫기";

        private CanvasGroup underlying;
        [SerializeField] private CanvasGroup modal;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TMP_Dropdown frameRateDropdown;
        [SerializeField] private Slider effectVolumeSlider;
        [SerializeField] private TextMeshProUGUI effectVolumeLabel;
        private SoundManager sound;
        private static readonly GameFrameRate[] FrameRates =
            { GameFrameRate.Fps30, GameFrameRate.Fps60, GameFrameRate.Fps144 };
        private RunController run;
        private GameManager manager;
        private GameObject previousSelection;
        private bool previousInteraction;
        private bool previousRaycasts;
        private bool previousDragEnabled;

        public bool IsOpen { get; private set; }
        public CanvasGroup Modal => modal;
        public CanvasGroup Underlying => underlying;
        public Button OpenButton => openButton;
        public Button CloseButton => closeButton;
        public Button QuitButton => quitButton;
        public TMP_Dropdown FrameRateDropdown => frameRateDropdown;
        public Slider EffectVolumeSlider => effectVolumeSlider;
        public TextMeshProUGUI EffectVolumeLabel => effectVolumeLabel;

        public void BindContext(CanvasGroup group, RunController controller)
        {
            if(underlying!=group) Close();
            underlying=group; run=controller;
        }
        public void UnbindContext(CanvasGroup group)
        {
            if(underlying!=group) return;
            Close(); underlying=null; run=null;
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
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            quitButton.onClick.AddListener(Quit);
            frameRateDropdown.onValueChanged.AddListener(SelectFrameRate);
            modal.gameObject.SetActive(false);
            RefreshFrameRate(manager.CurrentFrameRate);
        }

        private bool CanOpen => underlying!=null && !GameNavigation.IsTransitioning
            && (run==null || run.IsReady && !run.IsShuttingDown && !run.Flow.IsAugmentWindowOpen && !run.Equipment.IsInventoryOpen);

        private void LateUpdate()
        {
            openButton.interactable=!IsOpen && CanOpen;
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
            if (effectVolumeSlider!=null) effectVolumeSlider.onValueChanged.RemoveListener(SelectEffectVolume);
            if (openButton!=null) openButton.onClick.RemoveListener(Open);
            if (closeButton!=null) closeButton.onClick.RemoveListener(Close);
            if (quitButton!=null) quitButton.onClick.RemoveListener(Quit);
            if (frameRateDropdown!=null) frameRateDropdown.onValueChanged.RemoveListener(SelectFrameRate);
            if (modal!=null) modal.DOKill();
        }
    }
}
