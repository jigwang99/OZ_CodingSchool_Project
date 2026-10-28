using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AutoBattler.Audio
{
    public enum SoundId
    {
        Click, Denied, Trade, Refresh, Place, Augment, BattleStart, Victory, Defeat,
        Hit, MagicHit, Death, Heal, Shield, Slash, Shoot, Fire, HeavyImpact
    }

    [Serializable]
    public sealed class SoundEntry
    {
        [SerializeField] private SoundId id;
        [SerializeField] private AssetReferenceT<AudioClip> clip;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField, Min(0f)] private float interval = .08f;
        [SerializeField, Range(0, 256)] private int priority = 128;
        public SoundId Id => id;
        public AssetReferenceT<AudioClip> Clip => clip;
        public float Volume => volume;
        public float Interval => interval;
        public int Priority => priority;
#if UNITY_EDITOR
        public SoundEntry(SoundId key, AssetReferenceT<AudioClip> audio, float gain, float spacing, int importance)
        { id = key; clip = audio; volume = gain; interval = spacing; priority = importance; }
#endif
    }

    [CreateAssetMenu(menuName = "PixelTactics/Audio/Sound Catalog")]
    public sealed class SoundCatalog : ScriptableObject
    {
        [SerializeField] private SoundEntry[] effects = Array.Empty<SoundEntry>();
        public IReadOnlyList<SoundEntry> Effects => Array.AsReadOnly(effects);
        public bool TryGet(SoundId id, out SoundEntry found)
        {
            foreach (SoundEntry entry in effects)
                if (entry != null && entry.Id == id) { found = entry; return true; }
            found = null; return false;
        }
#if UNITY_EDITOR
        public void Configure(SoundEntry[] entries) => effects = entries;
#endif
    }
}
