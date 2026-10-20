using System;
using System.Collections.Generic;
using AutoBattler.Run;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>선택 모달과 독립 토글 버튼. 닫아도 선택 단계·후보 유지.</summary>
    public sealed class AugmentSelectionPanel : MonoBehaviour
    {
        [SerializeField] private GameObject overlay;
        [SerializeField] private CanvasGroup overlayGroup;
        [SerializeField] private Button toggleButton;
        [SerializeField] private TextMeshProUGUI toggleText;
        [SerializeField] private AugmentCard[] cards;
        private bool wasOpen;
        public IReadOnlyList<AugmentCard> Cards=>Array.AsReadOnly(cards);
        public Button ToggleButton=>toggleButton;
        public TextMeshProUGUI ToggleText=>toggleText;
        public bool IsOpen=>gameObject.activeSelf && overlay.activeSelf;
        public event Action<int> Selected;
        public event Action ToggleRequested;
#if UNITY_EDITOR
        public void Configure(GameObject body,CanvasGroup group,Button toggle,TextMeshProUGUI label,AugmentCard[] choices)
        { overlay=body; overlayGroup=group; toggleButton=toggle; toggleText=label; cards=choices; }
#endif
        public void Show(RunController run)
        {
            bool pending=run.IsReady && run.State.Augments.HasPending;
            if(!pending) { gameObject.SetActive(false); return; }
            gameObject.SetActive(true);
            bool open=run.Flow.IsAugmentWindowOpen;
            for(int i=0;i<cards.Length;i++) cards[i].Show(run.State.Data.Augments[run.State.Augments.OfferedIds[i]],i);
            overlay.SetActive(open);
            toggleText.text=open?"Hide choices":"Show choices";
            if(open && !wasOpen) { overlayGroup.DOKill(); overlayGroup.alpha=0f; overlayGroup.DOFade(1f,.15f).SetUpdate(true); }
            wasOpen=open;
        }
        private void OnEnable()
        {
            if(!Application.isPlaying || toggleButton==null) return;
            toggleButton.onClick.AddListener(Toggle);
            foreach(var card in cards) card.Selected+=Choose;
        }
        private void OnDisable()
        {
            if(toggleButton!=null) toggleButton.onClick.RemoveListener(Toggle);
            if(cards!=null) foreach(var card in cards) card.Selected-=Choose;
            if(overlayGroup!=null) overlayGroup.DOKill();
            wasOpen=false;
        }
        private void Toggle()=>ToggleRequested?.Invoke();
        private void Choose(int id)=>Selected?.Invoke(id);
    }
}
