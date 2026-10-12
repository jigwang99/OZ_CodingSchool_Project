using System;
using UnityEngine;

namespace AutoBattler.Assets
{
    [Serializable] public sealed class CatalogEntry { public int id; public string address; }
    [CreateAssetMenu(menuName="PixelTactics/Asset Catalog")]
    public sealed class AssetCatalog : ScriptableObject
    {
        public string snapshotAddress="Data/GameSnapshot";
        public CatalogEntry[] visuals;
        public string VisualAddress(int id)
        {
            foreach(var entry in visuals) if(entry.id==id) return entry.address;
            throw new ArgumentException($"미등록 visual_id: {id}");
        }
    }
}
