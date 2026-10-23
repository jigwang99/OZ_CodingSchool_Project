using System.Collections.Generic;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Traits;
using AutoBattler.Data.Traits;
using TMPro;
using UnityEngine;

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
        private TraitSnapshot displayed;
        private BattlePhase displayedPhase;
        private bool hasShown,wasReady;
        public IReadOnlyList<TraitRowView> Rows=>System.Array.AsReadOnly(rows);
        public TextMeshProUGUI StatusText=>statusText;
        public TextMeshProUGUI EmptyText=>emptyText;
        public TraitSnapshot DisplayedSnapshot=>displayed;
        public bool IsDataAvailable=>content.activeSelf;
#if UNITY_EDITOR
        public void ConfigureReferences(GameObject body,TextMeshProUGUI status,TextMeshProUGUI empty,TraitRowView[] views,
            RectTransform origins,RectTransform classes,RectTransform footer)
        { content=body; statusText=status; emptyText=empty; rows=views; originsTitle=origins; classesTitle=classes; rules=footer; }
#endif
        public void Show(TraitSnapshot snapshot,bool ready,BattlePhase phase)
        {
            snapshot=snapshot ?? TraitSnapshot.Empty;
            if(hasShown && ReferenceEquals(displayed,snapshot) && wasReady==ready && displayedPhase==phase) return;
            displayed=snapshot; displayedPhase=phase; wasReady=ready; hasShown=true;
            bool available=ready && snapshot.Statuses.Count>0;
            content.SetActive(available);
            emptyText.text=ready?"시너지 정보가 없음.":"시너지 불러오는 중...";
            statusText.text=string.Empty;
            statusText.gameObject.SetActive(false);
            rules.gameObject.SetActive(false);
            // 숨긴 행의 자리를 남기지 않고 그룹과 행을 위에서부터 배치.
            bool anyVisible=false;
            if(available) foreach(var row in rows)
            {
                row.Show(snapshot);
                anyVisible|=row.gameObject.activeSelf;
            }
            emptyText.gameObject.SetActive(!anyVisible);
            if(available && !anyVisible) emptyText.text="기물을 배치하면 시너지 표시.";
            float top=52f;
            LayoutGroup(TraitCategory.Origin,originsTitle,available,ref top);
            LayoutGroup(TraitCategory.Class,classesTitle,available,ref top);
            ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,anyVisible?top+8f:148f);
        }
        private void LayoutGroup(TraitCategory category,RectTransform title,bool available,ref float top)
        {
            bool visible=false;
            foreach(var row in rows) if(available && row.Category==category && row.gameObject.activeSelf) { visible=true; break; }
            title.gameObject.SetActive(visible);
            if(!visible) return;
            title.anchoredPosition=new Vector2(title.anchoredPosition.x,-top);
            top+=26f;
            foreach(var row in rows)
            {
                if(row.Category!=category || !row.gameObject.activeSelf) continue;
                var rect=(RectTransform)row.transform;
                rect.anchoredPosition=new Vector2(rect.anchoredPosition.x,-top);
                top+=rect.sizeDelta.y+4f;
            }
            top+=8f;
        }
        private void OnDisable() { displayed=null; hasShown=false; }
    }
}
