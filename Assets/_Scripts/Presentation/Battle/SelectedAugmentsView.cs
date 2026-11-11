using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AutoBattler.Data.Augments;
using AutoBattler.Run;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>선택 증강 요약과 호버 설명. 모달이 열리면 호버와 설명 즉시 차단.</summary>
    public sealed class SelectedAugmentsView : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        [SerializeField] private Image hoverArea;
        [SerializeField] private Image[] slots;
        [SerializeField] private TextMeshProUGUI[] slotTexts;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private CanvasGroup tooltip;
        [SerializeField] private TextMeshProUGUI tooltipText;
        [SerializeField,Min(.1f)] private float activationSeconds=1.4f;
        private readonly Dictionary<int,Tween> activations=new Dictionary<int,Tween>();
        private AugmentDefinition[] definitions=Array.Empty<AugmentDefinition>();
        private bool pointerInside,canHover;
        public int SelectedCount { get; private set; }
        public bool IsTooltipVisible=>tooltip.gameObject.activeSelf;
        public bool CanHover=>canHover;
        public TextMeshProUGUI TooltipText=>tooltipText;
        public TextMeshProUGUI CountText=>countText;
        public CanvasGroup Tooltip=>tooltip;
        public void Show(RunController run)=>Show(run.IsReady?run.State.Augments.Selected:Array.Empty<AugmentDefinition>(),
            run.IsReady && !run.Flow.IsAugmentWindowOpen);
        public void Show(IEnumerable<AugmentDefinition> selected,bool allowHover)
        {
            definitions=selected.ToArray(); SelectedCount=definitions.Length;
            countText.text=$"증강 {SelectedCount}/3";
            RefreshActivation();
            StringBuilder text=new StringBuilder();
            if(definitions.Length==0) text.Append("선택한 증강이 없습니다.");
            for(int i=0;i<definitions.Length;i++)
            {
                if(i>0) text.Append("\n\n");
                text.Append("<b>").Append(BattleText.AugmentName(definitions[i])).Append("</b>\n").Append(BattleText.AugmentDescription(definitions[i]));
            }
            tooltipText.text=text.ToString();
            float height=tooltipText.GetPreferredValues(tooltipText.text,388f,Mathf.Infinity).y;
            tooltipText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            ((RectTransform)tooltip.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height+32f);
            canHover=allowHover; hoverArea.raycastTarget=allowHover;
            if(!canHover) { pointerInside=false; HideTooltip(false); }
            else if(pointerInside) ShowTooltip();
        }
        public void ShowActivation(AugmentDefinition definition)
        {
            if(!isActiveAndEnabled || activations.ContainsKey(definition.Id)) return;
            int id=definition.Id;
            activations.Add(id,DOVirtual.DelayedCall(activationSeconds,()=>
            {
                activations.Remove(id);
                RefreshActivation();
            }));
            RefreshActivation();
        }
        private void RefreshActivation()
        {
            for(int i=0;i<slots.Length;i++)
            {
                bool owned=i<definitions.Length;
                bool active=owned && activations.ContainsKey(definitions[i].Id);
                slots[i].color=active?new Color(.42f,.68f,.43f):owned?new Color(.16f,.34f,.36f):new Color(.1f,.13f,.18f);
                slotTexts[i].text=owned?(i+1).ToString():"-";
                slotTexts[i].color=active?Color.white:owned?new Color(1f,.84f,.39f):new Color(.4f,.47f,.53f);
            }
        }
        public void OnPointerEnter(PointerEventData eventData)
        { if(!canHover) return; pointerInside=true; ShowTooltip(); }
        public void OnPointerExit(PointerEventData eventData)
        { pointerInside=false; HideTooltip(true); }
        private void ShowTooltip()
        {
            if(tooltip.gameObject.activeSelf && tooltip.alpha>=1f) return;
            tooltip.DOKill(); tooltip.gameObject.SetActive(true);
            tooltip.DOFade(1f,.12f).SetUpdate(true);
        }
        private void HideTooltip(bool animate)
        {
            tooltip.DOKill();
            if(animate && tooltip.gameObject.activeSelf)
                tooltip.DOFade(0f,.1f).SetUpdate(true).OnComplete(()=>tooltip.gameObject.SetActive(false));
            else { tooltip.alpha=0f; tooltip.gameObject.SetActive(false); }
        }
        private void OnDisable()
        {
            pointerInside=false;
            if(tooltip!=null) HideTooltip(false);
            foreach(Tween activation in activations.Values) activation.Kill();
            activations.Clear();
        }
    }
}
