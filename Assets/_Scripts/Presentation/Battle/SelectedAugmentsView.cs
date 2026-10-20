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
        private bool pointerInside,canHover;
        public int SelectedCount { get; private set; }
        public bool IsTooltipVisible=>tooltip.gameObject.activeSelf;
        public bool CanHover=>canHover;
        public TextMeshProUGUI TooltipText=>tooltipText;
        public TextMeshProUGUI CountText=>countText;
        public CanvasGroup Tooltip=>tooltip;
#if UNITY_EDITOR
        public void Configure(Image area,Image[] badges,TextMeshProUGUI[] labels,TextMeshProUGUI count,CanvasGroup popup,TextMeshProUGUI description)
        { hoverArea=area; slots=badges; slotTexts=labels; countText=count; tooltip=popup; tooltipText=description; }
#endif
        public void Show(RunController run)=>Show(run.IsReady?run.State.Augments.Selected:Array.Empty<AugmentDefinition>(),
            run.IsReady && !run.Flow.IsAugmentWindowOpen);
        public void Show(IEnumerable<AugmentDefinition> selected,bool allowHover)
        {
            var definitions=selected.ToArray(); SelectedCount=definitions.Length;
            countText.text=$"AUGMENTS {SelectedCount}/3";
            for(int i=0;i<slots.Length;i++)
            {
                bool owned=i<definitions.Length;
                slots[i].color=owned?new Color(.16f,.34f,.36f):new Color(.1f,.13f,.18f);
                slotTexts[i].text=owned?(i+1).ToString():"-";
                slotTexts[i].color=owned?new Color(1f,.84f,.39f):new Color(.4f,.47f,.53f);
            }
            var text=new StringBuilder();
            if(definitions.Length==0) text.Append("No augments selected yet.\nChoose augments at rounds 1-2, 2-1 and 3-1.");
            for(int i=0;i<definitions.Length;i++)
            {
                if(i>0) text.Append("\n\n");
                text.Append("<b>").Append(definitions[i].Name).Append("</b>\n").Append(definitions[i].Description);
            }
            tooltipText.text=text.ToString();
            float height=tooltipText.GetPreferredValues(tooltipText.text,388f,Mathf.Infinity).y;
            tooltipText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            ((RectTransform)tooltip.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height+32f);
            canHover=allowHover; hoverArea.raycastTarget=allowHover;
            if(!canHover) { pointerInside=false; HideTooltip(false); }
            else if(pointerInside) ShowTooltip();
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
        { pointerInside=false; if(tooltip!=null) HideTooltip(false); }
    }
}
