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
        [SerializeField, Min(0f)] private float fadeDuration = 0.2f;

        public bool CanContinue { get; private set; }
        public event Action ContinueRequested;
        private bool isPresented;

        public CanvasGroup CanvasGroup => canvasGroup;
        public TextMeshProUGUI TitleText => titleText;
        public TextMeshProUGUI ResultText => resultText;
        public TextMeshProUGUI EndMessageText => endMessageText;
        public Button NextRoundButton => nextRoundButton;
#if UNITY_EDITOR
        public void ConfigureReferences(CanvasGroup group, TextMeshProUGUI title,
            TextMeshProUGUI result, TextMeshProUGUI endMessage, Button next)
        { canvasGroup=group; titleText=title; resultText=result; endMessageText=endMessage; nextRoundButton=next; }
#endif

        public void Show(string title, string result, bool canContinue, bool animate = true)
        {
            bool wasVisible = gameObject.activeSelf;
            isPresented = true;
            titleText.text = title;
            resultText.text = result;
            CanContinue = canContinue;
            nextRoundButton.interactable = canContinue;
            nextRoundButton.gameObject.SetActive(canContinue);
            endMessageText.gameObject.SetActive(!canContinue);
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

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                nextRoundButton.onClick.AddListener(RequestContinue);
                if (isPresented)
                    canvasGroup.alpha = 1f;
            }
        }

        private void RequestContinue()
        {
            if (CanContinue && nextRoundButton.IsInteractable())
                ContinueRequested?.Invoke();
        }

        private void OnDisable()
        {
            if (nextRoundButton != null)
                nextRoundButton.onClick.RemoveListener(RequestContinue);
            if (canvasGroup != null)
                canvasGroup.DOKill();
        }
    }
}
