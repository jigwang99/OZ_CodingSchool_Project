using AutoBattler.Battle.Units;
using TMPro;
using UnityEngine;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>PIXEM 샘플 외형에 종류·성급·팀을 표시하는 임시 표현 어댑터.</summary>
    public sealed class UnitIdentityView : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset koreanFont;
        private TextMeshProUGUI label;
#if UNITY_EDITOR
        public void ConfigureFont(TMP_FontAsset font) => koreanFont = font;
#endif
        public void Apply(Unit unit)
        {
            if(label==null)
            {
                var canvasObj=new GameObject("IdentityCanvas",typeof(RectTransform),typeof(Canvas));
                canvasObj.transform.SetParent(transform,false); canvasObj.transform.localPosition=new Vector3(0f,-.8f,0f);
                canvasObj.transform.localScale=Vector3.one*.01f;
                var canvas=canvasObj.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvas.sortingOrder=60;
                var obj=new GameObject("Identity",typeof(RectTransform),typeof(TextMeshProUGUI)); obj.transform.SetParent(canvasObj.transform,false);
                label=obj.GetComponent<TextMeshProUGUI>(); label.fontSize=20f; label.alignment=TextAlignmentOptions.Center;
                if(koreanFont!=null) label.font=koreanFont;
                label.rectTransform.sizeDelta=new Vector2(250f,60f); label.raycastTarget=false;
            }
            label.text=$"{BattleText.UnitName(unit.Definition)}\n{(unit.Star==2?"**":"*")}";
            label.color=unit.Team==UnitTeam.Ally?new Color(.6f,1f,1f):new Color(1f,.65f,.65f);
        }
        private void LateUpdate()
        {
            if(label!=null) label.enabled=GetComponent<UnitHealth>().IsAlive;
        }
    }
}
