#if UNITY_EDITOR
using System.Linq;
using AutoBattler.Presentation.Battle;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.EditorTools
{
    /// <summary>판매 버튼을 제거하고 상점·판매 영역을 프리팹에 연결.</summary>
    public static class SellDragSetup
    {
        [MenuItem("PixelTactics/Setup/Connect Drag Selling")]
        public static void Connect()
        {
            var path=ProjectSetupSettings.Current.HUDPath;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            { if(Configure(root.GetComponent<RunHUD>())) PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        public static bool Configure(RunHUD hud)
        {
            if(hud.SellZone!=null && hud.ShopContent!=null) return false;
            var parent=(RectTransform)hud.PreparationPanel.transform;
            // 이전 프리팹의 판매 버튼을 한 번만 제거하는 마이그레이션.
            foreach(var button in parent.GetComponentsInChildren<Button>(true).Where(button=>button.name=="SellButton").ToArray())
                Object.DestroyImmediate(button.gameObject);
            var children=parent.Cast<Transform>().ToArray();
            var content=Stretch(parent,"ShopContent");
            foreach(var child in children) child.SetParent(content,false);
            var rect=Stretch(parent,"SellZone");
            var image=rect.gameObject.AddComponent<Image>(); image.color=new Color(.3f,.11f,.15f);
            var panel=rect.gameObject.AddComponent<CanvasGroup>();
            var title=Label(rect,"Title","판매",32f,31f);
            var price=Label(rect,"Price","유닛  ★  +1골드",0f,24f);
            var hint=Label(rect,"Hint","여기에 유닛을 놓으면 판매",-34f,20f);
            var zone=rect.gameObject.AddComponent<UnitSellZone>();
            zone.Configure(rect,hud.GetComponent<Canvas>(),panel,image,title,price,hint);
            zone.gameObject.SetActive(false); hud.ConfigureSellDrag(content.gameObject,zone);
            EditorUtility.SetDirty(hud); return true;
        }
        private static RectTransform Stretch(Transform parent,string name)
        {
            var root=new GameObject(name,typeof(RectTransform)); root.layer=LayerMask.NameToLayer("UI");
            root.transform.SetParent(parent,false); var rect=(RectTransform)root.transform;
            rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
            rect.sizeDelta=Vector2.zero; rect.anchoredPosition=Vector2.zero; return rect;
        }
        private static TextMeshProUGUI Label(RectTransform parent,string name,string text,float y,float size)
        {
            var root=new GameObject(name,typeof(RectTransform)); root.layer=LayerMask.NameToLayer("UI");
            root.transform.SetParent(parent,false); var rect=(RectTransform)root.transform;
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
            rect.sizeDelta=new Vector2(1700f,36f); rect.anchoredPosition=new Vector2(0f,y);
            var label=root.AddComponent<TextMeshProUGUI>(); label.font=ProjectSetupSettings.Current.UnitInfoFont;
            label.fontSize=size; label.text=text; label.color=Color.white; label.alignment=TextAlignmentOptions.Center;
            label.textWrappingMode=TextWrappingModes.NoWrap; label.raycastTarget=false; return label;
        }
    }
}
#endif
