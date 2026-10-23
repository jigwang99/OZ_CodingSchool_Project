#if UNITY_EDITOR
using System;
using AutoBattler.Managers;
using AutoBattler.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    /// <summary>전투 씬의 기능별 매니저와 RunController 직접 참조 연결.</summary>
    public static class RunManagerSetup
    {
        [MenuItem("PixelTactics/Setup/Connect Run Managers")]
        public static void Connect()
        {
            var scene=EditorSceneManager.OpenScene(ProjectSetupSettings.Current.ScenePath);
            var run=UnityEngine.Object.FindFirstObjectByType<RunController>(); Configure(run);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("RUN_MANAGER_SETUP_PASS");
        }
        public static void Configure(RunController run)
        {
            run.ConfigureManagers(Placed(run,run.Assets),Placed(run,run.Units),Placed(run,run.Battle),
                Placed(run,run.Shop),Placed(run,run.Augments),Placed(run,run.Synergies));
            EditorUtility.SetDirty(run);
        }
        private static T Placed<T>(RunController run,T connected) where T:Component
        {
            if(connected!=null && connected.gameObject.scene==run.gameObject.scene) return connected;
            var placed=run.GetComponentsInChildren<T>(true);
            if(placed.Length!=1)
                throw new InvalidOperationException($"{typeof(T).Name}를 전투 씬의 RunController 아래에 하나 배치하고 Inspector 참조 연결 필요.");
            return placed[0];
        }
    }
}
#endif
