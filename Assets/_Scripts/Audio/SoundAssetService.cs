using System;
using System.Collections.Generic;
using System.Threading;
using AutoBattler.Assets;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Audio
{
    /// <summary>효과음의 Addressables 로드와 핸들 소유권 관리.</summary>
    public sealed class SoundAssetService : IDisposable
    {
        private readonly AssetService assets = new AssetService();
        private readonly Dictionary<SoundId, AssetService.Lease<AudioClip>> clips =
            new Dictionary<SoundId, AssetService.Lease<AudioClip>>();
        private bool disposed;
        public bool IsReady { get; private set; }
        public int LoadedCount => clips.Count;
        public int ActiveHandleCount => assets.ActiveHandleCount;

        public async UniTask PreloadAsync(SoundCatalog catalog, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (disposed) throw new ObjectDisposedException(nameof(SoundAssetService));
            if (IsReady) return;
            try
            {
                if (catalog == null) throw new ArgumentNullException(nameof(catalog));
                foreach (SoundEntry entry in catalog.Effects)
                {
                    if (clips.ContainsKey(entry.Id)) continue;
                    AssetService.Lease<AudioClip> lease = await assets.AcquireAsync<AudioClip>(entry.Clip, token);
                    if (disposed || token.IsCancellationRequested)
                    { lease.Dispose(); token.ThrowIfCancellationRequested(); throw new ObjectDisposedException(nameof(SoundAssetService)); }
                    clips.Add(entry.Id, lease);
                }
                IsReady = true;
            }
            catch
            {
                // 일부 로드 뒤 실패·취소되어도 먼저 획득한 핸들까지 반환.
                Clear(); throw;
            }
        }

        public bool TryGetClip(SoundId id, out AudioClip clip)
        {
            if (IsReady && clips.TryGetValue(id, out AssetService.Lease<AudioClip> lease)) { clip = lease.Asset; return clip != null; }
            clip = null; return false;
        }
        private void Clear()
        {
            IsReady = false;
            foreach (AssetService.Lease<AudioClip> lease in clips.Values) lease.Dispose();
            clips.Clear();
        }
        public void Dispose()
        { if (disposed) return; disposed = true; Clear(); assets.Dispose(); }
    }
}
