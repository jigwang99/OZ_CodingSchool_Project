using DG.Tweening;
using TMPro;
using UnityEngine;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>실패 사유를 짧은 토스트 알림으로 표시. 게임 진행·입력에는 영향 없음.</summary>
    public sealed class ActionFeedbackView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup toast;
        [SerializeField] private TextMeshProUGUI toastText;
        [SerializeField,Min(.1f)] private float visibleSeconds=1.8f;
        private Sequence tween;
        public string LastMessage { get; private set; }
        public bool IsToastVisible=>toast.gameObject.activeSelf;
        public TextMeshProUGUI ToastText=>toastText;
        public RectTransform ToastRect=>(RectTransform)toast.transform;
        public void ShowFailure(string message)
        {
            if(string.IsNullOrEmpty(message) || !isActiveAndEnabled) return;
            LastMessage=message;toastText.text=message;
            RectTransform rect=(RectTransform)toast.transform;
            float height=toastText.GetPreferredValues(message,rect.rect.width-28f,Mathf.Infinity).y;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(68f,height+24f));
            tween?.Kill();toast.alpha=0f;toast.gameObject.SetActive(true);transform.SetAsLastSibling();
            tween=DOTween.Sequence().SetUpdate(true).Append(toast.DOFade(1f,.1f))
                .AppendInterval(visibleSeconds).Append(toast.DOFade(0f,.2f))
                .OnComplete(()=>toast.gameObject.SetActive(false));
        }
        private void OnDisable()
        {tween?.Kill();if(toast!=null)toast.gameObject.SetActive(false);}
    }
}
