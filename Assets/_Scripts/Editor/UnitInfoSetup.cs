#if UNITY_EDITOR
using AutoBattler.Presentation.Battle;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;

namespace AutoBattler.EditorTools
{
    /// <summary>오른쪽 기물 정보 프리팹 생성과 HUD 참조 연결.</summary>
    public static class UnitInfoSetup
    {
        [MenuItem("PixelTactics/Setup/Connect Unit Info UI")]
        public static void Connect()
        {
            var setup=ProjectSetupSettings.Current;
            EnsureFont();
            EnsurePrefab();
            var source=PrefabUtility.LoadPrefabContents(setup.UnitInfoPath);
            try
            {
                bool changed=false;
                foreach(var label in source.GetComponentsInChildren<TextMeshProUGUI>(true))
                    if(label.font!=setup.UnitInfoFont) { label.font=setup.UnitInfoFont; changed=true; }
                if(changed) PrefabUtility.SaveAsPrefabAsset(source,setup.UnitInfoPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(source); }
            var root=PrefabUtility.LoadPrefabContents(setup.HUDPath);
            try
            {
                var hud=root.GetComponent<RunHUD>();
                if (hud.UnitInfo==null)
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
            if (hud.UnitInfo!=null) return;
            var root=(GameObject)PrefabUtility.InstantiatePrefab(EnsurePrefab(),hud.transform);
            if (hud.AugmentPanel!=null) root.transform.SetSiblingIndex(hud.AugmentPanel.transform.GetSiblingIndex());
            hud.ConfigureUnitInfo(root.GetComponent<UnitInfoPanel>());
            EditorUtility.SetDirty(hud);
        }
        private static GameObject EnsurePrefab()
        {
            var setup=ProjectSetupSettings.Current;
            if (setup.UnitInfo!=null) return setup.UnitInfo.gameObject;
            EnsureFont();
            var root=Rect(null,"UnitInfoPanel",new Vector2(-24f,-136f),new Vector2(420f,740f));
            root.anchorMin=root.anchorMax=root.pivot=new Vector2(1f,1f);
            try
            {
                root.gameObject.AddComponent<Image>().color=new Color(.035f,.065f,.1f,.98f);
                var group=root.gameObject.AddComponent<CanvasGroup>();
                group.alpha=0f; group.interactable=false; group.blocksRaycasts=true;
                var accent=Rect(root,"TeamAccent",Vector2.zero,new Vector2(420f,4f)).gameObject.AddComponent<Image>();
                accent.raycastTarget=false;
                var title=Label(root,"Name",new Vector2(22f,-22f),new Vector2(376f,38f),29f,"기물 이름");
                title.fontStyle=FontStyles.Bold;
                var identity=Label(root,"Identity",new Vector2(22f,-66f),new Vector2(376f,25f),20f,"아군   *   1골드");
                var traits=Label(root,"Traits",new Vector2(22f,-98f),new Vector2(376f,25f),18f,"종족 / 직업");
                var hp=Label(root,"Health",new Vector2(22f,-143f),new Vector2(376f,25f),19f,"HP");
                var hpFill=Bar(root,"HealthBar",181f,new Color(.25f,.8f,.47f));
                var mp=Label(root,"Mana",new Vector2(22f,-204f),new Vector2(376f,25f),19f,"MP");
                var mpFill=Bar(root,"ManaBar",242f,new Color(.27f,.58f,1f));
                var stats=Label(root,"Stats",new Vector2(22f,-279f),new Vector2(376f,213f),20f,"능력치");
                stats.lineSpacing=3f;
                var skillName=Label(root,"SkillName",new Vector2(22f,-512f),new Vector2(376f,32f),23f,"스킬");
                skillName.fontStyle=FontStyles.Bold; skillName.color=new Color(.55f,.82f,1f);
                var skill=Label(root,"Skill",new Vector2(22f,-556f),new Vector2(376f,162f),18f,"스킬 정보");
                var panel=root.gameObject.AddComponent<UnitInfoPanel>();
                panel.Configure(group,accent,title,identity,traits,hp,mp,hpFill,mpFill,stats,skillName,skill);
                panel.ConfigureHealthSegments(ShieldBarSetup.CreateFill(hpFill));
                root.gameObject.SetActive(false);
                var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,setup.UnitInfoPath);
                setup.SetUnitInfo(prefab.GetComponent<UnitInfoPanel>()); EditorUtility.SetDirty(setup);
                return prefab;
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }
        private static void EnsureFont()
        {
            var setup=ProjectSetupSettings.Current;
            if(setup.UnitInfoFont!=null) return;
            if(setup.UnitInfoSourceFont==null) throw new System.InvalidOperationException("셋업 SO에 유닛 정보용 한글 원본 폰트 연결 필요.");
            var font=TMP_FontAsset.CreateFontAsset(setup.UnitInfoSourceFont,48,5,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
            font.name="UnitInfo Korean SDF";
            AssetDatabase.CreateAsset(font,setup.UnitInfoFontPath);
            font.atlasTextures[0].name="UnitInfo Korean Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTextures[0],font);
            font.material.name="UnitInfo Korean Material"; AssetDatabase.AddObjectToAsset(font.material,font);
            setup.SetUnitInfoFont(font); EditorUtility.SetDirty(setup);
        }
        private static Image Bar(Transform parent,string name,float top,Color color)
        {
            var background=Rect(parent,name,new Vector2(22f,-top),new Vector2(376f,10f));
            var image=background.gameObject.AddComponent<Image>(); image.color=new Color(.1f,.15f,.2f); image.raycastTarget=false;
            var fill=Rect(background,"Fill",Vector2.zero,new Vector2(376f,10f)).gameObject.AddComponent<Image>();
            fill.color=color; fill.raycastTarget=false; return fill;
        }
        private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform)); go.layer=LayerMask.NameToLayer("UI");
            if(parent!=null) go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform; rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0f,1f);
            rect.anchoredPosition=position; rect.sizeDelta=size; return rect;
        }
        private static TextMeshProUGUI Label(Transform parent,string name,Vector2 position,Vector2 size,float fontSize,string text)
        {
            var label=Rect(parent,name,position,size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font=ProjectSetupSettings.Current.UnitInfoFont; label.fontSize=fontSize; label.text=text;
            label.color=Color.white; label.alignment=TextAlignmentOptions.TopLeft;
            label.textWrappingMode=TextWrappingModes.Normal; label.overflowMode=TextOverflowModes.Ellipsis;
            label.raycastTarget=false; return label;
        }
    }
}
#endif
