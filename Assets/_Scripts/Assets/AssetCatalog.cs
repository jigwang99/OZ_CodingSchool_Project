using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AutoBattler.Assets
{
    [Serializable] public sealed class CatalogEntry { public int id; public AssetReferenceGameObject prefab; }
    [Serializable] public sealed class ItemIconEntry { public int id; public AssetReferenceSprite sprite; }
    [Serializable] public sealed class UnitIconEntry { public int id; public Sprite sprite; }
    [CreateAssetMenu(menuName="PixelTactics/Asset Catalog")]
    public sealed class AssetCatalog : ScriptableObject
    {
        [SerializeField] private AssetReferenceT<TextAsset> snapshot;
        public AssetReferenceT<TextAsset> Snapshot => snapshot;
        public CatalogEntry[] visuals;
        [SerializeField] private ItemIconEntry[] itemIcons=Array.Empty<ItemIconEntry>();
        public IReadOnlyList<ItemIconEntry> ItemIcons=>Array.AsReadOnly(itemIcons);
        public AssetReferenceSprite ItemIconReference(int id)
        { foreach(ItemIconEntry entry in itemIcons) if(entry.id==id) return entry.sprite; throw new ArgumentException($"미등록 아이템 icon_id: {id}"); }
        [SerializeField] private AutoBattler.Presentation.Units.BattleVisualSettings battleVisuals;
        public AutoBattler.Presentation.Units.BattleVisualSettings BattleVisuals=>battleVisuals;
        [Header("시트 icon_id에 연결할 유닛 아이콘")]
        public UnitIconEntry[] icons = Array.Empty<UnitIconEntry>();

        // 등록되지 않은 아이콘은 상점 카드의 기본 표시 사용.
        public Sprite Icon(int id)
        {
            if(icons==null) return null;
            foreach(UnitIconEntry entry in icons)
                if(entry!=null && entry.id==id) return entry.sprite;
            return null;
        }
        public AssetReferenceGameObject VisualPrefab(int id)
        {
            foreach(CatalogEntry entry in visuals) if(entry.id==id) return entry.prefab;
            throw new ArgumentException($"미등록 visual_id: {id}");
        }
    }
}
