#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AutoBattler.Assets;
using AutoBattler.Presentation.Battle;
using AutoBattler.Presentation.Units;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    [Serializable]
    public sealed class UnitPrefabSetupEntry
    {
        [SerializeField] private int visualId;
        [SerializeField] private GameObject sourcePrefab;
        [SerializeField] private GameObject unitPrefab;
        public int VisualId => visualId;
        public GameObject SourcePrefab => sourcePrefab;
        public GameObject UnitPrefab => unitPrefab;
        public UnitPrefabSetupEntry(int id, GameObject source, GameObject unit)
        { visualId=id; sourcePrefab=source; unitPrefab=unit; }
        public void SetUnitPrefab(GameObject prefab) => unitPrefab=prefab;
    }

    /// <summary>에디터 셋업과 검증에 필요한 에셋을 경로 대신 직접 연결. 플레이어에 포함하지 않음.</summary>
    [CreateAssetMenu(menuName="PixelTactics/Editor/Project Setup Settings")]
    public sealed class ProjectSetupSettings : ScriptableObject
    {
        [SerializeField] private AssetCatalog catalog;
        [SerializeField] private TextAsset snapshot;
        [SerializeField] private SceneAsset battleScene;
        [SerializeField] private RunHUD battleHUD;
        [SerializeField] private ShopItem shopItem;
        [SerializeField] private ResultPanel resultPanel;
        [SerializeField] private UnitHealthBar healthBar;
        [SerializeField] private SynergyPanel synergyPanel;
        [SerializeField] private DefaultAsset uiPrefabFolder;
        [SerializeField] private DefaultAsset unitPrefabFolder;
        [SerializeField] private AddressableAssetGroup dataGroup;
        [SerializeField] private AddressableAssetGroup unitGroup;
        [SerializeField] private UnitPrefabSetupEntry[] units=Array.Empty<UnitPrefabSetupEntry>();

        public AssetCatalog Catalog => catalog;
        public TextAsset Snapshot => snapshot;
        public SceneAsset BattleScene => battleScene;
        public RunHUD BattleHUD => battleHUD;
        public ShopItem ShopItem => shopItem;
        public ResultPanel ResultPanel => resultPanel;
        public UnitHealthBar HealthBar => healthBar;
        public SynergyPanel SynergyPanel => synergyPanel;
        public AddressableAssetGroup DataGroup => dataGroup;
        public AddressableAssetGroup UnitGroup => unitGroup;
        public IReadOnlyList<UnitPrefabSetupEntry> Units => Array.AsReadOnly(units);
        public string ScenePath => AssetDatabase.GetAssetPath(battleScene);
        public string HUDPath => ExistingOrNew(battleHUD, uiPrefabFolder, "BattleHUD.prefab");
        public string ShopItemPath => ExistingOrNew(shopItem, uiPrefabFolder, "ShopItem.prefab");
        public string ResultPath => ExistingOrNew(resultPanel, uiPrefabFolder, "ResultPanel.prefab");
        public string HealthBarPath => ExistingOrNew(healthBar, uiPrefabFolder, "UnitHealthBar.prefab");
        public string SynergyPanelPath => ExistingOrNew(synergyPanel, uiPrefabFolder, "SynergyPanel.prefab");
        public string UnitPath(UnitPrefabSetupEntry entry) => ExistingOrNew(entry.UnitPrefab, unitPrefabFolder, $"Unit_{entry.SourcePrefab.name}.prefab");

        private static ProjectSetupSettings current;
        public static ProjectSetupSettings Current
        {
            get
            {
                if(current!=null) return current;
                var matches=AssetDatabase.FindAssets($"t:{nameof(ProjectSetupSettings)}");
                if(matches.Length!=1) throw new InvalidOperationException("ProjectSetupSettings SO 하나를 생성하고 에셋 연결 필요.");
                return current=AssetDatabase.LoadAssetAtPath<ProjectSetupSettings>(AssetDatabase.GUIDToAssetPath(matches[0]));
            }
        }
        private static string ExistingOrNew(UnityEngine.Object asset, DefaultAsset folder, string fileName)
        {
            if(asset!=null) return AssetDatabase.GetAssetPath(asset);
            if(folder==null) throw new InvalidOperationException("프리팹 생성 폴더 연결 필요.");
            return Path.Combine(AssetDatabase.GetAssetPath(folder),fileName).Replace('\\','/');
        }
        public UnitPrefabSetupEntry Unit(int visualId)
        {
            foreach(var entry in units) if(entry.VisualId==visualId) return entry;
            throw new ArgumentException($"셋업에 연결되지 않은 외형 ID: {visualId}");
        }
        public void SetBattleHUD(RunHUD prefab) => battleHUD=prefab;
        public void SetShopItem(ShopItem prefab) => shopItem=prefab;
        public void SetResultPanel(ResultPanel prefab) => resultPanel=prefab;
        public void SetHealthBar(UnitHealthBar prefab) => healthBar=prefab;
        public void SetSynergyPanel(SynergyPanel prefab) => synergyPanel=prefab;
        public void Configure(AssetCatalog assets, TextAsset data, SceneAsset scene, RunHUD hud, ShopItem shop,
            ResultPanel result, UnitHealthBar health, DefaultAsset uiFolder, DefaultAsset unitFolder,
            AddressableAssetGroup dataAssets, AddressableAssetGroup unitAssets, UnitPrefabSetupEntry[] entries)
        {
            catalog=assets; snapshot=data; battleScene=scene; battleHUD=hud; shopItem=shop; resultPanel=result;
            healthBar=health; uiPrefabFolder=uiFolder; unitPrefabFolder=unitFolder;
            dataGroup=dataAssets; unitGroup=unitAssets; units=entries;
        }
    }
}
#endif
