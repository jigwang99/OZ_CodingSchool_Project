#if UNITY_EDITOR
using System;
using AutoBattler.Assets;
using AutoBattler.Presentation.Battle;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.EditorTools
{
    /// <summary>상점 카드 프리팹 생성과 BattleHUD의 기존 슬롯 교체.</summary>
    public static partial class ShopItemSetup
    {
        public static string PrefabPath => ProjectSetupSettings.Current.ShopItemPath;

        [MenuItem("PixelTactics/Setup/Create ShopItem Prefab")]
        public static void CreatePrefab()
        {
            Selection.activeObject = EnsurePrefab();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("PixelTactics/Setup/Connect ShopItems to BattleHUD")]
        public static void UpgradeBattleHUD()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("편집 모드에서 실행 필요.");
            EnsurePrefab();
            var root = PrefabUtility.LoadPrefabContents(BattleHUDSetup.PrefabPath);
            try
            {
                var hud = root.GetComponent<RunHUD>();
                ConfigureSlots(hud.ShopContainer, hud);
                PrefabUtility.SaveAsPrefabAsset(root, BattleHUDSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            InitializeIconEntries();
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureSlots(RectTransform container, RunHUD hud)
        {
            var prefab = EnsurePrefab();
            var items = new ShopItem[5];
            for (int i = 0; i < items.Length; i++)
            {
                // 이미 연결된 슬롯은 이름과 위치에 관계없이 그대로 사용.
                var item = i < hud.ShopItems.Count ? hud.ShopItems[i] : null;
                if (item == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
                    instance.name = "ShopSlot" + (i + 1);
                    var rect = instance.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = new Vector2(-670f + i * 250f, 0f);
                    rect.sizeDelta = new Vector2(236f, 110f);
                    item = instance.GetComponent<ShopItem>();
                }
                items[i] = item;
            }
            hud.ConfigureShopItems(items);
            EditorUtility.SetDirty(hud);
        }

        public static GameObject EnsurePrefab()
        {
            var existing = ProjectSetupSettings.Current.ShopItem != null ? ProjectSetupSettings.Current.ShopItem.gameObject : null;
            if (existing != null)
                return existing;
            var root = new GameObject("ShopItem", typeof(RectTransform), typeof(Image), typeof(Button));
            root.layer = LayerMask.NameToLayer("UI");
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(236f, 110f);
            try
            {
                var image = root.GetComponent<Image>();
                image.color = new Color(0.17f, 0.28f, 0.4f);
                var button = root.GetComponent<Button>();
                button.targetGraphic = image;
                button.interactable = false;
                var colors = button.colors;
                colors.highlightedColor = new Color(0.8f, 0.95f, 1f);
                colors.pressedColor = new Color(0.65f, 0.8f, 0.9f);
                button.colors = colors;
                var item = root.AddComponent<ShopItem>();

                var content = Rect(root.GetComponent<RectTransform>(), "Content", Vector2.zero, new Vector2(236f, 110f));
                content.anchorMin = Vector2.zero;
                content.anchorMax = Vector2.one;
                content.sizeDelta = Vector2.zero;
                var icon = Rect(content, "Icon", new Vector2(-74f, 0f), new Vector2(64f, 64f));
                var iconImage = icon.gameObject.AddComponent<Image>();
                iconImage.raycastTarget = false;
                iconImage.preserveAspect = true;
                iconImage.color = new Color(0.25f, 0.4f, 0.55f);
                var placeholder = Label(icon, "IconPlaceholderText", Vector2.zero, new Vector2(60f, 60f), 34f, "?");
                var nameLabel = Label(content, "NameText", new Vector2(35f, 29f), new Vector2(142f, 32f), 21f, "기물");
                var price = Label(content, "PriceText", new Vector2(35f, 0f), new Vector2(142f, 28f), 19f, "1골드");
                var pool = Label(content, "PoolText", new Vector2(35f, -27f), new Vector2(142f, 26f), 17f, "");
                var empty = Label(root.GetComponent<RectTransform>(), "EmptyText", Vector2.zero, new Vector2(220f, 90f), 23f, "빈 상점");
                content.gameObject.SetActive(false);
                item.ConfigureReferences(button, content.gameObject, iconImage, placeholder, nameLabel, price, pool, empty);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                ProjectSetupSettings.Current.SetShopItem(prefab.GetComponent<ShopItem>());
                EditorUtility.SetDirty(ProjectSetupSettings.Current);
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void InitializeIconEntries()
        {
            var catalog = ProjectSetupSettings.Current.Catalog;
            if (catalog.icons != null && catalog.icons.Length > 0)
                return;
            catalog.icons = new UnitIconEntry[8];
            for (int i = 0; i < catalog.icons.Length; i++)
                catalog.icons[i] = new UnitIconEntry { id = i + 1 };
            EditorUtility.SetDirty(catalog);
        }

        private static RectTransform Rect(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.layer = LayerMask.NameToLayer("UI");
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static TextMeshProUGUI Label(RectTransform parent, string name, Vector2 position, Vector2 size, float fontSize, string text)
        {
            var label = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = ProjectSetupSettings.Current.UnitInfoFont;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

    }
}
#endif
