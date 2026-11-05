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
        [Header("결과 이미지")]
        [SerializeField] private Image outcomeImage;
        [SerializeField] private Sprite victorySprite;
        [SerializeField] private Sprite defeatSprite;
        [SerializeField, Min(0f)] private float fadeDuration = 0.2f;

        public bool CanContinue { get; private set; }
        public event Action ContinueRequested;
        public event Action RetryRequested;
        public event Action TitleRequested;
        [Header("카운트다운 알림 배치")]
        [SerializeField] private Vector2 countdownSize=new Vector2(420f,120f);
        [SerializeField] private float countdownTop=128f;
        private RectLayout normalFrame;
        private RectLayout normalOutcome;
        private RectLayout normalTitle;
        private float normalTitleFontSize;
        private bool layoutCaptured;
        private bool compact;
        private bool isPresented;
        private bool automaticCountdown;

        public CanvasGroup CanvasGroup => canvasGroup;
        public TextMeshProUGUI TitleText => titleText;
        public TextMeshProUGUI ResultText => resultText;
        public TextMeshProUGUI EndMessageText => endMessageText;
        public Button NextRoundButton => nextRoundButton;
        public Button RetryButton => retryButton;
        public Button TitleButton => titleButton;
        public Image OutcomeImage => outcomeImage;
        public void SetOutcome(bool won)
        {
            if (outcomeImage != null) outcomeImage.sprite = won ? victorySprite : defeatSprite;
            titleText.text = won ? "승리" : "패배";
        }

        public void ShowCountdown()
        {
            if(!CanContinue || automaticCountdown) return;
            ApplyCountdownLayout();
            canvasGroup.interactable=false; canvasGroup.blocksRaycasts=false;
            automaticCountdown=true;
            nextRoundButton.interactable=false; nextRoundButton.gameObject.SetActive(false);
        }
        public void Show(bool won, bool canContinue, bool animate = true)
        {
            CaptureLayout(); RestoreLayout();
            automaticCountdown=false;
            bool wasVisible = gameObject.activeSelf;
            isPresented = true;
            SetOutcome(won);
            resultText.text = string.Empty;
            resultText.gameObject.SetActive(false);
            CanContinue = canContinue;
            nextRoundButton.interactable = canContinue;
            nextRoundButton.gameObject.SetActive(canContinue);
            endMessageText.text=string.Empty;
            endMessageText.gameObject.SetActive(false);
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
            RestoreLayout();
            CanContinue = false;
            isPresented = false;
            canvasGroup.DOKill();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        private void CaptureLayout()
        {
            if(layoutCaptured) return;
            layoutCaptured=true; normalFrame=new RectLayout((RectTransform)transform);
            if(outcomeImage!=null) normalOutcome=new RectLayout(outcomeImage.rectTransform);
            normalTitle=new RectLayout(titleText.rectTransform); normalTitleFontSize=titleText.fontSize;
        }
        private void ApplyCountdownLayout()
        {
            CaptureLayout();
            if(compact) return;
            compact=true;
            RectTransform frame=(RectTransform)transform;
            frame.anchorMin=frame.anchorMax=frame.pivot=new Vector2(.5f,1f);
            frame.anchoredPosition=new Vector2(0f,-countdownTop); frame.sizeDelta=countdownSize;
            if(outcomeImage!=null)
            {
                RectTransform outcome=outcomeImage.rectTransform;
                outcome.anchorMin=outcome.anchorMax=outcome.pivot=new Vector2(.5f,1f);
                outcome.anchoredPosition=new Vector2(0f,-8f); outcome.sizeDelta=new Vector2(240f,62f);
            }
            RectTransform title=titleText.rectTransform;
            title.anchorMin=title.anchorMax=title.pivot=new Vector2(.5f,0f);
            title.anchoredPosition=new Vector2(0f,10f); title.sizeDelta=new Vector2(countdownSize.x-24f,32f);
            titleText.fontSize=26f;
        }
        private void RestoreLayout()
        {
            if(!compact) return;
            compact=false; normalFrame.Restore((RectTransform)transform);
            if(outcomeImage!=null) normalOutcome.Restore(outcomeImage.rectTransform);
            normalTitle.Restore(titleText.rectTransform); titleText.fontSize=normalTitleFontSize;
        }
        private readonly struct RectLayout
        {
            private readonly Vector2 anchorMin,anchorMax,pivot,position,size;
            public RectLayout(RectTransform rect)
            { anchorMin=rect.anchorMin; anchorMax=rect.anchorMax; pivot=rect.pivot; position=rect.anchoredPosition; size=rect.sizeDelta; }
            public void Restore(RectTransform rect)
            { rect.anchorMin=anchorMin; rect.anchorMax=anchorMax; rect.pivot=pivot; rect.anchoredPosition=position; rect.sizeDelta=size; }
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
            if (!automaticCountdown && CanContinue && nextRoundButton.IsInteractable())
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
