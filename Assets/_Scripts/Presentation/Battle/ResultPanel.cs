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
        [Header("카운트다운 알림 배치")]
        [SerializeField] private Vector2 countdownSize=new Vector2(660f,142f);
        [SerializeField] private float countdownTop=128f;
        private RectLayout normalFrame;
        private RectLayout[] normalLabels;
        private float[] normalFontSizes;
        private bool layoutCaptured;
        private bool compact;
        private bool isPresented;
        private bool automaticCountdown;
        private int countdownSeconds=-1;
        private string itemReward;

        public CanvasGroup CanvasGroup => canvasGroup;
        public TextMeshProUGUI TitleText => titleText;
        public TextMeshProUGUI ResultText => resultText;
        public TextMeshProUGUI EndMessageText => endMessageText;
        public Button NextRoundButton => nextRoundButton;
        public Button RetryButton => retryButton;
        public Button TitleButton => titleButton;

        public void ShowItemReward(string name)
        { if(CanContinue) { itemReward=name; RefreshCaption(); } }
        public void ShowCountdown(float seconds)
        {
            if(!CanContinue) return;
            ApplyCountdownLayout();
            canvasGroup.interactable=false; canvasGroup.blocksRaycasts=false;
            automaticCountdown=true;
            nextRoundButton.interactable=false; nextRoundButton.gameObject.SetActive(false);
            int remaining=Mathf.Max(0,Mathf.CeilToInt(seconds));
            if(countdownSeconds==remaining) return;
            countdownSeconds=remaining; RefreshCaption();
        }
        private void RefreshCaption()
        {
            string countdown=countdownSeconds<0?"":countdownSeconds==0?"다음 전투 준비 중...":$"다음 전투 준비까지 {countdownSeconds}초";
            string reward=string.IsNullOrEmpty(itemReward)?"":$"아이템 획득: {itemReward}";
            endMessageText.text=reward+(reward.Length>0 && countdown.Length>0?" · ":"")+countdown;
            endMessageText.gameObject.SetActive(endMessageText.text.Length>0);
        }
        public void Show(string title, string result, bool canContinue, bool animate = true)
        {
            CaptureLayout(); RestoreLayout();
            automaticCountdown=false; countdownSeconds=-1; itemReward=null;
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
            RestoreLayout();
            CanContinue = false;
            isPresented = false;
            canvasGroup.DOKill();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        private TextMeshProUGUI[] Labels=>new[] {titleText,resultText,endMessageText};
        private void CaptureLayout()
        {
            if(layoutCaptured) return;
            layoutCaptured=true; normalFrame=new RectLayout((RectTransform)transform);
            TextMeshProUGUI[] labels=Labels;
            normalLabels=new RectLayout[labels.Length]; normalFontSizes=new float[labels.Length];
            for(int i=0;i<labels.Length;i++)
            { normalLabels[i]=new RectLayout(labels[i].rectTransform); normalFontSizes[i]=labels[i].fontSize; }
        }
        private void ApplyCountdownLayout()
        {
            CaptureLayout();
            if(compact) return;
            compact=true;
            RectTransform frame=(RectTransform)transform;
            frame.anchorMin=frame.anchorMax=frame.pivot=new Vector2(.5f,1f);
            frame.anchoredPosition=new Vector2(0f,-countdownTop); frame.sizeDelta=countdownSize;
            TextMeshProUGUI[] labels=Labels;
            for(int i=0;i<labels.Length;i++)
            {
                RectTransform rect=labels[i].rectTransform;
                rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0f,1f);
                rect.anchoredPosition=new Vector2(12f,i==0?-8f:i==1?-40f:-104f);
                rect.sizeDelta=new Vector2(countdownSize.x-24f,i==1?48f:32f);
                labels[i].fontSize=i==0?24f:i==1?19f:18f;
            }
        }
        private void RestoreLayout()
        {
            if(!compact) return;
            compact=false; normalFrame.Restore((RectTransform)transform);
            TextMeshProUGUI[] labels=Labels;
            for(int i=0;i<labels.Length;i++)
            { normalLabels[i].Restore(labels[i].rectTransform); labels[i].fontSize=normalFontSizes[i]; }
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
