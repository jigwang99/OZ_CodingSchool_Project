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
            const string catalogPath="Assets/_Data/AssetCatalog.asset";
            var catalog=AssetDatabase.LoadAssetAtPath<AssetCatalog>(catalogPath);
            if(catalog==null) { catalog=ScriptableObject.CreateInstance<AssetCatalog>(); AssetDatabase.CreateAsset(catalog,catalogPath); }
            catalog.visuals=Enumerable.Range(1,8).Select(id=>new CatalogEntry {id=id,address="Units/PIXEM_Sample"}).ToArray();
            catalog.snapshotAddress="Data/GameSnapshot"; EditorUtility.SetDirty(catalog);
            var settings=AddressableAssetSettingsDefaultObject.Settings;
            if(settings==null)
            {
                settings=AddressableAssetSettings.Create("Assets/AddressableAssetsData","AddressableAssetSettings",true,true);
                AddressableAssetSettingsDefaultObject.Settings=settings;
            }
            AddAddress(settings,"Data","Assets/_Data/Snapshots/GameSnapshot.json",catalog.snapshotAddress);
            AddAddress(settings,"Units","Assets/_Prefabs/Units/Unit_Character2.prefab","Units/PIXEM_Sample");
            settings.ActivePlayModeDataBuilderIndex=0;
            EditorUtility.SetDirty(settings);
            // 수치 SO 연결 제거. AssetCatalog는 에셋 주소만 보유.
            var prefab=PrefabUtility.LoadPrefabContents("Assets/_Prefabs/Units/Unit_Character2.prefab");
            try
            {
                var serialized=new SerializedObject(prefab.GetComponent<Unit>());
                serialized.FindProperty("unitData").objectReferenceValue=null;
                serialized.ApplyModifiedPropertiesWithoutUndo(); prefab.layer=LayerMask.NameToLayer("AllyUnit");
                PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/_Prefabs/Units/Unit_Character2.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var scene=EditorSceneManager.OpenScene("Assets/_Scenes/BattleScene.unity");
            foreach(var unit in UnityEngine.Object.FindObjectsByType<Unit>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(unit.gameObject);
            foreach(var oldUi in UnityEngine.Object.FindObjectsByType<BattlePhaseUI>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(oldUi.gameObject);
            var grid=UnityEngine.Object.FindFirstObjectByType<HexGridLayout>();
            var bench=UnityEngine.Object.FindFirstObjectByType<BenchLayout>();
            var drag=UnityEngine.Object.FindFirstObjectByType<UnitDragController>();
            var flow=UnityEngine.Object.FindFirstObjectByType<BattleFlowController>();
            var run=drag.GetComponent<RunController>(); if(run==null) run=drag.gameObject.AddComponent<RunController>();
            run.ConfigureScene(catalog,flow,drag,grid,bench);
            var hud=drag.GetComponent<RunHUD>(); if(hud==null) hud=drag.gameObject.AddComponent<RunHUD>(); hud.ConfigureScene(run);
            if(grid.GetComponent<BoardOutlineView>()==null) grid.gameObject.AddComponent<BoardOutlineView>();
            var dragSerialized=new SerializedObject(drag); dragSerialized.FindProperty("playerLevel").intValue=1; dragSerialized.ApplyModifiedPropertiesWithoutUndo();
            var camera=Camera.main; camera.orthographicSize=10f; camera.transform.position=new Vector3(5.25f,4.4f,-10f);
            camera.backgroundColor=new Color(.035f,.055f,.09f);
            EditorUtility.SetDirty(run); EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/_Scenes/BattleScene.unity",true)};
            AssetDatabase.SaveAssets(); Debug.Log("MILESTONE_SETUP_PASS");
        }
        private static void AddAddress(AddressableAssetSettings settings,string groupName,string path,string address)
        {
            var group=settings.FindGroup(groupName);
            if(group==null) group=settings.CreateGroup(groupName,false,false,true,null,typeof(BundledAssetGroupSchema),typeof(ContentUpdateGroupSchema));
            var schema=group.GetSchema<BundledAssetGroupSchema>(); schema.BuildPath.SetVariableByName(settings,AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(settings,AddressableAssetSettings.kLocalLoadPath); schema.IncludeInBuild=true;
            var entry=settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path),group); entry.address=address;
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
