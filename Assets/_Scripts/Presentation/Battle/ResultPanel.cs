using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>전투 결과 표시와 다음 라운드 요청. 진행 상태 변경은 HUD에서 연결.</summary>
    [DisallowMultipleComponent]
    public sealed class ResultPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private TextMeshProUGUI endMessageText;
        [SerializeField] private Button nextRoundButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button titleButton;
        [SerializeField, Min(0f)] private float fadeDuration = 0.2f;

        public bool CanContinue { get; private set; }
        public event Action ContinueRequested;
        public event Action RetryRequested;
        public event Action TitleRequested;
        private bool isPresented;

        public CanvasGroup CanvasGroup => canvasGroup;
        public TextMeshProUGUI TitleText => titleText;
        public TextMeshProUGUI ResultText => resultText;
        public TextMeshProUGUI EndMessageText => endMessageText;
        public Button NextRoundButton => nextRoundButton;
        public Button RetryButton => retryButton;
        public Button TitleButton => titleButton;

        public void ShowItemReward(string name)
        { if(CanContinue) { endMessageText.text=$"아이템 획득: {name}"; endMessageText.gameObject.SetActive(true); } }
        public void Show(string title, string result, bool canContinue, bool animate = true)
        {
            bool wasVisible = gameObject.activeSelf;
            isPresented = true;
            titleText.text = title;
            resultText.text = result;
            CanContinue = canContinue;
            nextRoundButton.interactable = canContinue;
            nextRoundButton.gameObject.SetActive(canContinue);
            endMessageText.text=canContinue?"":"다시 도전하거나 타이틀로 돌아갈 수 있습니다.";
            endMessageText.gameObject.SetActive(!canContinue);
            if(retryButton!=null) retryButton.gameObject.SetActive(!canContinue);
            if(titleButton!=null) titleButton.gameObject.SetActive(!canContinue);
            SetNavigationBusy(false);
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            gameObject.SetActive(true);
            if (!animate || !wasVisible)
            {
                canvasGroup.DOKill();
                canvasGroup.alpha = animate ? 0f : 1f;
                if (animate)
                    canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);
            }
        }

        public void Hide()
        {
            CanContinue = false;
            isPresented = false;
            canvasGroup.DOKill();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        public void StopAnimation() => canvasGroup.DOKill();
        public void SetNavigationBusy(bool busy)
        {
            if(retryButton!=null) retryButton.interactable=!busy;
            if(titleButton!=null) titleButton.interactable=!busy;
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                nextRoundButton.onClick.AddListener(RequestContinue);
                if(retryButton!=null) retryButton.onClick.AddListener(RequestRetry);
                if(titleButton!=null) titleButton.onClick.AddListener(RequestTitle);
                if (isPresented)
                    canvasGroup.alpha = 1f;
            }
        }

        private void RequestContinue()
        {
            if (CanContinue && nextRoundButton.IsInteractable())
                ContinueRequested?.Invoke();
        }
        private void RequestRetry()
        { if(isPresented && gameObject.activeInHierarchy && !CanContinue && retryButton.IsInteractable()) RetryRequested?.Invoke(); }
        private void RequestTitle()
        { if(isPresented && gameObject.activeInHierarchy && !CanContinue && titleButton.IsInteractable()) TitleRequested?.Invoke(); }

        private void OnDisable()
        {
            if (nextRoundButton != null)
                nextRoundButton.onClick.RemoveListener(RequestContinue);
            if(retryButton!=null) retryButton.onClick.RemoveListener(RequestRetry);
            if(titleButton!=null) titleButton.onClick.RemoveListener(RequestTitle);
            if (canvasGroup != null)
                canvasGroup.DOKill();
        }
    }
}
