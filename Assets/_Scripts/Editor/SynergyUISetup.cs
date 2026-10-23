#if UNITY_EDITOR
using System;
using AutoBattler.Data.Traits;
using AutoBattler.Presentation.Battle;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.EditorTools
{
    /// <summary>공통 HUD Canvas 아래에 편집 가능한 시너지 프리팹 연결.</summary>
    public static class SynergyUISetup
    {
        [MenuItem("PixelTactics/Setup/Connect Synergy UI")]
        public static void Connect()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 실행 필요.");
            EnsurePrefab();
            var setup=ProjectSetupSettings.Current;
            var root=PrefabUtility.LoadPrefabContents(setup.HUDPath);
            try
            {
                var hud=root.GetComponent<RunHUD>();
                if(hud.SynergyPanel==null)
                {
                    ConfigurePanel(hud);
                    PrefabUtility.SaveAsPrefabAsset(root,setup.HUDPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        public static void ConfigurePanel(RunHUD hud)
        {
            if(hud.SynergyPanel!=null) return;
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(EnsurePrefab(),hud.transform);
            hud.ConfigureSynergies(instance.GetComponent<SynergyPanel>());
            EditorUtility.SetDirty(hud);
        }
        public static GameObject EnsurePrefab()
        {
            var setup=ProjectSetupSettings.Current;
            if(setup.SynergyPanel!=null) return setup.SynergyPanel.gameObject;
            var root=new GameObject("SynergyPanel",typeof(RectTransform),typeof(Image));
            root.layer=LayerMask.NameToLayer("UI");
            try
            {
                var rect=root.GetComponent<RectTransform>();
                rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0f,1f);
                rect.anchoredPosition=new Vector2(24f,-200f); rect.sizeDelta=new Vector2(352f,688f);
                root.GetComponent<Image>().color=new Color(.045f,.07f,.105f,.97f);
                Label(rect,"Title",new Vector2(20f,-18f),new Vector2(312f,30f),25f,"시너지",true);
                var status=Label(rect,"Status",new Vector2(20f,-52f),new Vector2(312f,22f),15f,string.Empty);
                status.color=new Color(.62f,.73f,.84f);
                status.gameObject.SetActive(false);
                var content=Rect(rect,"Content",Vector2.zero,new Vector2(352f,768f));
                var origins=Label(content,"OriginsTitle",new Vector2(20f,-52f),new Vector2(312f,20f),15f,"종족",true);
                var classes=Label(content,"ClassesTitle",new Vector2(20f,-376f),new Vector2(312f,20f),15f,"직업",true);
                var rows=new TraitRowView[8];
                for(int i=0;i<rows.Length;i++)
                {
                    bool origin=i<4; int group=i%4+1;
                    var card=Rect(content,"Trait"+(i+1),new Vector2(20f,(origin?-78f:-402f)-(group-1)*70f),new Vector2(312f,66f));
                    var background=card.gameObject.AddComponent<Image>(); background.color=new Color(.12f,.12f,.12f);
                    var stripe=Rect(card,"Indicator",Vector2.zero,new Vector2(4f,66f)).gameObject.AddComponent<Image>();
                    stripe.color=new Color(.6f,.6f,.6f);
                    var name=Label(card,"Name",new Vector2(14f,-6f),new Vector2(166f,24f),20f,"--",true);
                    var count=Label(card,"Count",new Vector2(190f,-7f),new Vector2(110f,22f),15.5f,"0/2");
                    count.alignment=TextAlignmentOptions.TopRight;
                    var effect=Label(card,"Effect",new Vector2(14f,-30f),new Vector2(284f,32f),13.5f,"");
                    effect.color=new Color(.6f,.6f,.6f);
                    var row=card.gameObject.AddComponent<TraitRowView>();
                    row.ConfigureReferences(origin?TraitCategory.Origin:TraitCategory.Class,group,background,stripe,name,count,effect);
                    rows[i]=row;
                }
                var footer=Label(content,"Rules",new Vector2(20f,-692f),new Vector2(312f,34f),13.5f,string.Empty);
                footer.color=new Color(.62f,.73f,.84f);
                footer.gameObject.SetActive(false);
                var empty=Label(rect,"Empty",new Vector2(20f,-66f),new Vector2(312f,66f),18f,"시너지 불러오는 중...");
                empty.gameObject.SetActive(false);
                root.AddComponent<SynergyPanel>().ConfigureReferences(content.gameObject,status,empty,rows,
                    origins.rectTransform,classes.rectTransform,footer.rectTransform);
                foreach(var graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget=false;
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,setup.SynergyPanelPath);
                setup.SetSynergyPanel(prefab.GetComponent<SynergyPanel>()); EditorUtility.SetDirty(setup);
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static RectTransform Rect(RectTransform parent,string name,Vector2 position,Vector2 size)
        {
            var root=new GameObject(name,typeof(RectTransform)); root.layer=LayerMask.NameToLayer("UI");
            root.transform.SetParent(parent,false);
            var rect=root.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0f,1f);
            rect.anchoredPosition=position; rect.sizeDelta=size; return rect;
        }
        private static TextMeshProUGUI Label(RectTransform parent,string name,Vector2 position,Vector2 size,float fontSize,string text,bool bold=false)
        {
            var label=Rect(parent,name,position,size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font=TMP_Settings.defaultFontAsset; label.fontSize=fontSize; label.text=text;
            label.fontStyle=bold?FontStyles.Bold:FontStyles.Normal; label.alignment=TextAlignmentOptions.TopLeft;
            label.textWrappingMode=TextWrappingModes.Normal; label.overflowMode=TextOverflowModes.Ellipsis;
            label.color=Color.white; label.raycastTarget=false; return label;
        }
    }
}
#endif
