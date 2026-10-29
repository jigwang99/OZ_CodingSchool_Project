using System;
using AutoBattler.Utility;
using UnityEngine;

namespace AutoBattler.Managers
{
    /// <summary>씬 전환 사이 프레임 설정 유지와 게임 종료 담당.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : Singleton<GameManager>
    {
        private const string FrameRatePreferenceKey = "PixelTactics.FrameRate";

        [Header("프레임 설정")]
        [SerializeField] private GameFrameRate defaultFrameRate = GameFrameRate.Fps60;
        [SerializeField] private AutoBattler.Presentation.Battle.GameSettingsView settings;

        [Header("씬 전환 로딩 화면")]
        [SerializeField] private AutoBattler.Presentation.Battle.SceneLoadingView loadingScreen;
        public AutoBattler.Presentation.Battle.SceneLoadingView LoadingScreen=>loadingScreen;

        public GameFrameRate CurrentFrameRate { get; private set; } = GameFrameRate.Fps60;
        public event Action<GameFrameRate> FrameRateChanged;
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
