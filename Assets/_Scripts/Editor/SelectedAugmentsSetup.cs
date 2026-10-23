#if UNITY_EDITOR
using AutoBattler.Presentation.Battle;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.EditorTools
{
    /// <summary>시너지 위 증강 요약 바와 클릭을 가리지 않는 설명 프리팹 연결.</summary>
    public static class SelectedAugmentsSetup
    {
        [MenuItem("PixelTactics/Setup/Connect Selected Augments UI")]
        public static void Connect()
        {
            var setup=ProjectSetupSettings.Current;
            var synergyPath=AssetDatabase.GetAssetPath(setup.SynergyPanel);
            var source=PrefabUtility.LoadPrefabContents(synergyPath);
            try
            {
                var rect=(RectTransform)source.transform;
                if(!Mathf.Approximately(rect.anchoredPosition.y,-200f) || !Mathf.Approximately(rect.sizeDelta.y,748f))
                {
                    rect.anchoredPosition=new Vector2(rect.anchoredPosition.x,-200f); rect.sizeDelta=new Vector2(rect.sizeDelta.x,748f);
                    PrefabUtility.SaveAsPrefabAsset(source,synergyPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(source); }
            var root=PrefabUtility.LoadPrefabContents(setup.HUDPath);
            try
            {
                var hud=root.GetComponent<RunHUD>(); bool changed=false;
                if(hud.SelectedAugments==null) { ConfigurePanel(hud); changed=true; }
                var rect=(RectTransform)hud.SynergyPanel.transform;
                if(!Mathf.Approximately(rect.anchoredPosition.y,-200f) || !Mathf.Approximately(rect.sizeDelta.y,748f))
                {
                    rect.anchoredPosition=new Vector2(rect.anchoredPosition.x,-200f); rect.sizeDelta=new Vector2(rect.sizeDelta.x,748f);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rect); changed=true;
                }
                if(changed) PrefabUtility.SaveAsPrefabAsset(root,setup.HUDPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        public static void ConfigurePanel(RunHUD hud)
        {
            if(hud.SelectedAugments!=null) return;
            var root=(GameObject)PrefabUtility.InstantiatePrefab(EnsurePrefab(),hud.transform);
            // 강화 선택 모달보다 먼저 그려 모달 위에 설명이 남지 않도록 유지.
            if(hud.AugmentPanel!=null) root.transform.SetSiblingIndex(hud.AugmentPanel.transform.GetSiblingIndex());
            hud.ConfigureSelectedAugments(root.GetComponent<SelectedAugmentsView>()); EditorUtility.SetDirty(hud);
        }
        private static GameObject EnsurePrefab()
        {
            var setup=ProjectSetupSettings.Current;
            if(setup.SelectedAugments!=null) return setup.SelectedAugments.gameObject;
            var root=Rect(null,"SelectedAugments",new Vector2(24f,-136f),new Vector2(352f,56f));
            try
            {
                var area=root.gameObject.AddComponent<Image>(); area.color=new Color(.045f,.07f,.105f,.97f);
                var count=Label(root,"Count",new Vector2(12f,-17f),new Vector2(154f,25f),18f,"증강 0/3"); count.fontStyle=FontStyles.Bold;
                var slots=new Image[3]; var labels=new TextMeshProUGUI[3];
                for(int i=0;i<3;i++)
                {
                    var rect=Rect(root,"Slot"+(i+1),new Vector2(190f+i*50f,-7f),new Vector2(42f,42f));
                    slots[i]=rect.gameObject.AddComponent<Image>(); slots[i].color=new Color(.1f,.13f,.18f); slots[i].raycastTarget=false;
                    labels[i]=Label(rect,"Number",Vector2.zero,new Vector2(42f,42f),23f,"-"); labels[i].alignment=TextAlignmentOptions.Center;
                }
                var popup=Rect(root,"Tooltip",new Vector2(364f,0f),new Vector2(420f,160f));
                popup.gameObject.AddComponent<Image>().color=new Color(.045f,.11f,.16f,.99f);
                var text=Label(popup,"Description",new Vector2(16f,-16f),new Vector2(388f,128f),19f,"선택한 증강이 없습니다.");
                var group=popup.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts=false; group.interactable=false; group.alpha=0f;
                foreach(var graphic in popup.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget=false;
                popup.gameObject.SetActive(false);
                root.gameObject.AddComponent<SelectedAugmentsView>().Configure(area,slots,labels,count,group,text);
                var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,setup.SelectedAugmentsPath);
                setup.SetSelectedAugments(prefab.GetComponent<SelectedAugmentsView>()); EditorUtility.SetDirty(setup);
                return prefab;
            }
            finally { Object.DestroyImmediate(root.gameObject); }
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
            label.font=ProjectSetupSettings.Current.UnitInfoFont; label.fontSize=fontSize; label.text=text; label.color=Color.white;
            label.alignment=TextAlignmentOptions.TopLeft; label.textWrappingMode=TextWrappingModes.Normal;
            label.overflowMode=TextOverflowModes.Ellipsis; label.raycastTarget=false; return label;
        }
    }
}
#endif
