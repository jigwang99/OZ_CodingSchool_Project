using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AutoBattler.Presentation.Battle
{
    [Serializable]
    public sealed class StageMapRegion
    {
        [SerializeField] private int chapter;
        [SerializeField] private AssetReferenceSprite background;
        [SerializeField] private Vector2[] nodePositions=Array.Empty<Vector2>();
        public int Chapter=>chapter;
        public AssetReferenceSprite Background=>background;
        public IReadOnlyList<Vector2> NodePositions=>Array.AsReadOnly(nodePositions);
    }

    /// <summary>지역 지도와 길 위 선택 지점의 정규화 좌표 설정.</summary>
    [CreateAssetMenu(menuName="PixelTactics/Stage Map Settings")]
    public sealed class StageMapSettings : ScriptableObject
    {
        [SerializeField] private StageMapRegion[] regions=Array.Empty<StageMapRegion>();
        public IReadOnlyList<StageMapRegion> Regions=>Array.AsReadOnly(regions);
        public bool TryGetRegion(int chapter,out StageMapRegion region)
        {
            foreach(StageMapRegion candidate in regions)
                if(candidate.Chapter==chapter) { region=candidate; return true; }
            region=null; return false;
        }
    }
}
