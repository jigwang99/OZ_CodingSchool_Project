#if UNITY_EDITOR
using AutoBattler.Presentation.Battle;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.EditorTools
{
    /// <summary>공유 HUD Canvas 아래 강화 프리팹 생성·직렬화 참조 연결.</summary>
    public static class AugmentSetup
    {
        [MenuItem("PixelTactics/Setup/Connect Augment UI")]
        public static void Connect()
        {
            EnsurePrefabs(); var setup=ProjectSetupSettings.Current;
            var root=PrefabUtility.LoadPrefabContents(setup.HUDPath);
            try
            {
                var hud=root.GetComponent<RunHUD>();
                if(hud.AugmentPanel==null) { ConfigurePanel(hud); PrefabUtility.SaveAsPrefabAsset(root,setup.HUDPath); }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        public static void ConfigurePanel(RunHUD hud)
        {
            if(hud.AugmentPanel!=null) return;
            EnsurePrefabs();
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(ProjectSetupSettings.Current.AugmentPanel.gameObject,hud.transform);
            hud.ConfigureAugments(instance.GetComponent<AugmentSelectionPanel>()); EditorUtility.SetDirty(hud);
        }
        public static void EnsurePrefabs()
        {
            var setup=ProjectSetupSettings.Current;
            if(setup.AugmentCard==null)
            {
                var root=Rect(null,"AugmentCard",new Vector2(320f,390f),Vector2.zero);
                try
                {
                    root.gameObject.AddComponent<Image>().color=new Color(.83f,.66f,.28f);
                    var button=root.gameObject.AddComponent<Button>(); button.targetGraphic=root.GetComponent<Image>();
                    var inner=Rect(root,"Body",new Vector2(310f,380f),Vector2.zero);
                    inner.gameObject.AddComponent<Image>().color=new Color(.035f,.16f,.22f);
                    var number=Label(inner,"Number",new Vector2(0f,115f),new Vector2(260f,95f),70f,"01"); number.color=new Color(.47f,.91f,1f);
                    var title=Label(inner,"Name",new Vector2(0f,18f),new Vector2(275f,65f),27f,"증강"); title.fontStyle=FontStyles.Bold;
                    var description=Label(inner,"Description",new Vector2(0f,-78f),new Vector2(266f,110f),21f,"설명");
                    var hint=Label(inner,"Hint",new Vector2(0f,-160f),new Vector2(270f,30f),15f,""); hint.color=new Color(.7f,.8f,.84f);
                    root.gameObject.AddComponent<AugmentCard>().Configure(button,title,description,number);
                    setup.SetAugmentCard(PrefabUtility.SaveAsPrefabAsset(root.gameObject,setup.AugmentCardPath).GetComponent<AugmentCard>());
                }
                finally { Object.DestroyImmediate(root.gameObject); }
            }
            if(setup.AugmentPanel==null)
            {
                var root=Rect(null,"AugmentSelectionPanel",Vector2.zero,Vector2.zero); Stretch(root);
                try
                {
                    var overlay=Rect(root,"Overlay",Vector2.zero,Vector2.zero); Stretch(overlay);
                    overlay.gameObject.AddComponent<Image>().color=new Color(.015f,.03f,.055f,.93f);
                    var group=overlay.gameObject.AddComponent<CanvasGroup>();
                    var title=Label(overlay,"Title",new Vector2(0f,270f),new Vector2(900f,55f),38f,"증강 선택"); title.fontStyle=FontStyles.Bold;
                    Label(overlay,"Subtitle",new Vector2(0f,225f),new Vector2(1000f,35f),20f,"");
                    var cards=new AugmentCard[3];
                    for(int i=0;i<3;i++)
                    {
                        var card=(GameObject)PrefabUtility.InstantiatePrefab(setup.AugmentCard.gameObject,overlay);
                        ((RectTransform)card.transform).anchoredPosition=new Vector2((i-1)*350f,0f); cards[i]=card.GetComponent<AugmentCard>();
                    }
                    var toggle=Rect(root,"Toggle",new Vector2(320f,60f),new Vector2(0f,-270f));
                    var image=toggle.gameObject.AddComponent<Image>(); image.color=new Color(.07f,.36f,.43f);
                    var button=toggle.gameObject.AddComponent<Button>(); button.targetGraphic=image;
                    var toggleText=Label(toggle,"Label",Vector2.zero,new Vector2(300f,50f),24f,"증강 닫기");
                    root.gameObject.SetActive(false);
                    root.gameObject.AddComponent<AugmentSelectionPanel>().Configure(overlay.gameObject,group,button,toggleText,cards);
                    setup.SetAugmentPanel(PrefabUtility.SaveAsPrefabAsset(root.gameObject,setup.AugmentPanelPath).GetComponent<AugmentSelectionPanel>());
                }
                finally { Object.DestroyImmediate(root.gameObject); }
            }
            EditorUtility.SetDirty(setup);
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; }
        private static RectTransform Rect(Transform parent,string name,Vector2 size,Vector2 position)
        {
            var go=new GameObject(name,typeof(RectTransform)); go.layer=LayerMask.NameToLayer("UI");
            if(parent!=null) go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform; rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.sizeDelta=size; rect.anchoredPosition=position; return rect;
        }
        private static TextMeshProUGUI Label(RectTransform parent,string name,Vector2 position,Vector2 size,float fontSize,string text)
        {
            var label=Rect(parent,name,size,position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font=ProjectSetupSettings.Current.UnitInfoFont; label.fontSize=fontSize; label.text=text;
            label.alignment=TextAlignmentOptions.Center; label.textWrappingMode=TextWrappingModes.Normal;
            label.overflowMode=TextOverflowModes.Ellipsis; label.color=Color.white; label.raycastTarget=false; return label;
        }
    }
}
#endif
