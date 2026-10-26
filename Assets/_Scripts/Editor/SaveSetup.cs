#if UNITY_EDITOR
using AutoBattler.Managers;
using AutoBattler.Presentation.Battle;
using AutoBattler.Run;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoBattler.EditorTools
{
    /// <summary>SaveManager 배치와 이어하기·로드 실패 복귀 버튼 연결.</summary>
    public static class SaveSetup
    {
        [MenuItem("PixelTactics/Setup/Connect Save Manager")]
        public static void Connect()
        {
            var setup=ProjectSetupSettings.Current;
            var previous=SceneManager.GetActiveScene().path;
            var root=PrefabUtility.LoadPrefabContents(setup.GameSettingsPath);
            try
            {
                if(root.GetComponent<SaveManager>()==null)
                { root.AddComponent<SaveManager>(); PrefabUtility.SaveAsPrefabAsset(root,setup.GameSettingsPath); }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            root=PrefabUtility.LoadPrefabContents(setup.TitleMenuPath);
            try
            {
                var menu=root.GetComponent<TitleMenu>();
                if(menu.ContinueButton==null)
                {
                    var button=Object.Instantiate(menu.OpenStagesButton,menu.OpenStagesButton.transform.parent);
                    button.name="ContinueButton"; button.GetComponentInChildren<TextMeshProUGUI>().text="이어하기";
                    var rect=(RectTransform)button.transform; rect.anchoredPosition=new Vector2(0f,45f); rect.sizeDelta=new Vector2(320f,66f);
                    button.gameObject.SetActive(false); menu.ConfigureContinue(button);
                    PrefabUtility.SaveAsPrefabAsset(root,setup.TitleMenuPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            root=PrefabUtility.LoadPrefabContents(setup.HUDPath);
            try
            {
                var hud=root.GetComponent<RunHUD>();
                if(hud.LoadTitleButton==null)
                {
                    var button=Object.Instantiate(hud.RetryLoadButton,hud.RetryLoadButton.transform.parent);
                    button.name="LoadTitleButton"; button.GetComponentInChildren<TextMeshProUGUI>().text="타이틀 화면";
                    var rect=(RectTransform)button.transform; rect.anchoredPosition=new Vector2(500f,-25f); rect.sizeDelta=new Vector2(220f,46f);
                    button.gameObject.SetActive(false); hud.ConfigureLoadTitle(button);
                    PrefabUtility.SaveAsPrefabAsset(root,setup.HUDPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var scene=EditorSceneManager.OpenScene(setup.ScenePath);
            var run=Object.FindFirstObjectByType<RunController>();
            if(run.Stage!=setup.FirstStage)
            {
                run.ConfigureStage(setup.FirstStage); EditorUtility.SetDirty(run);
                PrefabUtility.RecordPrefabInstancePropertyModifications(run);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            if(!string.IsNullOrEmpty(previous) && previous!=SceneManager.GetActiveScene().path) EditorSceneManager.OpenScene(previous);
            Debug.Log("SAVE_SETUP_PASS");
        }
    }
}
#endif
