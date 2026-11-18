using System;
using System.Collections.Generic;
using System.Linq;
using AutoBattler.Utility;
using UnityEngine;

namespace AutoBattler.Managers
{
    /// <summary>씬 전환 사이 프레임·화면 설정 유지와 게임 종료 담당.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : Singleton<GameManager>
    {
        private const string FrameRatePreferenceKey = "PixelTactics.FrameRate";
        private const string ScreenModePreferenceKey = "PixelTactics.ScreenMode";
        private const string WindowedResolutionPrefix = "PixelTactics.WindowedResolution";
        private const string FullScreenResolutionPrefix = "PixelTactics.FullScreenResolution";
        private static readonly Vector2Int[] StandardResolutions =
        {
            new Vector2Int(960, 540), new Vector2Int(1280, 720), new Vector2Int(1366, 768),
            new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440),
            new Vector2Int(3200, 1800), new Vector2Int(3840, 2160),
        };

        [Header("프레임 설정")]
        [SerializeField] private GameFrameRate defaultFrameRate = GameFrameRate.Fps60;
        [SerializeField] private AutoBattler.Presentation.Battle.GameSettingsView settings;

        [Header("씬 전환 로딩 화면")]
        [SerializeField] private AutoBattler.Presentation.Battle.SceneLoadingView loadingScreen;
        public AutoBattler.Presentation.Battle.SceneLoadingView LoadingScreen=>loadingScreen;

        public GameFrameRate CurrentFrameRate { get; private set; } = GameFrameRate.Fps60;
        public event Action<GameFrameRate> FrameRateChanged;
        public FullScreenMode CurrentScreenMode { get; private set; }
        public Vector2Int CurrentResolution { get; private set; }
        public IReadOnlyList<Vector2Int> ResolutionOptions { get; private set; } = Array.Empty<Vector2Int>();
        public event Action ScreenSettingsChanged;
        public AutoBattler.Presentation.Battle.GameSettingsView Settings => settings;
#if UNITY_EDITOR
        public void ConfigureSettings(AutoBattler.Presentation.Battle.GameSettingsView view)=>settings=view;
#endif

        protected override void Awake()
        {
            isDontDestroy = true;
            base.Awake();

            // 중복 씬 인스턴스의 설정 재적용 방지.
            if (!TryGetExisting(out GameManager registered) || registered != this)
                return;

            int fallback = (int)defaultFrameRate;
            if (fallback != 30 && fallback != 60 && fallback != 144)
                fallback = 60;

            int stored = PlayerPrefs.GetInt(FrameRatePreferenceKey, fallback);
            if (stored != 30 && stored != 60 && stored != 144)
                stored = fallback;

            SetFrameRate((GameFrameRate)stored);
            LoadScreenSettings();
        }

        public void SetFrameRate(GameFrameRate frameRate)
        {
            if (frameRate != GameFrameRate.Fps30
                && frameRate != GameFrameRate.Fps60
                && frameRate != GameFrameRate.Fps144)
            {
                return;
            }

            bool changed = CurrentFrameRate != frameRate;
            CurrentFrameRate = frameRate;

            // 수직 동기화에 의한 프레임 제한 우선 적용 해제.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = (int)frameRate;
            PlayerPrefs.SetInt(FrameRatePreferenceKey, (int)frameRate);
            PlayerPrefs.Save();

            if (changed)
                FrameRateChanged?.Invoke(frameRate);
        }

        private void LoadScreenSettings()
        {
            RefreshScreenOptions();
            int storedMode = PlayerPrefs.GetInt(ScreenModePreferenceKey, -1);
            if (storedMode != (int)FullScreenMode.Windowed && storedMode != (int)FullScreenMode.FullScreenWindow)
                return;

            CurrentScreenMode = (FullScreenMode)storedMode;
            ResolutionOptions = ListResolutions(CurrentScreenMode);
            CurrentResolution = PreferredResolution(CurrentScreenMode);
            Screen.SetResolution(CurrentResolution.x, CurrentResolution.y, CurrentScreenMode);
        }

        public void RefreshScreenOptions()
        {
            // 에디터 Game 뷰에는 운영체제 창 모드 전환이 없음.
            if (!Application.isEditor || CurrentResolution == Vector2Int.zero)
            {
                FullScreenMode actualMode = Screen.fullScreenMode == FullScreenMode.Windowed
                    ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
                if (CurrentResolution == Vector2Int.zero || actualMode != CurrentScreenMode || actualMode == FullScreenMode.Windowed)
                    CurrentResolution = new Vector2Int(Screen.width, Screen.height);
                CurrentScreenMode = actualMode;
            }

            Vector2Int[] options = ListResolutions(CurrentScreenMode);
            if (CurrentResolution.x <= 0 || CurrentResolution.y <= 0) CurrentResolution = options[0];
            ResolutionOptions = Array.AsReadOnly(options.Append(CurrentResolution).Distinct()
                .OrderByDescending(size => size.x).ThenByDescending(size => size.y).ToArray());
            ScreenSettingsChanged?.Invoke();
        }

        public void SetScreenMode(FullScreenMode mode)
        {
            if (mode == CurrentScreenMode || (mode != FullScreenMode.Windowed && mode != FullScreenMode.FullScreenWindow))
                return;

            CurrentScreenMode = mode;
            ResolutionOptions = ListResolutions(mode);
            ApplyResolution(PreferredResolution(mode));
        }

        public void SetResolution(Vector2Int resolution)
        {
            if (resolution != CurrentResolution && ResolutionOptions.Contains(resolution))
                ApplyResolution(resolution);
        }

        private void ApplyResolution(Vector2Int resolution)
        {
            CurrentResolution = resolution;
            Screen.SetResolution(resolution.x, resolution.y, CurrentScreenMode);
            string prefix = ResolutionPrefix(CurrentScreenMode);
            PlayerPrefs.SetInt(ScreenModePreferenceKey, (int)CurrentScreenMode);
            PlayerPrefs.SetInt(prefix + ".Width", resolution.x);
            PlayerPrefs.SetInt(prefix + ".Height", resolution.y);
            PlayerPrefs.Save();
            ScreenSettingsChanged?.Invoke();
        }

        private Vector2Int PreferredResolution(FullScreenMode mode)
        {
            string prefix = ResolutionPrefix(mode);
            Vector2Int fallback = mode == FullScreenMode.Windowed ? new Vector2Int(1280, 720) : ResolutionOptions[0];
            int width = PlayerPrefs.GetInt(prefix + ".Width", fallback.x);
            int height = PlayerPrefs.GetInt(prefix + ".Height", fallback.y);
            if (width <= 0 || height <= 0) { width = fallback.x; height = fallback.y; }
            return ResolutionOptions.OrderBy(size => Math.Abs((long)size.x - width) + Math.Abs((long)size.y - height)).First();
        }

        private static string ResolutionPrefix(FullScreenMode mode) =>
            mode == FullScreenMode.Windowed ? WindowedResolutionPrefix : FullScreenResolutionPrefix;

        private static Vector2Int[] ListResolutions(FullScreenMode mode)
        {
            Resolution desktop = Screen.currentResolution;
            int maxWidth = desktop.width > 0 ? desktop.width : 1920;
            int maxHeight = desktop.height > 0 ? desktop.height : 1080;
#if UNITY_STANDALONE && !UNITY_EDITOR
            DisplayInfo display = Screen.mainWindowDisplayInfo;
            if (display.width > 0 && display.height > 0)
            {
                maxWidth = display.width;
                maxHeight = display.height;
            }
            if (mode == FullScreenMode.Windowed && display.workArea.width > 32 && display.workArea.height > 64)
            {
                // 창 테두리와 제목 표시줄을 포함해 작업 영역 안에 배치.
                maxWidth = display.workArea.width - 24;
                maxHeight = display.workArea.height - 56;
            }
#endif
            HashSet<Vector2Int> options = new HashSet<Vector2Int>();
            foreach (Vector2Int size in StandardResolutions)
                if (size.x <= maxWidth && size.y <= maxHeight) options.Add(size);
            foreach (Resolution resolution in Screen.resolutions)
            {
                float aspect = resolution.width / (float)resolution.height;
                if (resolution.width >= 960 && resolution.height >= 540 && resolution.width <= maxWidth
                    && resolution.height <= maxHeight && Mathf.Abs(aspect - 16f / 9f) < .02f)
                    options.Add(new Vector2Int(resolution.width, resolution.height));
            }
            if (mode == FullScreenMode.FullScreenWindow) options.Add(new Vector2Int(maxWidth, maxHeight));
            if (options.Count == 0)
            {
                int height = Mathf.Max(1, Mathf.Min(maxHeight, maxWidth * 9 / 16));
                options.Add(new Vector2Int(Mathf.Max(1, height * 16 / 9), height));
            }
            return options.OrderByDescending(size => size.x).ThenByDescending(size => size.y).ToArray();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
