#if UNITY_EDITOR
using System;
using System.Linq;
using AutoBattler.Assets;
using AutoBattler.Battle.Units;
using AutoBattler.Presentation.Units;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AutoBattler.EditorTools
{
    /// <summary>설정 SO의 원본 프리팹을 상속하고 전투 모듈과 AssetReference 연결.</summary>
    public static class CharacterPrefabSetup
    {
        [MenuItem("PixelTactics/Setup/Connect Character Prefabs")]
        public static void Connect()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 실행 필요.");
            var setup=ProjectSetupSettings.Current;
            var settings=setup.UnitGroup.Settings;
            var healthBar=ReusableUISetup.EnsureHealthBarPrefab();
            foreach(var entry in setup.Units)
            {
                EnsureUnitPrefab(entry,healthBar);
                MilestoneSetup.AddAddress(settings,setup.UnitGroup,entry.UnitPrefab);
            }
            MilestoneSetup.AddAddress(settings,setup.DataGroup,setup.Snapshot);
            setup.Catalog.ConfigureReferences(new AssetReferenceT<TextAsset>(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(setup.Snapshot))),
                setup.Units.Select(entry=>new CatalogEntry
                {
                    id=entry.VisualId,
                    prefab=new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry.UnitPrefab)))
                }).ToArray());
            EditorUtility.SetDirty(setup.Catalog);
            EditorUtility.SetDirty(setup);
            EditorUtility.SetDirty(setup.UnitGroup);
            EditorUtility.SetDirty(setup.DataGroup);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"CHARACTER_PREFABS_CONNECTED {setup.Units.Count}");
        }

        private static void EnsureUnitPrefab(UnitPrefabSetupEntry entry, GameObject healthBar)
        {
            if(entry.SourcePrefab==null) throw new InvalidOperationException($"원본 프리팹 연결 필요: {entry.VisualId}");
            string path=ProjectSetupSettings.Current.UnitPath(entry);
            bool isVariant=entry.UnitPrefab!=null && PrefabUtility.GetPrefabAssetType(entry.UnitPrefab)==PrefabAssetType.Variant;
            var root=isVariant ? PrefabUtility.LoadPrefabContents(path) : (GameObject)PrefabUtility.InstantiatePrefab(entry.SourcePrefab);
            try
            {
                // 사용자가 편집한 이름, 외형과 레이아웃은 기존 Variant에서 유지.
                if(!isVariant) root.name="Unit_"+entry.SourcePrefab.name;
                AddIfMissing<UnitHealth>(root);
                AddIfMissing<UnitStats>(root);
                AddIfMissing<UnitEffects>(root);
                AddIfMissing<UnitView>(root);
                AddIfMissing<Unit>(root);
                AddIfMissing<UnitMove>(root);
                AddIfMissing<UnitCombat>(root);
                AddIfMissing<UnitFacingView>(root);
                var facing=root.GetComponent<UnitFacingView>();
                if(facing.SpriteRenderers.Count==0)
                    facing.ConfigureRenderers(root.GetComponentsInChildren<SpriteRenderer>(true));
                if(root.GetComponent<Collider2D>()==null) root.AddComponent<BoxCollider2D>();
                root.layer=LayerMask.NameToLayer("AllyUnit");
                root.GetComponent<Unit>().ConfigureEditorDefaults();
                if(root.GetComponentInChildren<UnitHealthBar>(true)==null)
                    PrefabUtility.InstantiatePrefab(healthBar,root.transform);
                entry.SetUnitPrefab(PrefabUtility.SaveAsPrefabAsset(root,path));
            }
            finally
            {
                if(isVariant) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }
        private static void AddIfMissing<T>(GameObject root) where T:Component
        { if(root.GetComponent<T>()==null) root.AddComponent<T>(); }

        public static void BuildForVerification()
        {
            Connect(); Connect();
            CheckPrefabs();
            MilestoneSetup.BuildLocal();
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex=2;
            MilestonePlayChecks.Start();
        }
        public static void CheckPrefabs()
        {
            foreach(var entry in ProjectSetupSettings.Current.Units)
            {
                var prefab=entry.UnitPrefab;
                if(PrefabUtility.GetCorrespondingObjectFromSource(prefab)!=entry.SourcePrefab)
                    throw new InvalidOperationException($"원본 상속 확인 실패: {entry.VisualId}");
                if(prefab.GetComponent<Unit>()==null || prefab.GetComponent<UnitHealth>()==null
                    || prefab.GetComponent<UnitStats>()==null || prefab.GetComponent<UnitEffects>()==null
                    || prefab.GetComponent<UnitCombat>()==null || prefab.GetComponent<UnitMove>()==null
                    || prefab.GetComponent<UnitView>()==null || prefab.GetComponent<Collider2D>()==null
                    || prefab.GetComponent<UnitFacingView>()==null
                    || prefab.GetComponent<UnitFacingView>().SpriteRenderers.Count==0
                    || prefab.GetComponentsInChildren<UnitHealthBar>(true).Length!=1)
                    throw new InvalidOperationException($"모듈 연결 확인 실패: {entry.VisualId}");
                RuntimeAnimatorController controller=prefab.GetComponent<Animator>().runtimeAnimatorController;
                while(controller is AnimatorOverrideController overrides) controller=overrides.runtimeAnimatorController;
                var parameters=((UnityEditor.Animations.AnimatorController)controller).parameters
                    .Where(parameter=>parameter.type==AnimatorControllerParameterType.Trigger).Select(parameter=>parameter.nameHash).ToArray();
                if(new[] {UnitAnimatorParameters.Idle,UnitAnimatorParameters.Run,UnitAnimatorParameters.AttackSlash,
                    UnitAnimatorParameters.AttackShoot,UnitAnimatorParameters.AttackMagic}.Any(hash=>!parameters.Contains(hash)))
                    throw new InvalidOperationException($"Animator 파라미터 연결 필요: {entry.VisualId}");
            }
            Debug.Log("CHARACTER_PREFABS_PASS variants-modules-animator-hashes-healthbars");
        }
    }
}
#endif
