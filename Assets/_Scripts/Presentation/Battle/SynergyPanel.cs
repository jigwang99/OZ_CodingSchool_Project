using System.Collections.Generic;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Traits;
using AutoBattler.Data.Traits;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>진행 시스템의 읽기 전용 집계 결과를 표시. 집계·효과 적용 책임은 전투 시스템에 유지.</summary>
    [DisallowMultipleComponent]
    public sealed class SynergyPanel : MonoBehaviour
    {
        [SerializeField] private GameObject content;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI emptyText;
        [SerializeField] private TraitRowView[] rows;
        [SerializeField] private RectTransform originsTitle;
        [SerializeField] private RectTransform classesTitle;
        [SerializeField] private RectTransform rules;
        [SerializeField] private ScrollRect scroll;
        [SerializeField,Min(148f)] private float maximumHeight=710f;
        [Header("시너지 호버 설명")]
        [SerializeField] private CanvasGroup tooltip;
        [SerializeField] private TextMeshProUGUI tooltipText;
        private readonly Vector3[] tooltipCorners=new Vector3[4];
        private Canvas tooltipCanvas;
        private TraitRowView hoveredRow;
        private TraitSnapshot displayed;
        private BattlePhase displayedPhase;
        private bool hasShown,wasReady;
        public IReadOnlyList<TraitRowView> Rows=>System.Array.AsReadOnly(rows);
        public TextMeshProUGUI StatusText=>statusText;
        public TextMeshProUGUI EmptyText=>emptyText;
        public TraitSnapshot DisplayedSnapshot=>displayed;
        public bool IsDataAvailable=>content.activeSelf;
        public CanvasGroup Tooltip=>tooltip;
        public TextMeshProUGUI TooltipText=>tooltipText;
        private void Awake()=>tooltipCanvas=GetComponentInParent<Canvas>()?.rootCanvas;
        public void Show(TraitSnapshot snapshot,bool ready,BattlePhase phase)
        {
            snapshot=snapshot ?? TraitSnapshot.Empty;
            if(hasShown && ReferenceEquals(displayed,snapshot) && wasReady==ready && displayedPhase==phase) return;
            displayed=snapshot; displayedPhase=phase; wasReady=ready; hasShown=true;
            bool available=ready && snapshot.Statuses.Count>0;
            if(!available) ClearTooltip();
            content.SetActive(available);
            emptyText.text=ready?"시너지 정보가 없음.":"시너지 불러오는 중...";
            statusText.text=string.Empty;
            statusText.gameObject.SetActive(false);
            rules.gameObject.SetActive(false);
            // 숨긴 행의 자리를 남기지 않고 그룹과 행을 위에서부터 배치.
            bool anyVisible=false;
            if(available) foreach(TraitRowView row in rows)
            {
                row.Show(snapshot);
                anyVisible|=row.gameObject.activeSelf;
            }
            emptyText.gameObject.SetActive(!anyVisible);
            if(available && !anyVisible) emptyText.text="기물을 배치하면 시너지 표시.";
            float top=scroll!=null?0f:52f;
            LayoutGroup(TraitCategory.Origin,originsTitle,available,ref top);
            LayoutGroup(TraitCategory.Class,classesTitle,available,ref top);
            float height=anyVisible?top+8f+(scroll!=null?52f:0f):148f;
            ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,scroll!=null?Mathf.Min(height,maximumHeight):height);
            if(scroll!=null)
            {
                scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,top+8f);
                scroll.vertical=height>maximumHeight;
                if(!scroll.vertical) {scroll.StopMovement(); scroll.verticalNormalizedPosition=1f;}
            }
        }
        public void ShowTooltip(TraitRowView row)
        {
            if(tooltip==null || !isActiveAndEnabled || !row.isActiveAndEnabled || row.Count==0) return;
            bool opening=hoveredRow!=row || !tooltip.gameObject.activeSelf;
            hoveredRow=row;
            tooltipText.text=$"<b>{row.NameText.text}</b>\n{row.TooltipDescription}";
            float width=((RectTransform)tooltip.transform).rect.width-32f;
            float height=tooltipText.GetPreferredValues(tooltipText.text,width,Mathf.Infinity).y;
            tooltipText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            ((RectTransform)tooltip.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height+32f);
            tooltip.gameObject.SetActive(true);
            tooltip.transform.SetAsLastSibling();
            PositionTooltip();
            if(opening)
            {
                tooltip.DOKill(); tooltip.alpha=0f;
                tooltip.DOFade(1f,.1f).SetUpdate(true);
            }
        }
        public void RefreshTooltip(TraitRowView row)
        { if(hoveredRow==row) ShowTooltip(row); }
        public void HideTooltip(TraitRowView row)
        { if(hoveredRow==row) ClearTooltip(); }
        private void ClearTooltip()
        {
            hoveredRow=null;
            if(tooltip==null) return;
            tooltip.DOKill(); tooltip.alpha=0f; tooltip.gameObject.SetActive(false);
        }
        private void LateUpdate()
        { if(hoveredRow!=null) PositionTooltip(); }
        private void PositionTooltip()
        {
            RectTransform row=(RectTransform)hoveredRow.transform;
            RectTransform popup=(RectTransform)tooltip.transform;
            RectTransform panel=(RectTransform)transform;
            Vector3 top=panel.InverseTransformPoint(row.TransformPoint(new Vector3(row.rect.xMax,row.rect.yMax,0f)));
            popup.position=panel.TransformPoint(new Vector3(panel.rect.xMax+12f,top.y,0f));
            if(tooltipCanvas==null) return;
            RectTransform bounds=(RectTransform)tooltipCanvas.transform;
            popup.GetWorldCorners(tooltipCorners);
            Vector3 min=bounds.InverseTransformPoint(tooltipCorners[0]);
            Vector3 max=bounds.InverseTransformPoint(tooltipCorners[2]);
            float x=Mathf.Max(0f,bounds.rect.xMin+8f-min.x)-Mathf.Max(0f,max.x-(bounds.rect.xMax-8f));
            float y=Mathf.Max(0f,bounds.rect.yMin+8f-min.y)-Mathf.Max(0f,max.y-(bounds.rect.yMax-8f));
            popup.position+=bounds.TransformVector(new Vector3(x,y,0f));
        }
        private void LayoutGroup(TraitCategory category,RectTransform title,bool available,ref float top)
        {
            bool visible=false;
            foreach(TraitRowView row in rows) if(available && row.Category==category && row.gameObject.activeSelf) { visible=true; break; }
            title.gameObject.SetActive(visible);
            if(!visible) return;
            title.anchoredPosition=new Vector2(title.anchoredPosition.x,-top);
            top+=26f;
            foreach(TraitRowView row in rows)
            {
                if(row.Category!=category || !row.gameObject.activeSelf) continue;
                RectTransform rect=(RectTransform)row.transform;
                rect.anchoredPosition=new Vector2(rect.anchoredPosition.x,-top);
                top+=rect.sizeDelta.y+4f;
            }
            top+=8f;
        }
        private void OnDisable() { ClearTooltip(); displayed=null; hasShown=false; }
    }
}
