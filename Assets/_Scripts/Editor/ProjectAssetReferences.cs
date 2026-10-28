#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AutoBattler.Assets;
using AutoBattler.Presentation.Battle;
using AutoBattler.Presentation.Units;
using AutoBattler.Run;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    [Serializable]
    public sealed class UnitPrefabReference
    {
        [SerializeField] private int visualId;
        [SerializeField] private GameObject sourcePrefab;
        [SerializeField] private GameObject unitPrefab;
        public int VisualId=>visualId;
        public GameObject SourcePrefab=>sourcePrefab;
        public GameObject UnitPrefab=>unitPrefab;
    }
    /// <summary>Inspector에서 편집할 프로젝트 에셋의 참조 목록. 에셋 생성·씬 수정 없음.</summary>
    [CreateAssetMenu(menuName="PixelTactics/Editor/Project Asset References")]
    public sealed class ProjectAssetReferences : ScriptableObject
    {
        [Header("게임 데이터와 장면")]
        [SerializeField] private AssetCatalog catalog;
        [SerializeField] private TextAsset snapshot;
        [SerializeField] private SceneAsset battleScene,titleScene;
        [SerializeField] private GameNavigationSettings navigation;
        [SerializeField] private StageDefinition firstStage;
        [Header("UI 프리팹")]
        [SerializeField] private TitleMenu titleMenu;
        [SerializeField] private GameSettingsView gameSettings;
        [SerializeField] private RunHUD battleHUD;
        [SerializeField] private ShopItem shopItem;
        [SerializeField] private ResultPanel resultPanel;
        [SerializeField] private UnitHealthBar healthBar;
        [SerializeField] private SynergyPanel synergyPanel;
        [SerializeField] private AugmentSelectionPanel augmentPanel;
        [SerializeField] private AugmentCard augmentCard;
        [SerializeField] private SelectedAugmentsView selectedAugments;
        [SerializeField] private UnitInfoPanel unitInfo;
        [SerializeField] private ItemInventoryPanel itemInventory;
        [Header("전투 환경 프리팹")]
        [SerializeField] private GameObject battleStage,boardPrefab,backgroundPrefab,identityPrefab,inputPrefab;
        [Header("표현과 폰트")]
        [SerializeField] private BattleVisualSettings battleVisuals;
        [SerializeField] private Font unitInfoSourceFont;
        [SerializeField] private TMP_FontAsset unitInfoFont;
        [Header("Addressables 그룹")]
        [SerializeField] private AddressableAssetGroup dataGroup,unitGroup,battleVisualGroup,soundGroup,backgroundGroup,itemIconGroup;
        [Header("유닛 프리팹")]
        [SerializeField] private UnitPrefabReference[] units=Array.Empty<UnitPrefabReference>();
        public AssetCatalog Catalog=>catalog;
        public TextAsset Snapshot=>snapshot;
        public SceneAsset BattleScene=>battleScene;
        public SceneAsset TitleScene=>titleScene;
        public GameNavigationSettings Navigation=>navigation;
        public StageDefinition FirstStage=>firstStage;
        public TitleMenu TitleMenu=>titleMenu;
        public GameSettingsView GameSettings=>gameSettings;
        public RunHUD BattleHUD=>battleHUD;
        public ShopItem ShopItem=>shopItem;
        public ResultPanel ResultPanel=>resultPanel;
        public UnitHealthBar HealthBar=>healthBar;
        public SynergyPanel SynergyPanel=>synergyPanel;
        public AugmentSelectionPanel AugmentPanel=>augmentPanel;
        public AugmentCard AugmentCard=>augmentCard;
        public SelectedAugmentsView SelectedAugments=>selectedAugments;
        public UnitInfoPanel UnitInfo=>unitInfo;
        public ItemInventoryPanel ItemInventory=>itemInventory;
        public GameObject BattleStage=>battleStage;
        public GameObject BoardPrefab=>boardPrefab;
        public GameObject BackgroundPrefab=>backgroundPrefab;
        public GameObject IdentityPrefab=>identityPrefab;
        public GameObject InputPrefab=>inputPrefab;
        public BattleVisualSettings BattleVisuals=>battleVisuals;
        public Font UnitInfoSourceFont=>unitInfoSourceFont;
        public TMP_FontAsset UnitInfoFont=>unitInfoFont;
        public AddressableAssetGroup DataGroup=>dataGroup;
        public AddressableAssetGroup UnitGroup=>unitGroup;
        public AddressableAssetGroup BattleVisualGroup=>battleVisualGroup;
        public AddressableAssetGroup SoundGroup=>soundGroup;
        public AddressableAssetGroup BackgroundGroup=>backgroundGroup;
        public AddressableAssetGroup ItemIconGroup=>itemIconGroup;
        public IReadOnlyList<UnitPrefabReference> Units=>Array.AsReadOnly(units);
        public string ScenePath=>AssetDatabase.GetAssetPath(battleScene);
        public string TitleScenePath=>AssetDatabase.GetAssetPath(titleScene);
        public string TitleMenuPath=>AssetDatabase.GetAssetPath(titleMenu);
        public string GameSettingsPath=>AssetDatabase.GetAssetPath(gameSettings);
        public string HUDPath=>AssetDatabase.GetAssetPath(battleHUD);
        public string ShopItemPath=>AssetDatabase.GetAssetPath(shopItem);
        public string ResultPath=>AssetDatabase.GetAssetPath(resultPanel);
        public string HealthBarPath=>AssetDatabase.GetAssetPath(healthBar);
        public string SynergyPanelPath=>AssetDatabase.GetAssetPath(synergyPanel);
        public string AugmentPanelPath=>AssetDatabase.GetAssetPath(augmentPanel);
        public string AugmentCardPath=>AssetDatabase.GetAssetPath(augmentCard);
        public string SelectedAugmentsPath=>AssetDatabase.GetAssetPath(selectedAugments);
        public string UnitInfoPath=>AssetDatabase.GetAssetPath(unitInfo);
        public string UnitPath(UnitPrefabReference entry)=>AssetDatabase.GetAssetPath(entry.UnitPrefab);
        public UnitPrefabReference Unit(int id)
        {
            foreach(UnitPrefabReference entry in units) if(entry.VisualId==id) return entry;
            throw new ArgumentException($"미등록 유닛 외형 ID: {id}");
        }
        private static ProjectAssetReferences current;
        public static ProjectAssetReferences Current
        {
            get
            {
                if(current!=null) return current;
                string[] matches=AssetDatabase.FindAssets($"t:{nameof(ProjectAssetReferences)}");
                if(matches.Length!=1) throw new InvalidOperationException("ProjectAssetReferences 에셋 하나에 프로젝트 참조 연결 필요.");
                return current=AssetDatabase.LoadAssetAtPath<ProjectAssetReferences>(AssetDatabase.GUIDToAssetPath(matches[0]));
            }
        }
    }
}
#endif
