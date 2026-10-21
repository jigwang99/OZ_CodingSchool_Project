#if UNITY_EDITOR
using System.Linq;
using AutoBattler.Presentation.Battle;
using AutoBattler.Presentation.Units;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.EditorTools
{
    /// <summary>공통 체력바·상세 정보의 보호막 참조와 타이틀 문구 정리.</summary>
    public static class ShieldBarSetup
    {
        [MenuItem("PixelTactics/Setup/Connect Shield HP Bars")]
        public static void Connect()
        {
            var setup=ProjectSetupSettings.Current;
            ReusableUISetup.EnsureHealthBarPrefab();
            var info=PrefabUtility.LoadPrefabContents(setup.UnitInfoPath);
            try
            {
                var panel=info.GetComponent<UnitInfoPanel>();
                if(panel.HealthSegments==null)
                {
                    panel.ConfigureHealthSegments(CreateFill(panel.HealthFill));
                    PrefabUtility.SaveAsPrefabAsset(info,setup.UnitInfoPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(info); }
            var title=PrefabUtility.LoadPrefabContents(setup.TitleMenuPath);
            try
            {
                var menu=title.GetComponent<TitleMenu>(); bool changed=false;
                // 기존 프리팹의 일회성 문구 제거. 런타임 조회에는 사용하지 않음.
                foreach(var label in title.GetComponentsInChildren<TextMeshProUGUI>(true)
                    .Where(label=>label.text=="나만의 부대, 나만의 전술").ToArray())
                { Object.DestroyImmediate(label.gameObject); changed=true; }
                var stageLabel=menu.StageButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if(stageLabel.text!="1-1") { stageLabel.text="1-1"; changed=true; }
                if(changed) PrefabUtility.SaveAsPrefabAsset(title,setup.TitleMenuPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(title); }
            AssetDatabase.SaveAssets();
        }
        public static void ConfigureUnitBar(UnitHealthBar bar)
        { if(bar.Segments==null) bar.ConfigureSegments(CreateFill(bar.FillImage)); }
        public static HealthBarFill CreateFill(Image health)
        {
            var source=health.rectTransform;
            var root=new GameObject("ShieldFill",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            root.layer=health.gameObject.layer; root.transform.SetParent(source.parent,false);
            var rect=(RectTransform)root.transform;
            rect.anchorMin=source.anchorMin; rect.anchorMax=source.anchorMax; rect.pivot=source.pivot;
            rect.sizeDelta=source.sizeDelta; rect.anchoredPosition3D=source.anchoredPosition3D;
            rect.localRotation=source.localRotation; rect.localScale=new Vector3(0f,1f,1f);
            rect.SetSiblingIndex(source.GetSiblingIndex()+1);
            var shield=root.GetComponent<Image>(); shield.color=new Color(.68f,.68f,.68f); shield.raycastTarget=false; shield.enabled=false;
            var fill=source.parent.gameObject.AddComponent<HealthBarFill>(); fill.Configure(health,shield); return fill;
        }
    }
}
#endif
