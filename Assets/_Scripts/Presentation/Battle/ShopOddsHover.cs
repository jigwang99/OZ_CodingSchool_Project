using System.Collections.Generic;
using System.Text;
using AutoBattler.Run;
using AutoBattler.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AutoBattler.Presentation.Battle
{
    public sealed class ShopOddsHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private TextMeshProUGUI label;
        private RunController run;
        private CanvasGroup inputRoot;
        private Vector2 pointer;
        private Camera eventCamera;
        private int shownLevel;

        public void Bind(RunController controller,CanvasGroup group)
        { Hide(); run=controller; inputRoot=group; }

        private bool CanShow=>run!=null && run.IsReady && !run.IsShuttingDown
            && inputRoot!=null && inputRoot.interactable && inputRoot.blocksRaycasts
            && !GameNavigation.IsTransitioning && !run.Flow.IsAugmentWindowOpen;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if(!CanShow || eventData.dragging) return;
            pointer=eventData.position; eventCamera=eventData.enterEventCamera;
            panel.SetAsLastSibling(); panel.gameObject.SetActive(true);
            Refresh();
        }
        public void OnPointerExit(PointerEventData eventData)=>Hide();
        private void LateUpdate()
        {
            if(!panel.gameObject.activeSelf) return;
            if(!CanShow) Hide();
            else if(shownLevel!=run.State.Level) Refresh();
        }
        private void Refresh()
        {
            shownLevel=run.State.Level;
            IReadOnlyList<int> weights=run.State.Data.ShopWeights(shownLevel);
            int total=0;
            foreach(int weight in weights) total+=weight;
            StringBuilder text=new StringBuilder();
            text.Append("레벨 ").Append(shownLevel).Append(" 상점 확률");
            for(int i=0;i<weights.Count;i++)
                text.Append('\n').Append(i+1).Append("G<pos=124>")
                    .Append((total>0?100f*weights[i]/total:0f).ToString("0.#")).Append('%');
            label.text=text.ToString();
            TooltipPositionUtility.Place(panel,pointer,eventCamera,new Vector2(16f,-22f),12f);
        }
        private void Hide() { if(panel!=null) panel.gameObject.SetActive(false); shownLevel=0; }
        private void OnDisable()=>Hide();
        private void OnApplicationFocus(bool focused) { if(!focused) Hide(); }
    }
}
