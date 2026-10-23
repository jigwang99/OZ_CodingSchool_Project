#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AutoBattler.Assets;
using AutoBattler.Run;
using AutoBattler.Presentation.Battle;
using AutoBattler.Presentation.Units;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using TMPro;

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
        [SerializeField] private AutoBattler.Presentation.Units.BattleVisualSettings battleVisuals;
        [SerializeField] private AddressableAssetGroup battleVisualGroup;
        public AutoBattler.Presentation.Units.BattleVisualSettings BattleVisuals=>battleVisuals;
        public AddressableAssetGroup BattleVisualGroup=>battleVisualGroup;
        public void SetBattleVisuals(AutoBattler.Presentation.Units.BattleVisualSettings settings,AddressableAssetGroup group)
        { battleVisuals=settings; battleVisualGroup=group; }
        [SerializeField] private TextAsset snapshot;
        [SerializeField] private SceneAsset battleScene;
        [SerializeField] private SceneAsset titleScene;
        [SerializeField] private TitleMenu titleMenu;
        [SerializeField] private GameSettingsView gameSettings;
        [SerializeField] private GameNavigationSettings navigation;
        [SerializeField] private StageDefinition firstStage;
        [SerializeField] private RunHUD battleHUD;
        [SerializeField] private ShopItem shopItem;
        [SerializeField] private ResultPanel resultPanel;
        [SerializeField] private UnitHealthBar healthBar;
        [SerializeField] private SynergyPanel synergyPanel;
        [SerializeField] private AugmentSelectionPanel augmentPanel;
        [SerializeField] private AugmentCard augmentCard;
        [SerializeField] private SelectedAugmentsView selectedAugments;
        [SerializeField] private UnitInfoPanel unitInfo;
        [SerializeField] private Font unitInfoSourceFont;
        [SerializeField] private TMP_FontAsset unitInfoFont;
        [SerializeField] private DefaultAsset uiPrefabFolder;
        [SerializeField] private DefaultAsset unitPrefabFolder;
        [SerializeField] private AddressableAssetGroup dataGroup;
        [SerializeField] private AddressableAssetGroup unitGroup;
        [SerializeField] private UnitPrefabSetupEntry[] units=Array.Empty<UnitPrefabSetupEntry>();

        public AssetCatalog Catalog => catalog;
        public TextAsset Snapshot => snapshot;
        public SceneAsset BattleScene => battleScene;
        public SceneAsset TitleScene=>titleScene;
        public TitleMenu TitleMenu=>titleMenu;
        public GameSettingsView GameSettings=>gameSettings;
        public string GameSettingsPath=>ExistingOrNew(gameSettings,uiPrefabFolder,"GameSettings.prefab");
        public GameNavigationSettings Navigation=>navigation;
        public StageDefinition FirstStage=>firstStage;
        public string TitleScenePath=>AssetDatabase.GetAssetPath(titleScene);
        public string TitleMenuPath=>ExistingOrNew(titleMenu,uiPrefabFolder,"TitleMenu.prefab");
        public string NavigationPath=>Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(this)),"GameNavigationSettings.asset").Replace('\\','/');
        public string FirstStagePath=>Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(this)),"Stage_1_1.asset").Replace('\\','/');
        public RunHUD BattleHUD => battleHUD;
        public ShopItem ShopItem => shopItem;
        public ResultPanel ResultPanel => resultPanel;
        public UnitHealthBar HealthBar => healthBar;
        public SynergyPanel SynergyPanel => synergyPanel;
        public AugmentSelectionPanel AugmentPanel=>augmentPanel;
        public AugmentCard AugmentCard=>augmentCard;
        public SelectedAugmentsView SelectedAugments=>selectedAugments;
        public UnitInfoPanel UnitInfo => unitInfo;
        public Font UnitInfoSourceFont => unitInfoSourceFont;
        public TMP_FontAsset UnitInfoFont => unitInfoFont;
        public string UnitInfoFontPath => Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(this)), "UnitInfoKoreanFont.asset").Replace('\\', '/');
        public string UnitInfoPath => ExistingOrNew(unitInfo, uiPrefabFolder, "UnitInfoPanel.prefab");
        public string SelectedAugmentsPath=>ExistingOrNew(selectedAugments,uiPrefabFolder,"SelectedAugments.prefab");
        public string AugmentPanelPath=>ExistingOrNew(augmentPanel,uiPrefabFolder,"AugmentSelectionPanel.prefab");
        public string AugmentCardPath=>ExistingOrNew(augmentCard,uiPrefabFolder,"AugmentCard.prefab");
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
        public void SetGameSettings(GameSettingsView prefab)=>gameSettings=prefab;
        public void SetShopItem(ShopItem prefab) => shopItem=prefab;
        public void SetResultPanel(ResultPanel prefab) => resultPanel=prefab;
        public void SetHealthBar(UnitHealthBar prefab) => healthBar=prefab;
        public void SetSynergyPanel(SynergyPanel prefab) => synergyPanel=prefab;
        public void SetAugmentPanel(AugmentSelectionPanel prefab)=>augmentPanel=prefab;
        public void SetAugmentCard(AugmentCard prefab)=>augmentCard=prefab;
        public void SetSelectedAugments(SelectedAugmentsView prefab)=>selectedAugments=prefab;
        public void SetUnitInfo(UnitInfoPanel prefab) => unitInfo = prefab;
        public void SetUnitInfoFont(TMP_FontAsset font) => unitInfoFont = font;
        public void ConfigureEntryFlow(SceneAsset title,TitleMenu menu,GameNavigationSettings settings,StageDefinition stage)
        { titleScene=title; titleMenu=menu; navigation=settings; firstStage=stage; }
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
