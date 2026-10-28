using System;
using System.Collections.Generic;
using System.Threading;
using AutoBattler.Audio;
using AutoBattler.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Managers
{
    /// <summary>영속 효과음 볼륨과 미리 배치된 2D 재생 채널 관리.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-110)]
    public sealed class SoundManager : Singleton<SoundManager>
    {
        public const string VolumePreferenceKey = "PixelTactics.SfxVolume";
        [SerializeField] private SoundCatalog catalog;
        [SerializeField] private AudioSource[] channels = Array.Empty<AudioSource>();
        [SerializeField, Range(0f, 1f)] private float defaultVolume = .7f;
        private readonly Dictionary<SoundId, double> lastPlayed = new Dictionary<SoundId, double>();
        private readonly BattleSoundService battle = new BattleSoundService();
        private SoundAssetService soundAssets;
        private CancellationTokenSource audioLifetime;
        private UniTaskCompletionSource initialization;
        public SoundCatalog Catalog => catalog;
        public float EffectVolume { get; private set; } = .7f;
        public int ChannelCount => channels.Length;
        public bool IsReady => soundAssets != null && soundAssets.IsReady;
        public bool IsLoading { get; private set; }
        public string LastLoadError { get; private set; }
        public int LoadedClipCount => soundAssets?.LoadedCount ?? 0;
        public int ActiveAssetHandleCount => soundAssets?.ActiveHandleCount ?? 0;
        public event Action<float> VolumeChanged;

        protected override void Awake()
        {
            isDontDestroy = true;
            base.Awake();
            if (!TryGetExisting(out SoundManager current) || current != this) return;
            float stored = PlayerPrefs.GetFloat(VolumePreferenceKey, defaultVolume);
            EffectVolume = Normalize(stored, defaultVolume);
            foreach (AudioSource channel in channels)
                if (channel != null) { channel.Stop(); channel.volume = EffectVolume; }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || !TryGetExisting(out SoundManager current) || current != this) return;
            soundAssets = new SoundAssetService();
            audioLifetime = new CancellationTokenSource();
            PreloadAsync().Forget();
        }

        // 동시에 요청해도 같은 로드 작업 공유. 실패 후 다시 호출하면 재시도.
        public UniTask PreloadAsync()
        {
            if (IsReady || soundAssets == null) return UniTask.CompletedTask;
            if (IsLoading) return initialization.Task;
            IsLoading = true;
            initialization = new UniTaskCompletionSource();
            LoadCoreAsync(soundAssets, audioLifetime.Token, initialization).Forget();
            return initialization.Task;
        }
        private async UniTask LoadCoreAsync(SoundAssetService loading, CancellationToken token, UniTaskCompletionSource completion)
        {
            LastLoadError = null;
            try { await loading.PreloadAsync(catalog, token); }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (token.IsCancellationRequested) return;
                LastLoadError = exception.Message;
                Debug.LogWarning("효과음 불러오기 실패. 로컬 Addressables 빌드 확인 필요. " + exception.Message, this);
            }
            finally
            {
                if (ReferenceEquals(soundAssets, loading)) IsLoading = false;
                completion.TrySetResult();
            }
        }

        public bool TryGetLoadedClip(SoundId id, out AudioClip clip)
        {
            if (soundAssets != null) return soundAssets.TryGetClip(id, out clip);
            clip = null; return false;
        }

        private static float Normalize(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? Mathf.Clamp01(fallback) : Mathf.Clamp01(value);

        public void SetEffectVolume(float value)
        {
            float next = Normalize(value, EffectVolume);
            if (Mathf.Approximately(next, EffectVolume)) return;
            EffectVolume = next;
            foreach (AudioSource channel in channels)
                if (channel != null) channel.volume = next;
            PlayerPrefs.SetFloat(VolumePreferenceKey, next);
            PlayerPrefs.Save();
            VolumeChanged?.Invoke(next);
        }

        // 이벤트 호출부에서는 싱글톤 검색·자동 생성 없이 등록된 매니저만 사용.
        public static bool PlayEffect(SoundId id) => TryGetExisting(out SoundManager manager) && manager.Play(id);
        public bool Play(SoundId id)
        {
            if (!isActiveAndEnabled || EffectVolume <= 0f || catalog == null
                || !catalog.TryGet(id, out SoundEntry entry) || !TryGetLoadedClip(id, out AudioClip clip)) return false;
            double now = AudioSettings.dspTime;
            if (lastPlayed.TryGetValue(id, out double previous) && now - previous < entry.Interval) return false;
            AudioSource available = null;
            foreach (AudioSource channel in channels)
            {
                if (channel == null) continue;
                if (!channel.isPlaying) { available = channel; break; }
                // 낮은 우선순위의 타격음만 더 중요한 UI·결과음에 양보.
                if (channel.priority > entry.Priority && (available == null || channel.priority > available.priority))
                    available = channel;
            }
            if (available == null) return false;
            available.Stop();
            available.clip = clip;
            available.volume = EffectVolume;
            available.priority = entry.Priority;
            // 채널 볼륨으로 재생 중인 소리에도 슬라이더 변경 반영.
            available.PlayOneShot(clip, entry.Volume);
            lastPlayed[id] = now;
            return true;
        }

        public void BeginBattle(IEnumerable<AutoBattler.Battle.Units.Unit> units) => battle.Begin(units);
        public void EndBattle() => battle.End();
        private void OnDisable()
        {
            battle.End();
            ReleaseAudioAssets();
        }
        private void ReleaseAudioAssets()
        {
            // 재생·채널 참조를 먼저 정리한 뒤 클립 핸들 반환.
            foreach (AudioSource channel in channels)
                if (channel != null) { channel.Stop(); channel.clip = null; }
            audioLifetime?.Cancel();
            soundAssets?.Dispose(); soundAssets = null;
            audioLifetime?.Dispose(); audioLifetime = null;
            IsLoading = false;
            lastPlayed.Clear();
        }
        protected override void OnDestroy() { battle.End(); ReleaseAudioAssets(); base.OnDestroy(); }
#if UNITY_EDITOR
        public void Configure(SoundCatalog sounds, AudioSource[] sources)
        {
            bool reload = Application.isPlaying && isActiveAndEnabled && TryGetExisting(out SoundManager current) && current == this;
            if (reload) ReleaseAudioAssets();
            catalog = sounds; channels = sources;
            if (reload) OnEnable();
        }
#endif
    }
}
