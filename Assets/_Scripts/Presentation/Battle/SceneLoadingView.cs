using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>씬 전환과 전투 초기화 동안 유지하는 로딩 화면.</summary>
    public sealed class SceneLoadingView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup screen;
        [SerializeField] private TextMeshProUGUI headingText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI percentText;
        [SerializeField] private Image progressFill;
        [SerializeField] private RectTransform spinner;
        [SerializeField,Min(0f)] private float minimumVisibleSeconds=.45f;
        [SerializeField,Min(0f)] private float fadeDuration=.15f;
        private double shownAt;
        public bool IsVisible=>screen.gameObject.activeSelf;
        public float Progress { get; private set; }
        public CanvasGroup Screen=>screen;
        public TextMeshProUGUI HeadingText=>headingText;
        public TextMeshProUGUI StatusText=>statusText;
        public TextMeshProUGUI PercentText=>percentText;
        public Image ProgressFill=>progressFill;
        private void Awake()=>HideImmediately();
        public void Show(string heading)
        {
            screen.DOKill(); spinner.DOKill();
            transform.SetAsLastSibling();
            headingText.text=heading; Progress=0f; shownAt=Time.realtimeSinceStartupAsDouble;
            screen.gameObject.SetActive(true); screen.alpha=1f; screen.interactable=true; screen.blocksRaycasts=true;
            SetProgress(0f,"장면 불러오는 중...");
            spinner.localRotation=Quaternion.identity;
            spinner.DOLocalRotate(new Vector3(0f,0f,-360f),.9f,RotateMode.FastBeyond360)
                .SetEase(Ease.Linear).SetLoops(-1,LoopType.Restart).SetUpdate(true);
        }
        public void SetProgress(float value,string message=null)
        {
            Progress=Mathf.Max(Progress,Mathf.Clamp01(value));
            progressFill.fillAmount=Progress;
            progressFill.rectTransform.anchorMax=new Vector2(Progress,1f);
            percentText.text=$"{Mathf.FloorToInt(Progress*100f)}%";
            if(message!=null) statusText.text=message;
        }
        public async UniTask HideAsync(bool completed,CancellationToken token)
        {
            if(!IsVisible) return;
            try
            {
                if(completed) SetProgress(1f,"완료");
                else statusText.text="불러오기를 완료하지 못했습니다.";
                double remaining=minimumVisibleSeconds-(Time.realtimeSinceStartupAsDouble-shownAt);
                if(remaining>0) await UniTask.Delay(TimeSpan.FromSeconds(remaining),ignoreTimeScale:true,cancellationToken:token);
                if(this==null || token.IsCancellationRequested) return;
                screen.DOFade(0f,fadeDuration).SetUpdate(true);
                if(fadeDuration>0) await UniTask.Delay(TimeSpan.FromSeconds(fadeDuration),ignoreTimeScale:true,cancellationToken:token);
            }
            finally { if(this!=null) HideImmediately(); }
        }
        public void HideImmediately()
        {
            screen.DOKill(); spinner.DOKill();
            screen.alpha=0f; screen.interactable=false; screen.blocksRaycasts=false; screen.gameObject.SetActive(false);
        }
        private void OnDisable()=>HideImmediately();
    }
}
