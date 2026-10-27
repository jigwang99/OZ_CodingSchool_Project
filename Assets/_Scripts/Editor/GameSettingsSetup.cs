#if UNITY_EDITOR
using System.Linq;
using AutoBattler.Managers;
using AutoBattler.Presentation.Battle;
using AutoBattler.Run;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AutoBattler.EditorTools
{
    /// <summary>영속 설정 프리팹과 장면별 배경 입력 참조 연결.</summary>
    public static class GameSettingsSetup
    {
        [MenuItem("PixelTactics/Setup/Connect Game Settings")]
        public static void Connect()
        {
            var setup=ProjectSetupSettings.Current;
            var previous=SceneManager.GetActiveScene().path;
            var prefab=EnsurePrefab();
            ConfigureUI(setup.HUDPath,true);
            ConfigureUI(setup.TitleMenuPath,false);
            foreach(var path in new[]{setup.TitleScenePath,setup.ScenePath})
            {
                var scene=EditorSceneManager.OpenScene(path);
                // 전투 씬을 직접 실행할 때도 같은 프리팹 사용. 타이틀에서 왔다면 중복 인스턴스만 제거.
                if(!scene.GetRootGameObjects().Any(root=>root.GetComponent<GameManager>()!=null))
                    PrefabUtility.InstantiatePrefab(prefab,scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            ReusableUISetup.EnsureHealthBarPrefab();
            AssetDatabase.SaveAssets();
            if(!string.IsNullOrEmpty(previous) && SceneManager.GetActiveScene().path!=previous)
                EditorSceneManager.OpenScene(previous);
            Debug.Log("GAME_SETTINGS_SETUP_PASS");
        }

        private static void ConfigureUI(string path,bool battle)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var group=root.GetComponent<CanvasGroup>();
                if(group==null) group=root.AddComponent<CanvasGroup>();
                if(battle) root.GetComponent<RunHUD>().ConfigureSettingsRoot(group);
                else root.GetComponent<TitleMenu>().ConfigureSettingsRoot(group);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static GameObject EnsurePrefab()
        {
            var setup=ProjectSetupSettings.Current;
            if(setup.GameSettings!=null)
            {
                if(setup.GameSettings.FrameRateDropdown!=null)
                { SoundSetup.EnsureSettings(); return setup.GameSettings.gameObject; }
                var contents=PrefabUtility.LoadPrefabContents(setup.GameSettingsPath);
                try
                {
                    var view=contents.GetComponent<GameSettingsView>();
                    var panel=view.QuitButton.transform.parent;
                    // 이전 버튼형 프리팹을 한 번만 변환하는 에디터 마이그레이션.
                    foreach(var button in panel.GetComponentsInChildren<Button>(true)
                        .Where(button=>button.name=="Fps30Button" || button.name=="Fps60Button" || button.name=="Fps144Button").ToArray())
                        Object.DestroyImmediate(button.gameObject);
                    foreach(var label in panel.GetComponentsInChildren<TextMeshProUGUI>(true)
                        .Where(label=>label.transform.parent==panel && label.name=="CurrentFrameRate").ToArray())
                        Object.DestroyImmediate(label.gameObject);
                    view.Configure(view.Modal,view.OpenButton,view.CloseButton,view.QuitButton,CreateFrameRateDropdown(panel));
                    PrefabUtility.SaveAsPrefabAsset(contents,setup.GameSettingsPath);
                    SoundSetup.EnsureSettings();
                    return AssetDatabase.LoadAssetAtPath<GameObject>(setup.GameSettingsPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            var root=new GameObject("GameSettings",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.layer=LayerMask.NameToLayer("UI");
            try
            {
                var canvas=root.GetComponent<Canvas>();
                canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=300;
                var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(1920f,1080f); scaler.matchWidthOrHeight=.5f;
                var open=Button(root.transform,"SettingsButton",GameSettingsView.Heading,Vector2.zero,new Vector2(120f,44f));
                var openRect=(RectTransform)open.transform;
                openRect.anchorMin=openRect.anchorMax=openRect.pivot=new Vector2(0f,1f);
                openRect.anchoredPosition=new Vector2(24f,-62f);
                var overlay=Rect(root.transform,"SettingsOverlay",Vector2.zero,Vector2.zero);
                overlay.anchorMin=Vector2.zero; overlay.anchorMax=Vector2.one;
                overlay.gameObject.AddComponent<Image>().color=new Color(.015f,.025f,.045f,.82f);
                var modal=overlay.gameObject.AddComponent<CanvasGroup>(); modal.ignoreParentGroups=true;
                var panel=Rect(overlay,"SettingsPanel",Vector2.zero,new Vector2(620f,400f));
                panel.gameObject.AddComponent<Image>().color=new Color(.065f,.11f,.17f,1f);
                Label(panel,"Heading",GameSettingsView.Heading,new Vector2(0f,143f),new Vector2(380f,52f),36f,Color.white);
                var close=Button(panel,"CloseButton","X",Vector2.zero,new Vector2(44f,44f));
                var closeRect=(RectTransform)close.transform;
                closeRect.anchorMin=closeRect.anchorMax=closeRect.pivot=Vector2.one;
                closeRect.anchoredPosition=new Vector2(-16f,-16f);
                Label(panel,"FrameRateHeading",GameSettingsView.FrameRateHeading,new Vector2(0f,72f),new Vector2(420f,40f),25f,Color.white);
                var dropdown=CreateFrameRateDropdown(panel);
                var quit=Button(panel,"QuitButton",GameSettingsView.QuitLabel,new Vector2(0f,-132f),new Vector2(456f,56f));
                quit.targetGraphic.color=new Color(.48f,.16f,.2f);
                overlay.gameObject.SetActive(false);
                var view=root.AddComponent<GameSettingsView>();
                view.Configure(modal,open,close,quit,dropdown);
                root.AddComponent<GameManager>().ConfigureSettings(view);
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,setup.GameSettingsPath);
                setup.SetGameSettings(prefab.GetComponent<GameSettingsView>()); EditorUtility.SetDirty(setup);
                SoundSetup.EnsureSettings();
                return AssetDatabase.LoadAssetAtPath<GameObject>(setup.GameSettingsPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static TMP_Dropdown CreateFrameRateDropdown(Transform parent)
        {
            var root=TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            root.name="FrameRateDropdown"; root.transform.SetParent(parent,false);
            var rect=root.GetComponent<RectTransform>();
            rect.anchoredPosition=new Vector2(0f,8f); rect.sizeDelta=new Vector2(456f,56f);
            var dropdown=root.GetComponent<TMP_Dropdown>();
            dropdown.ClearOptions();
            dropdown.AddOptions(new System.Collections.Generic.List<string>{"30 FPS","60 FPS","144 FPS"});
            dropdown.SetValueWithoutNotify(1);
            // 패널을 닫을 때 목록과 입력 차단 오브젝트도 즉시 정리.
            dropdown.alphaFadeSpeed=0f;
            dropdown.targetGraphic.color=new Color(.12f,.22f,.3f);
            dropdown.template.sizeDelta=new Vector2(0f,152f);
            dropdown.template.GetComponent<Image>().color=new Color(.07f,.13f,.19f);
            var toggle=dropdown.itemText.GetComponentInParent<Toggle>(true);
            var item=(RectTransform)toggle.transform;
            item.sizeDelta=new Vector2(0f,48f);
            ((RectTransform)item.parent).sizeDelta=new Vector2(0f,56f);
            toggle.targetGraphic.color=new Color(.11f,.2f,.28f);
            var toggleColors=toggle.colors;
            toggleColors.highlightedColor=new Color(.65f,1f,.88f);
            toggleColors.selectedColor=new Color(.65f,1f,.88f); toggle.colors=toggleColors;
            toggle.graphic.color=new Color(.2f,.85f,.65f);
            var mark=(RectTransform)toggle.graphic.transform;
            mark.sizeDelta=new Vector2(8f,8f); mark.anchoredPosition=new Vector2(13f,0f);
            dropdown.itemText.rectTransform.offsetMin=new Vector2(28f,4f);
            dropdown.itemText.rectTransform.offsetMax=new Vector2(-16f,-4f);
            dropdown.captionText.rectTransform.offsetMin=new Vector2(20f,4f);
            dropdown.captionText.rectTransform.offsetMax=new Vector2(-48f,-4f);
            foreach(var label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                label.font=ProjectSetupSettings.Current.UnitInfoFont; label.fontSize=24f;
                label.color=Color.white; label.textWrappingMode=TextWrappingModes.NoWrap; label.raycastTarget=false;
            }
            var arrow=root.GetComponentsInChildren<Image>(true)
                .Single(image=>image.transform.parent==root.transform && image.transform!=dropdown.template);
            arrow.enabled=false;
            ChevronLine(arrow.transform,"Left",new Vector2(-3f,0f),-45f);
            ChevronLine(arrow.transform,"Right",new Vector2(3f,0f),45f);
            foreach(var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=LayerMask.NameToLayer("UI");
            return dropdown;
        }

        private static void ChevronLine(Transform parent,string name,Vector2 position,float angle)
        {
            var rect=Rect(parent,name,position,new Vector2(10f,2f));
            rect.localRotation=Quaternion.Euler(0f,0f,angle);
            var image=rect.gameObject.AddComponent<Image>(); image.color=Color.white; image.raycastTarget=false;
        }

        private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform)); go.layer=LayerMask.NameToLayer("UI"); go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=position; rect.sizeDelta=size; return rect;
        }
        private static TextMeshProUGUI Label(Transform parent,string name,string text,Vector2 position,Vector2 size,float fontSize,Color color)
        {
            var label=Rect(parent,name,position,size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font=ProjectSetupSettings.Current.UnitInfoFont; label.fontSize=fontSize; label.text=text;
            label.color=color; label.alignment=TextAlignmentOptions.Center;
            label.textWrappingMode=TextWrappingModes.NoWrap; label.raycastTarget=false; return label;
        }
        private static Button Button(Transform parent,string name,string text,Vector2 position,Vector2 size)
        {
            var rect=Rect(parent,name,position,size);
            var image=rect.gameObject.AddComponent<Image>(); image.color=new Color(.12f,.22f,.3f);
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            var colors=button.colors; colors.disabledColor=Color.white; button.colors=colors;
            Label(rect,"Label",text,Vector2.zero,size-new Vector2(16f,8f),24f,Color.white);
            return button;
        }
    }
}
#endif
