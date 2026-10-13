#if UNITY_EDITOR
using System;
using AutoBattler.Presentation.Battle;
using AutoBattler.Presentation.Units;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.EditorTools
{
    /// <summary>설정 SO의 공통 결과 패널과 체력바 프리팹 연결.</summary>
    public static class ReusableUISetup
    {
        public static string ResultPath => ProjectSetupSettings.Current.ResultPath;
        public static string HealthBarPath => ProjectSetupSettings.Current.HealthBarPath;

        [MenuItem("PixelTactics/Setup/Create ResultPanel Prefab")]
        public static void CreateResultPrefab() => Select(EnsureResultPrefab());
        [MenuItem("PixelTactics/Setup/Create UnitHealthBar Prefab")]
        public static void CreateHealthBarPrefab() => Select(EnsureHealthBarPrefab());

        [MenuItem("PixelTactics/Setup/Connect ResultPanel and UnitHealthBar")]
        public static void ConnectPrefabs()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 실행 필요.");
            EnsureResultPrefab(); EnsureHealthBarPrefab();
            var setup=ProjectSetupSettings.Current;
            var hudRoot=PrefabUtility.LoadPrefabContents(setup.HUDPath);
            try
            {
                ConfigureResultPanel(hudRoot.GetComponent<RectTransform>(),hudRoot.GetComponent<RunHUD>());
                PrefabUtility.SaveAsPrefabAsset(hudRoot,setup.HUDPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(hudRoot); }
            foreach(var entry in setup.Units)
            {
                string path=setup.UnitPath(entry);
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var old=root.GetComponentInChildren<UnitHealthBar>(true);
                    if(old==null) PrefabUtility.InstantiatePrefab(EnsureHealthBarPrefab(),root.transform);
                    else if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old)!=HealthBarPath)
                        Replace(old.transform.parent,old.GetComponent<RectTransform>(),EnsureHealthBarPrefab(),"UnitHealthBar");
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureResultPanel(RectTransform parent, RunHUD hud)
        {
            var panel=hud.ResultPanel;
            if(panel==null)
                panel=Replace(parent,null,EnsureResultPrefab(),"ResultPanel").GetComponent<ResultPanel>();
            panel.gameObject.SetActive(false);
            hud.ConfigureResultPanel(panel);
            EditorUtility.SetDirty(hud);
        }

        public static GameObject EnsureResultPrefab()
        {
            var setup=ProjectSetupSettings.Current;
            if(setup.ResultPanel!=null) return setup.ResultPanel.gameObject;
            string path=setup.ResultPath;
            var prototype=new GameObject("ResultPanel",typeof(RectTransform),typeof(Image),typeof(CanvasGroup));
            try
            {
                var rect=prototype.GetComponent<RectTransform>();
                rect.sizeDelta=new Vector2(720f,250f); rect.anchoredPosition=new Vector2(0f,50f);
                prototype.layer=LayerMask.NameToLayer("UI");
                prototype.GetComponent<Image>().color=new Color(.075f,.11f,.17f,.96f);
                var title=Label(rect,"TitleText",new Vector2(0f,80f),new Vector2(660f,44f),30f,"Battle result");
                var result=Label(rect,"ResultText",new Vector2(0f,15f),new Vector2(660f,75f),24f,"Victory / Defeat");
                var next=Rect(rect,"NextRoundButton",new Vector2(0f,-80f),new Vector2(240f,52f));
                var image=next.gameObject.AddComponent<Image>(); image.color=new Color(.17f,.28f,.4f);
                var button=next.gameObject.AddComponent<Button>(); button.targetGraphic=image;
                Label(next,"Label",Vector2.zero,new Vector2(224f,44f),22f,"Next round");
                var end=Label(rect,"EndMessageText",new Vector2(0f,-80f),new Vector2(660f,44f),21f,"Restart Play to try again.");
                end.gameObject.SetActive(false);
                var panel=prototype.AddComponent<ResultPanel>();
                panel.ConfigureReferences(prototype.GetComponent<CanvasGroup>(),title,result,end,button);
                prototype.SetActive(false);
                var prefab=PrefabUtility.SaveAsPrefabAsset(prototype,path);
                setup.SetResultPanel(prefab.GetComponent<ResultPanel>()); EditorUtility.SetDirty(setup);
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(prototype); }
        }

        public static GameObject EnsureHealthBarPrefab()
        {
            var setup=ProjectSetupSettings.Current;
            if(setup.HealthBar!=null) return setup.HealthBar.gameObject;
            string path=setup.HealthBarPath;
            var prototype=new GameObject("UnitHealthBar",typeof(RectTransform),typeof(Canvas));
            try
            {
                prototype.layer=LayerMask.NameToLayer("UI");
                var rect=prototype.GetComponent<RectTransform>();
                rect.sizeDelta=new Vector2(140f,40f); rect.anchoredPosition=new Vector2(0f,1.1f);
                rect.localScale=Vector3.one*.01f;
                var canvas=prototype.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace;
                var background=Rect(rect,"Background",Vector2.zero,new Vector2(104f,12f)).gameObject.AddComponent<Image>();
                background.color=new Color(.05f,.05f,.05f,.9f);
                var fill=Rect(rect,"Fill",new Vector2(-50f,0f),new Vector2(100f,8f)).gameObject.AddComponent<Image>();
                fill.rectTransform.pivot=new Vector2(0f,.5f);
                var label=Label(rect,"HealthText",new Vector2(0f,18f),new Vector2(140f,20f),18f,"--");
                prototype.AddComponent<UnitHealthBar>().ConfigureReferences(canvas,fill,label);
                foreach(var graphic in prototype.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget=false;
                var prefab=PrefabUtility.SaveAsPrefabAsset(prototype,path);
                setup.SetHealthBar(prefab.GetComponent<UnitHealthBar>()); EditorUtility.SetDirty(setup);
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(prototype); }
        }

        private static GameObject Replace(Transform parent, RectTransform old, GameObject prefab, string name)
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            var rect=instance.GetComponent<RectTransform>(); instance.name=name;
            if(old!=null)
            {
                // 기존 위치, 크기와 표시 깊이를 유지.
                rect.anchorMin=old.anchorMin; rect.anchorMax=old.anchorMax; rect.pivot=old.pivot;
                rect.sizeDelta=old.sizeDelta; rect.anchoredPosition3D=old.anchoredPosition3D;
                rect.localRotation=old.localRotation; rect.localScale=old.localScale;
                rect.SetSiblingIndex(old.GetSiblingIndex()); UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            return instance;
        }
        private static void Select(GameObject prefab)
        { Selection.activeObject=prefab; EditorGUIUtility.PingObject(prefab); }
        private static RectTransform Rect(RectTransform parent,string name,Vector2 position,Vector2 size)
        {
            var obj=new GameObject(name,typeof(RectTransform)); obj.layer=LayerMask.NameToLayer("UI");
            obj.transform.SetParent(parent,false);
            var rect=obj.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
            rect.anchoredPosition=position; rect.sizeDelta=size; return rect;
        }
        private static TextMeshProUGUI Label(RectTransform parent,string name,Vector2 position,Vector2 size,float fontSize,string text)
        {
            var label=Rect(parent,name,position,size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font=TMP_Settings.defaultFontAsset; label.fontSize=fontSize; label.alignment=TextAlignmentOptions.Center;
            label.textWrappingMode=TextWrappingModes.Normal; label.raycastTarget=false; label.color=Color.white; label.text=text; return label;
        }
        public static void BuildForVerification()
        {
            ConnectPrefabs();
            var setup=ProjectSetupSettings.Current;
            string beforeHud=System.IO.File.ReadAllText(setup.HUDPath);
            ConnectPrefabs();
            if(beforeHud!=System.IO.File.ReadAllText(setup.HUDPath))
                throw new InvalidOperationException("재연결 시 HUD 변경 발생.");
            foreach(var entry in setup.Units)
            {
                var bars=entry.UnitPrefab.GetComponentsInChildren<UnitHealthBar>(true);
                if(bars.Length!=1 || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(bars[0])!=HealthBarPath)
                    throw new InvalidOperationException("공통 체력바 중첩 프리팹 연결 실패.");
            }
            BattleHUDSetup.BuildForVerification();
            Debug.Log("REUSABLE_UI_PREFABS_PASS");
            MilestonePlayChecks.Start();
        }
    }
}
#endif
