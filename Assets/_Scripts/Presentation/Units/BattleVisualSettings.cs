using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AutoBattler.Presentation.Units
{
    public enum BattleVisualId { Hit, MagicHit, Fire, Heal, Shield, Spin, Projectile, Stun }
    [Serializable] public sealed class BattleVisualEntry
    {
        public BattleVisualId id;
        public AssetReferenceGameObject prefab;
        [Min(1)] public int capacity=16;
        [Min(.001f)] public float scale=.1f;
        [Min(.01f)] public float lifetime=1.2f;
    }
    [Serializable] public sealed class SkillVisualEntry
    {
        public int skillId;
        public Sprite icon;
        public BattleVisualId impact=BattleVisualId.Hit;
        public bool areaImpact;
        public bool magicProjectile;
        public Color projectileColor=Color.white;
    }
    /// <summary>전투 표현의 Addressables 참조·풀 크기·스킬 아이콘 설정.</summary>
    [CreateAssetMenu(menuName="PixelTactics/Battle Visual Settings")]
    public sealed class BattleVisualSettings : ScriptableObject
    {
        [SerializeField] private BattleVisualEntry[] effects=Array.Empty<BattleVisualEntry>();
        [SerializeField] private SkillVisualEntry[] skills=Array.Empty<SkillVisualEntry>();
        [SerializeField] private Sprite arrow;
        [SerializeField] private Sprite magicOrb;
        [SerializeField] private Sprite stun;
        [SerializeField] private Sprite health;
        [SerializeField] private Sprite mana;
        [SerializeField] private Sprite shield;
        public IReadOnlyList<BattleVisualEntry> Effects=>Array.AsReadOnly(effects);
        public Sprite Arrow=>arrow;
        public Sprite MagicOrb=>magicOrb;
        public Sprite Stun=>stun;
        public Sprite Health=>health;
        public Sprite Mana=>mana;
        public Sprite Shield=>shield;
        public SkillVisualEntry Skill(int id)
        { foreach(var entry in skills) if(entry.skillId==id) return entry; return null; }
#if UNITY_EDITOR
        public void Configure(BattleVisualEntry[] entries,SkillVisualEntry[] presentations,Sprite arrowSprite,Sprite orb,
            Sprite stunSprite,Sprite hp,Sprite mp,Sprite shieldSprite)
        { effects=entries; skills=presentations; arrow=arrowSprite; magicOrb=orb; stun=stunSprite; health=hp; mana=mp; shield=shieldSprite; }
#endif
    }
}
