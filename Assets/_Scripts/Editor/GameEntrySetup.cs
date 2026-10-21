#if UNITY_EDITOR
using System.IO;
using System.Linq;
using AutoBattler.Presentation.Battle;
using AutoBattler.Run;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AutoBattler.EditorTools
{
    /// <summary>타이틀 프리팹·스테이지 SO·최종 결과 버튼과 빌드 장면 연결.</summary>
    public static class GameEntrySetup
    {
        [MenuItem("PixelTactics/Setup/Connect Title and Stage Flow")]
        public static void Connect()
        {
            var setup=ProjectSetupSettings.Current;
            var title=setup.TitleScene!=null ? setup.TitleScene : AssetDatabase.LoadAssetAtPath<SceneAsset>(
                Path.Combine(Path.GetDirectoryName(setup.ScenePath),"TitleScene.unity").Replace('\\','/'));
            if(title==null) throw new System.InvalidOperationException("타이틀 장면 에셋 연결 필요.");
            var stage=setup.FirstStage;
            if(stage==null)
            {
                stage=ScriptableObject.CreateInstance<StageDefinition>();
                stage.Configure(1,"1-1","유닛을 모으고 배치하여 마지막 보스에 도전하세요.\n\n상점 구매와 합성, 시너지와 증강 선택으로 부대를 성장시킬 수 있습니다.",1);
                AssetDatabase.CreateAsset(stage,setup.FirstStagePath);
            }
            var settings=setup.Navigation;
            if(settings==null)
            {
                settings=ScriptableObject.CreateInstance<GameNavigationSettings>(); settings.Configure(0,stage);
                AssetDatabase.CreateAsset(settings,setup.NavigationPath);
            }
            setup.ConfigureEntryFlow(title,setup.TitleMenu,settings,stage); EditorUtility.SetDirty(setup);
            var menu=EnsureTitlePrefab();
            setup.ConfigureEntryFlow(title,menu.GetComponent<TitleMenu>(),settings,stage); EditorUtility.SetDirty(setup);
            var result=PrefabUtility.LoadPrefabContents(setup.ResultPath);
            try
            {
                if(ConfigureResultPanel(result.GetComponent<ResultPanel>()))
                    PrefabUtility.SaveAsPrefabAsset(result,setup.ResultPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(result); }
            var hud=PrefabUtility.LoadPrefabContents(setup.HUDPath);
            try
            {
                var view=hud.GetComponent<RunHUD>();
                bool changed=false;
                if(view.Navigation!=settings)
                { view.ConfigureNavigation(settings); changed=true; }
                var resultRect=(RectTransform)view.ResultPanel.transform;
                if(resultRect.sizeDelta!=new Vector2(720f,340f))
                {
                    resultRect.sizeDelta=new Vector2(720f,340f);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(resultRect); changed=true;
                }
                if(changed) PrefabUtility.SaveAsPrefabAsset(hud,setup.HUDPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(hud); }
            var scene=EditorSceneManager.OpenScene(setup.TitleScenePath);
            var menus=Object.FindObjectsByType<TitleMenu>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(view=>view.gameObject.scene==scene).ToArray();
            if(menus.Length==0) PrefabUtility.InstantiatePrefab(menu,scene);
            if(!Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Any(system=>system.gameObject.scene==scene))
            {
                var system=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                system.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            ConfigureBuildScenes(); AssetDatabase.SaveAssets();
        }
        public static void ConfigureBuildScenes()
        {
            var setup=ProjectSetupSettings.Current;
            if(setup.TitleScene==null) return;
            var others=EditorBuildSettings.scenes.Where(scene=>scene.path!=setup.TitleScenePath && scene.path!=setup.ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(setup.TitleScenePath,true),
                new EditorBuildSettingsScene(setup.ScenePath,true)}.Concat(others).ToArray();
        }
        public static bool ConfigureResultPanel(ResultPanel panel)
        {
            if(panel.RetryButton!=null && panel.TitleButton!=null) return false;
            var rect=(RectTransform)panel.transform; rect.sizeDelta=new Vector2(720f,340f);
            panel.TitleText.rectTransform.anchoredPosition=new Vector2(0f,112f);
            panel.ResultText.rectTransform.anchoredPosition=new Vector2(0f,42f);
            panel.EndMessageText.rectTransform.anchoredPosition=new Vector2(0f,-25f);
            panel.EndMessageText.text="다시 도전하거나 타이틀로 돌아갈 수 있습니다.";
            panel.EndMessageText.font=ProjectSetupSettings.Current.UnitInfoFont;
            panel.NextRoundButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(0f,-105f);
            var retry=Button(rect,"RetryButton","재도전",new Vector2(-140f,-105f),new Vector2(240f,52f));
            var title=Button(rect,"TitleButton","타이틀 화면",new Vector2(140f,-105f),new Vector2(240f,52f));
            retry.gameObject.SetActive(false); title.gameObject.SetActive(false);
            panel.ConfigureNavigation(retry,title); return true;
        }
        private static GameObject EnsureTitlePrefab()
        {
            var setup=ProjectSetupSettings.Current;
            if(setup.TitleMenu!=null) return setup.TitleMenu.gameObject;
            var root=new GameObject("TitleMenu",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.layer=LayerMask.NameToLayer("UI");
            try
            {
                root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(1920f,1080f); scaler.matchWidthOrHeight=.5f;
                var background=Rect(root.transform,"Background",Vector2.zero,Vector2.zero);
                background.anchorMin=Vector2.zero; background.anchorMax=Vector2.one; background.sizeDelta=Vector2.zero;
                background.gameObject.AddComponent<Image>().color=new Color(.035f,.065f,.1f);
                var title=Rect(root.transform,"TitlePanel",Vector2.zero,new Vector2(1200f,760f)).gameObject.AddComponent<CanvasGroup>();
                Label(title.transform,"Brand","PixelTactics",new Vector2(0f,190f),new Vector2(1050f,120f),88f,new Color(.92f,.95f,.83f));
                var open=Button(title.transform,"StartButton","시작하기",new Vector2(0f,-55f),new Vector2(320f,76f));
                var quit=Button(title.transform,"QuitButton","종료",new Vector2(0f,-155f),new Vector2(320f,66f));
                var stages=Rect(root.transform,"StageSelectionPanel",Vector2.zero,new Vector2(1100f,720f)).gameObject.AddComponent<CanvasGroup>();
                Label(stages.transform,"Heading","스테이지 선택",new Vector2(0f,245f),new Vector2(1000f,76f),48f,Color.white);
                var choose=Button(stages.transform,"Stage1_1","1-1",new Vector2(-270f,15f),new Vector2(360f,250f));
                choose.GetComponentInChildren<TextMeshProUGUI>().fontSize=40f;
                var name=Label(stages.transform,"StageName","스테이지를 선택하세요",new Vector2(225f,105f),new Vector2(490f,64f),31f,new Color(.4f,.85f,.81f));
                var description=Label(stages.transform,"Description","현재 1-1 스테이지에 도전할 수 있습니다.",new Vector2(225f,-18f),new Vector2(490f,178f),25f,Color.white);
                description.alignment=TextAlignmentOptions.TopLeft;
                var back=Button(stages.transform,"BackButton","뒤로",new Vector2(-150f,-230f),new Vector2(260f,66f));
                var start=Button(stages.transform,"PlayButton","시작",new Vector2(150f,-230f),new Vector2(260f,66f));
                start.interactable=false; stages.gameObject.SetActive(false);
                var status=Label(root.transform,"Status","",new Vector2(0f,-415f),new Vector2(1500f,70f),26f,new Color(1f,.65f,.55f));
                var loading=Rect(root.transform,"LoadingOverlay",Vector2.zero,Vector2.zero);
                loading.anchorMin=Vector2.zero; loading.anchorMax=Vector2.one; loading.sizeDelta=Vector2.zero;
                loading.gameObject.AddComponent<Image>().color=new Color(.025f,.045f,.07f,.98f);
                Label(loading,"Loading","불러오는 중...",Vector2.zero,new Vector2(900f,100f),36f,Color.white);
                loading.gameObject.SetActive(false);
                root.AddComponent<TitleMenu>().Configure(setup.Navigation,title,stages,open,quit,choose,start,back,name,description,status,loading.gameObject);
                return PrefabUtility.SaveAsPrefabAsset(root,setup.TitleMenuPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform)); go.layer=LayerMask.NameToLayer("UI"); go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform; rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=position; rect.sizeDelta=size; return rect;
        }
        private static TextMeshProUGUI Label(Transform parent,string name,string text,Vector2 position,Vector2 size,float fontSize,Color color)
        {
            var label=Rect(parent,name,position,size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font=ProjectSetupSettings.Current.UnitInfoFont; label.fontSize=fontSize; label.text=text; label.color=color;
            label.alignment=TextAlignmentOptions.Center; label.textWrappingMode=TextWrappingModes.Normal;
            label.overflowMode=TextOverflowModes.Ellipsis; label.raycastTarget=false; return label;
        }
        private static Button Button(Transform parent,string name,string text,Vector2 position,Vector2 size)
        {
            var rect=Rect(parent,name,position,size); var image=rect.gameObject.AddComponent<Image>(); image.color=new Color(.12f,.22f,.3f);
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            Label(rect,"Label",text,Vector2.zero,size-new Vector2(24f,8f),26f,Color.white); return button;
        }
    }
}
#endif
