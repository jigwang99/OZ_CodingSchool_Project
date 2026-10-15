#if UNITY_EDITOR
using System;
using System.Linq;
using AutoBattler.Presentation.Battle;
using AutoBattler.Run;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AutoBattler.EditorTools
{
    /// <summary>편집 가능한 BattleHUD 프리팹 생성과 씬 연결. 런타임에는 실행하지 않음.</summary>
    public static class BattleHUDSetup
    {
        public static string PrefabPath => ProjectSetupSettings.Current.HUDPath;
        private static string ScenePath => ProjectSetupSettings.Current.ScenePath;
        private static readonly Color PanelColor = new Color(0.075f, 0.11f, 0.17f, 0.96f);
        private static readonly Color ButtonColor = new Color(0.17f, 0.28f, 0.4f);

        [MenuItem("PixelTactics/Setup/Create BattleHUD Prefab")]
        public static void CreatePrefab()
        {
            Selection.activeObject = EnsurePrefab();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("PixelTactics/Setup/Connect BattleHUD Scene")]
        public static void ConnectScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("편집 모드에서 실행 필요.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("BattleScene을 연 뒤 실행 필요.");
            RunController run = UnityEngine.Object.FindObjectsByType<RunController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(x => x.gameObject.scene == scene);
            if (run == null)
                throw new InvalidOperationException("BattleScene의 RunController 연결 필요.");
            Connect(scene, run);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static RunHUD Connect(Scene scene, RunController run)
        {
            var existing = UnityEngine.Object.FindObjectsByType<RunHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(x => x.gameObject.scene == scene).ToArray();
            var hud = existing.FirstOrDefault(x => x.GetComponent<Canvas>() != null);
            if (hud == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(EnsurePrefab(), scene);
                hud = instance.GetComponent<RunHUD>();
            }

            // 이전 컨트롤러에 붙어 있던 UI 생성 컴포넌트만 제거.
            foreach (var oldHud in existing)
                if (oldHud != hud)
                    UnityEngine.Object.DestroyImmediate(oldHud);

            hud.ConfigureScene(run);
            EditorUtility.SetDirty(hud);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            return hud;
        }

        public static GameObject EnsurePrefab()
        {
            // 이미 꾸민 프리팹은 다시 생성하거나 덮어쓰지 않음.
            var existing = ProjectSetupSettings.Current.BattleHUD != null ? ProjectSetupSettings.Current.BattleHUD.gameObject : null;
            if (existing != null)
                return existing;

            var root = new GameObject("BattleHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = LayerMask.NameToLayer("UI");
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                RectTransform rootRect = root.GetComponent<RectTransform>();
                var hud = root.AddComponent<RunHUD>();

                var header = Panel(rootRect, "Header", new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(1920f, 116f));
                StretchWidth(header);
                string[] names = { "RoundText", "GoldText", "LivesText", "LevelText", "ExperienceText", "DeploymentText" };
                string[] initial = { "Round --", "Gold --", "Lives --", "Level --", "XP --", "Deployed --" };
                var labels = new TextMeshProUGUI[names.Length];
                for (int i = 0; i < labels.Length; i++)
                    labels[i] = Label(header, names[i], new Vector2(-780f + i * 312f, 25f), new Vector2(290f, 40f), 27f, initial[i]);
                var message = Label(header, "MessageText", new Vector2(-220f, -25f), new Vector2(1370f, 38f), 21f, "Loading data...");
                hud.ConfigureHeader(labels[0], labels[1], labels[2], labels[3], labels[4], labels[5], message);
                var start = Button(header, "StartBattleButton", "Start battle", new Vector2(780f, -25f), new Vector2(270f, 46f));
                var retry = Button(header, "RetryLoadButton", "Retry load", new Vector2(780f, -25f), new Vector2(270f, 46f));

                var preparation = Panel(rootRect, "PreparationPanel", new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(1920f, 128f));
                StretchWidth(preparation);
                var preparationGroup = preparation.gameObject.AddComponent<CanvasGroup>();
                RectTransform shopContainer = Rect(preparation, "ShopContainer", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 128f));
                hud.ConfigureShopContainer(shopContainer);
                ShopItemSetup.ConfigureSlots(shopContainer, hud);
                var refresh = Button(preparation, "RefreshButton", "Refresh", new Vector2(650f, 32f), new Vector2(190f, 54f));
                var experience = Button(preparation, "BuyExperienceButton", "Buy XP", new Vector2(855f, 32f), new Vector2(180f, 54f));
                var sell = Button(preparation, "SellButton", "Sell selected", new Vector2(650f, -32f), new Vector2(190f, 54f));
                var selected = Label(preparation, "SelectedUnitText", new Vector2(855f, -32f), new Vector2(180f, 60f), 19f, "Select a unit");
                hud.ConfigurePreparation(preparationGroup, shopContainer, hud.ShopItems.ToArray(), refresh, refresh.GetComponentInChildren<TextMeshProUGUI>(),
                    experience, experience.GetComponentInChildren<TextMeshProUGUI>(), sell, selected);
                hud.ConfigureActions(start, retry, null);
                ReusableUISetup.ConfigureResultPanel(rootRect, hud);
                SynergyUISetup.ConfigurePanel(hud);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                ProjectSetupSettings.Current.SetBattleHUD(prefab.GetComponent<RunHUD>());
                EditorUtility.SetDirty(ProjectSetupSettings.Current);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static RectTransform Rect(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.layer = LayerMask.NameToLayer("UI");
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void StretchWidth(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
            rect.anchorMax = new Vector2(1f, rect.anchorMax.y);
            rect.sizeDelta = new Vector2(0f, rect.sizeDelta.y);
        }

        private static RectTransform Panel(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = Rect(parent, name, anchor, position, size);
            rect.gameObject.AddComponent<Image>().color = PanelColor;
            return rect;
        }

        private static TextMeshProUGUI Label(RectTransform parent, string name, Vector2 position, Vector2 size, float fontSize, string text)
        {
            var rect = Rect(parent, name, new Vector2(0.5f, 0.5f), position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.text = text;
            return label;
        }

        private static Button Button(RectTransform parent, string name, string text, Vector2 position, Vector2 size)
        {
            var rect = Panel(parent, name, new Vector2(0.5f, 0.5f), position, size);
            var image = rect.GetComponent<Image>();
            image.color = ButtonColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.8f, 0.95f, 1f);
            colors.pressedColor = new Color(0.65f, 0.8f, 0.9f);
            button.colors = colors;
            Label(rect, "Label", Vector2.zero, size - new Vector2(16f, 8f), 22f, text);
            return button;
        }

        public static void BuildForVerification()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var run = UnityEngine.Object.FindFirstObjectByType<RunController>();
            var hud = Connect(scene, run);
            if (!PrefabUtility.IsPartOfPrefabInstance(hud))
                throw new InvalidOperationException("씬의 BattleHUD 프리팹 연결 실패.");
            // 에디터에서 프리팹 참조와 재실행 시 사용자 배치 보존 확인.
            var prefab = EnsurePrefab();
            var view = prefab.GetComponent<RunHUD>();
            UnityEngine.Object[] references = { view.RoundText, view.GoldText, view.LivesText, view.LevelText,
                view.ExperienceText, view.DeploymentText, view.MessageText, view.PreparationPanel,
                view.ShopContainer, view.RefreshButton, view.BuyExperienceButton, view.SellButton,
                view.SelectedUnitText, view.StartBattleButton, view.RetryLoadButton, view.ResultPanel };
            if (references.Any(reference => reference == null) || view.ShopItems.Any(item => item == null))
                throw new InvalidOperationException("BattleHUD 참조 연결 필요.");
            int id = hud.GetInstanceID();
            if (Connect(scene, run).GetInstanceID() != id)
                throw new InvalidOperationException("기존 BattleHUD 인스턴스 보존 실패.");
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("BATTLE_HUD_PREFAB_PASS");
        }
    }
}
#endif
