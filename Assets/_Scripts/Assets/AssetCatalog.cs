using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AutoBattler.Assets
{
    [Serializable] public sealed class CatalogEntry { public int id; public AssetReferenceGameObject prefab; }
    [Serializable] public sealed class UnitIconEntry { public int id; public Sprite sprite; }
    [CreateAssetMenu(menuName="PixelTactics/Asset Catalog")]
    public sealed class AssetCatalog : ScriptableObject
    {
        [SerializeField] private AssetReferenceT<TextAsset> snapshot;
        public AssetReferenceT<TextAsset> Snapshot => snapshot;
        public CatalogEntry[] visuals;
        [Header("시트 icon_id에 연결할 유닛 아이콘")]
        public UnitIconEntry[] icons = Array.Empty<UnitIconEntry>();

        // 등록되지 않은 아이콘은 상점 카드의 기본 표시 사용.
        public Sprite Icon(int id)
        {
            if(icons==null) return null;
            foreach(var entry in icons)
                if(entry!=null && entry.id==id) return entry.sprite;
            return null;
        }
        public AssetReferenceGameObject VisualPrefab(int id)
        {
            foreach(var entry in visuals) if(entry.id==id) return entry.prefab;
            throw new ArgumentException($"미등록 visual_id: {id}");
        }
#if UNITY_EDITOR
        public void ConfigureReferences(AssetReferenceT<TextAsset> snapshotReference, CatalogEntry[] entries)
        {
            snapshot = snapshotReference;
            visuals = entries;
        }
#endif
    }
}
