using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AutoBattler.Audio
{
    [Serializable]
    public sealed class RegionMusicEntry
    {
        [SerializeField,Min(1)] private int chapter;
        [SerializeField] private string regionName;
        [SerializeField] private AssetReferenceT<AudioClip> clip;
        public int Chapter=>chapter;
        public string RegionName=>regionName;
        public AssetReferenceT<AudioClip> Clip=>clip;
    }
    /// <summary>타이틀과 여섯 지역의 배경음악 Addressables 참조.</summary>
    [CreateAssetMenu(menuName="PixelTactics/Music Catalog")]
    public sealed class MusicCatalog:ScriptableObject
    {
        [SerializeField] private AssetReferenceT<AudioClip> menu;
        [SerializeField] private RegionMusicEntry[] regions=Array.Empty<RegionMusicEntry>();
        public AssetReferenceT<AudioClip> Menu=>menu;
        public IReadOnlyList<RegionMusicEntry> Regions=>Array.AsReadOnly(regions);
        public bool TryGet(int chapter,out AssetReferenceT<AudioClip> clip)
        {
            foreach(RegionMusicEntry entry in regions)if(entry.Chapter==chapter){clip=entry.Clip;return clip!=null;}
            clip=null;return false;
        }
    }
}
