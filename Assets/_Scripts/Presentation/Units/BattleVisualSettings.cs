using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AutoBattler.Presentation.Units
{
    public enum BattleVisualId
    {
        Hit, MagicHit, Fire, Heal, Shield, Spin, Projectile, Stun,
        Slash, HeavySlash, Pierce, Punch, DarkSlash, HolyBurst, WindSlash, IceBurst, IceBolt, FireBolt,
        NatureBurst, Vine, SpiritBurst, StarBurst, BloodSlash, FlameWave, FireSpin,
        ColdStatus, PowerStatus, WeakStatus, ShieldStatus, SpiritBolt, NatureBolt, Rally, LifeSteal, Lightning, ArcaneBolt
    }
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
        public BattleVisualId projectile=BattleVisualId.Projectile;
        public bool casterCentered;
        [Min(.1f)] public float impactScale=1f;
    }
    [Serializable] public sealed class UnitVisualEntry
    {
        public int unitId;
        public BattleVisualId basicImpact=BattleVisualId.Hit;
        public BattleVisualId projectile=BattleVisualId.Projectile;
        public BattleVisualId healing=BattleVisualId.Heal;
        public bool magicProjectile;
        public Color projectileColor=Color.white;
        [Min(.1f)] public float basicImpactScale=1f;
    }
    /// <summary>전투 표현의 Addressables 참조·풀 크기·스킬 아이콘 설정.</summary>
    [CreateAssetMenu(menuName="PixelTactics/Battle Visual Settings")]
    public sealed class BattleVisualSettings : ScriptableObject
    {
        [SerializeField] private BattleVisualEntry[] effects=Array.Empty<BattleVisualEntry>();
        [SerializeField] private SkillVisualEntry[] skills=Array.Empty<SkillVisualEntry>();
        [SerializeField] private UnitVisualEntry[] units=Array.Empty<UnitVisualEntry>();
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
        public IReadOnlyList<UnitVisualEntry> Units=>Array.AsReadOnly(units);
        public IReadOnlyList<SkillVisualEntry> Skills=>Array.AsReadOnly(skills);
        public UnitVisualEntry Unit(int id)
        { foreach(UnitVisualEntry entry in units) if(entry.unitId==id) return entry; return null; }
        public SkillVisualEntry Skill(int id)
        { foreach(SkillVisualEntry entry in skills) if(entry.skillId==id) return entry; return null; }
    }
}
