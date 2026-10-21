#if UNITY_EDITOR
using System;
using System.Linq;
using AutoBattler.Assets;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Units;
using AutoBattler.Presentation.Battle;
using AutoBattler.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Build;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    public static class MilestoneSetup
    {
        [MenuItem("PixelTactics/Setup/Connect Milestone Scene")]
        public static void Configure()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 실행 필요.");
            var setup=ProjectSetupSettings.Current;
            var catalog=setup.Catalog;
            var settings=setup.UnitGroup.Settings;
            CharacterPrefabSetup.Connect();
            settings.ActivePlayModeDataBuilderIndex=0;
            EditorUtility.SetDirty(settings);
            var scene=EditorSceneManager.OpenScene(setup.ScenePath);
            foreach(var unit in UnityEngine.Object.FindObjectsByType<Unit>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(unit.gameObject);
            foreach(var oldUi in UnityEngine.Object.FindObjectsByType<BattlePhaseUI>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(oldUi.gameObject);
            var grid=UnityEngine.Object.FindFirstObjectByType<HexGridLayout>();
            var bench=UnityEngine.Object.FindFirstObjectByType<BenchLayout>();
            var drag=UnityEngine.Object.FindFirstObjectByType<UnitDragController>();
            var flow=UnityEngine.Object.FindFirstObjectByType<BattleFlowController>();
            var run=drag.GetComponent<RunController>(); if(run==null) run=drag.gameObject.AddComponent<RunController>();
            run.ConfigureScene(catalog,flow,drag,grid,bench);
            var hud=BattleHUDSetup.Connect(scene,run);
            if(grid.GetComponent<BoardOutlineView>()==null) grid.gameObject.AddComponent<BoardOutlineView>();
            drag.SetPlayerLevel(1); EditorUtility.SetDirty(drag);
            var camera=Camera.main; camera.orthographicSize=10f; camera.transform.position=new Vector3(5.25f,4.4f,-10f);
            camera.backgroundColor=new Color(.035f,.055f,.09f);
            EditorUtility.SetDirty(run); EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            if(setup.TitleScene!=null) GameEntrySetup.ConfigureBuildScenes();
            else EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(setup.ScenePath,true)};
            AssetDatabase.SaveAssets(); Debug.Log("MILESTONE_SETUP_PASS");
        }
        internal static void AddAddress(AddressableAssetSettings settings,AddressableAssetGroup group,UnityEngine.Object asset)
        {
            var schema=group.GetSchema<BundledAssetGroupSchema>(); schema.BuildPath.SetVariableByName(settings,AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(settings,AddressableAssetSettings.kLocalLoadPath); schema.IncludeInBuild=true;
            string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            var entry=settings.CreateOrMoveEntry(guid,group);
            // 주소는 사람이 확인할 표시용. 로드는 GUID 기반 AssetReference 사용.
            if(entry.address==guid) entry.address=asset.name;
        }
        [MenuItem("PixelTactics/Setup/Build Local Addressables")]
        public static void BuildLocal()
        {
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if(!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
            Debug.Log("ADDRESSABLES_BUILD_PASS "+result.Duration);
        }
    }
}
#endif
