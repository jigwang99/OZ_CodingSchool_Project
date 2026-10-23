#if UNITY_EDITOR
using System;
using System.Linq;
using AutoBattler.Assets;
using AutoBattler.Battle.Units;
using AutoBattler.Presentation.Units;
using AutoBattler.Presentation.Battle;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AutoBattler.EditorTools
{
    /// <summary>설정 SO의 원본 프리팹을 상속하고 전투 모듈과 AssetReference 연결.</summary>
    public static partial class CharacterPrefabSetup
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
                AddIfMissing<UnitMana>(root);
                AddIfMissing<UnitSkill>(root);
                AddIfMissing<UnitView>(root);
                AddIfMissing<Unit>(root);
                AddIfMissing<UnitMove>(root);
                AddIfMissing<UnitCombat>(root);
                AddIfMissing<UnitFacingView>(root);
                AddIfMissing<UnitSkillView>(root);
                AddIfMissing<UnitIdentityView>(root);
                root.GetComponent<UnitIdentityView>().ConfigureFont(ProjectSetupSettings.Current.UnitInfoFont);
                var facing=root.GetComponent<UnitFacingView>();
                if(facing.SpriteRenderers.Count==0)
                    facing.ConfigureRenderers(root.GetComponentsInChildren<SpriteRenderer>(true));
                if(root.GetComponent<Collider2D>()==null) root.AddComponent<BoxCollider2D>();
                root.layer=LayerMask.NameToLayer("AllyUnit");
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

    }
}
#endif
