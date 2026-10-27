#if UNITY_EDITOR
using System.IO;
using System.Linq;
using AutoBattler.Audio;
using AutoBattler.Managers;
using AutoBattler.Presentation.Battle;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace AutoBattler.EditorTools
{
    /// <summary>효과음 SO·고정 재생 채널·설정 슬라이더를 프리팹에 연결.</summary>
    public static class SoundSetup
    {
        [MenuItem("PixelTactics/Setup/Connect Sound")]
        public static void Connect()
        {
            EnsureSettings();
            string folder = Path.GetDirectoryName(ProjectSetupSettings.Current.GameSettingsPath).Replace('\\', '/');
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (var button in root.GetComponentsInChildren<Button>(true))
                    {
                        if (HasActionSound(button))
                        {
                            // 구매·증강·전투 시작은 결과에 맞는 전용 효과음만 재생.
                            foreach (var existing in button.GetComponents<ButtonSound>())
                            {
                                for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                                    if (button.onClick.GetPersistentTarget(i) == existing)
                                        UnityEventTools.RemovePersistentListener(button.onClick, i);
                                UnityEngine.Object.DestroyImmediate(existing); changed = true;
                            }
                            continue;
                        }
                        var sound = button.GetComponent<ButtonSound>();
                        if (sound == null) { sound = button.gameObject.AddComponent<ButtonSound>(); changed = true; }
                        sound.Configure(button);
                        bool connected = false;
                        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                            if (button.onClick.GetPersistentTarget(i) == sound
                                && button.onClick.GetPersistentMethodName(i) == nameof(ButtonSound.PlayClick)) connected = true;
                        if (!connected) { UnityEventTools.AddPersistentListener(button.onClick, sound.PlayClick); changed = true; }
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("SOUND_SETUP_PASS");
        }

        private static bool HasActionSound(Button button)
        {
            var shop = button.GetComponentInParent<ShopItem>(true);
            if (shop != null && shop.BuyButton == button) return true;
            var augment = button.GetComponentInParent<AugmentCard>(true);
            if (augment != null && augment.Button == button) return true;
            var hud = button.GetComponentInParent<RunHUD>(true);
            return hud != null && (hud.StartBattleButton == button || hud.RefreshButton == button
                || hud.BuyExperienceButton == button);
        }

        public static void EnsureSettings()
        {
            var setup = ProjectSetupSettings.Current;
            var catalog = EnsureCatalog();
            RegisterAddressables(catalog);
            var root = PrefabUtility.LoadPrefabContents(setup.GameSettingsPath);
            try
            {
                var view = root.GetComponent<GameSettingsView>();
                var manager = root.GetComponent<SoundManager>();
                if (manager != null && manager.Catalog != null && manager.ChannelCount > 0
                    && view.EffectVolumeSlider != null) return;
                if (manager == null) manager = root.AddComponent<SoundManager>();
                var sources = new AudioSource[24];
                var pool = new GameObject("EffectChannels"); pool.transform.SetParent(root.transform, false);
                for (int i = 0; i < sources.Length; i++)
                {
                    var go = new GameObject($"Channel_{i + 1:00}"); go.transform.SetParent(pool.transform, false);
                    var source = go.AddComponent<AudioSource>();
                    source.playOnAwake = false; source.loop = false; source.spatialBlend = 0f;
                    source.dopplerLevel = 0f; source.priority = 200; sources[i] = source;
                }
                manager.Configure(catalog, sources);
                var panel = (RectTransform)view.QuitButton.transform.parent;
                panel.sizeDelta = new Vector2(620f, 560f);
                // 기존 프레임 선택과 닫기 버튼 참조 유지.
                foreach (var label in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
                    if (label.transform.parent == panel)
                        label.rectTransform.anchoredPosition = new Vector2(0f, label.text == GameSettingsView.Heading ? 220f : 145f);
                ((RectTransform)view.FrameRateDropdown.transform).anchoredPosition = new Vector2(0f, 90f);
                ((RectTransform)view.QuitButton.transform).anchoredPosition = new Vector2(0f, -190f);
                Label(panel, "EffectVolumeHeading", GameSettingsView.EffectVolumeHeading, new Vector2(0f, 10f), new Vector2(420f, 40f));
                var value = Label(panel, "EffectVolumeValue", "70%", new Vector2(223f, -48f), new Vector2(82f, 40f));
                var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
                sliderObject.name = "EffectVolumeSlider"; sliderObject.transform.SetParent(panel, false);
                var rect = sliderObject.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(-46f, -48f); rect.sizeDelta = new Vector2(360f, 36f);
                var slider = sliderObject.GetComponent<Slider>();
                slider.minValue = 0f; slider.maxValue = 1f; slider.wholeNumbers = false; slider.SetValueWithoutNotify(.7f);
                slider.fillRect.GetComponent<Image>().color = new Color(.2f, .8f, .65f);
                slider.handleRect.GetComponent<Image>().color = new Color(.85f, 1f, .95f);
                foreach (var child in sliderObject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = LayerMask.NameToLayer("UI");
                view.ConfigureSound(slider, value);
                PrefabUtility.SaveAsPrefabAsset(root, setup.GameSettingsPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void RegisterAddressables(SoundCatalog catalog)
        {
            var setup = ProjectSetupSettings.Current;
            var addressables = setup.UnitGroup.Settings;
            var group = setup.SoundGroup;
            if (group == null)
            {
                group = addressables.FindGroup("SoundEffects") ?? addressables.CreateGroup("SoundEffects", false, false, false, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                setup.SetSoundGroup(group); EditorUtility.SetDirty(setup);
            }
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            foreach (var entry in catalog.Effects)
            {
                string path = AssetDatabase.GUIDToAssetPath(entry.Clip.AssetGUID);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) throw new FileNotFoundException("효과음 AssetReference 연결 필요: " + entry.Id);
                MilestoneSetup.AddAddress(addressables, group, clip);
            }
            EditorUtility.SetDirty(group); EditorUtility.SetDirty(addressables);
        }

        private static TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false); var label = go.AddComponent<TextMeshProUGUI>();
            label.rectTransform.anchoredPosition = position; label.rectTransform.sizeDelta = size;
            label.font = ProjectSetupSettings.Current.UnitInfoFont; label.fontSize = 25f; label.text = text;
            label.color = Color.white; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            return label;
        }

        private static SoundCatalog EnsureCatalog()
        {
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(ProjectSetupSettings.Current)).Replace('\\', '/');
            string path = folder + "/SoundCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<SoundCatalog>(path);
            if (catalog != null)
            {
                // 기존 공통 Confirm 설정만 한 번 교체. 이후 인스펙터 조정값 유지.
                var confirm = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Leohpaz/RPG_Essentials_Free/10_UI_Menu_SFX/013_Confirm_03.wav");
                if (catalog.TryGet(SoundId.Click, out var click) && click.Clip.AssetGUID == AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(confirm)))
                {
                    var lightClick = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Leohpaz/RPG_Essentials_Free/10_UI_Menu_SFX/001_Hover_01.wav");
                    if (lightClick == null) throw new FileNotFoundException("UI 클릭 효과음 에셋 연결 필요.");
                    catalog.Configure(System.Enum.GetValues(typeof(SoundId)).Cast<SoundId>()
                        .Select(id => catalog.TryGet(id, out var entry) ? id == SoundId.Click
                            ? new SoundEntry(id, Reference(lightClick), .15f, .14f, entry.Priority) : entry : null)
                        .Where(entry => entry != null).ToArray());
                    EditorUtility.SetDirty(catalog);
                }
                return catalog;
            }
            catalog = ScriptableObject.CreateInstance<SoundCatalog>();
            const string pack = "Assets/Leohpaz/RPG_Essentials_Free/";
            SoundEntry Entry(SoundId id, string audio, float gain, float interval, int priority)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(pack + audio + ".wav");
                if (clip == null) throw new FileNotFoundException("효과음 에셋 연결 필요: " + audio);
                return new SoundEntry(id, Reference(clip), gain, interval, priority);
            }
            catalog.Configure(new[]
            {
                Entry(SoundId.Click,"10_UI_Menu_SFX/001_Hover_01",.15f,.14f,40),
                Entry(SoundId.Denied,"10_UI_Menu_SFX/033_Denied_03",.5f,.15f,35),
                Entry(SoundId.Trade,"10_UI_Menu_SFX/079_Buy_sell_01",.55f,.06f,45),
                Entry(SoundId.Refresh,"10_UI_Menu_SFX/051_use_item_01",.45f,.1f,50),
                Entry(SoundId.Place,"10_UI_Menu_SFX/070_Equip_10",.4f,.08f,60),
                Entry(SoundId.Augment,"8_Buffs_Heals_SFX/16_Atk_buff_04",.55f,.2f,30),
                Entry(SoundId.BattleStart,"10_Battle_SFX/55_Encounter_02",.55f,.3f,25),
                Entry(SoundId.Victory,"8_Buffs_Heals_SFX/30_Revive_03",.6f,.3f,20),
                Entry(SoundId.Defeat,"10_Battle_SFX/51_Flee_02",.5f,.3f,20),
                Entry(SoundId.Hit,"10_Battle_SFX/15_Impact_flesh_02",.28f,.07f,200),
                Entry(SoundId.MagicHit,"10_Battle_SFX/77_flesh_02",.25f,.09f,200),
                Entry(SoundId.Death,"10_Battle_SFX/69_Enemy_death_01",.25f,.14f,180),
                Entry(SoundId.Heal,"8_Buffs_Heals_SFX/02_Heal_02",.45f,.16f,140),
                Entry(SoundId.Shield,"10_Battle_SFX/39_Block_03",.3f,.12f,170),
                Entry(SoundId.Slash,"10_Battle_SFX/22_Slash_04",.4f,.12f,150),
                Entry(SoundId.Shoot,"12_Player_Movement_SFX/56_Attack_03",.35f,.12f,150),
                Entry(SoundId.Fire,"8_Atk_Magic_SFX/04_Fire_explosion_04_medium",.4f,.18f,130),
                Entry(SoundId.HeavyImpact,"8_Atk_Magic_SFX/30_Earth_02",.4f,.16f,140)
            });
            AssetDatabase.CreateAsset(catalog, path);
            return catalog;
        }
        private static AssetReferenceT<AudioClip> Reference(AudioClip clip) =>
            new AssetReferenceT<AudioClip>(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip)));
    }
}
#endif
