using System.Text;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Skills;
using AutoBattler.Data.Snapshot;
using AutoBattler.Data.Traits;
using AutoBattler.Data.Units;
using AutoBattler.Presentation.Units;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>선택한 기물의 현재 능력치와 스킬을 공용 HUD에 표시.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitInfoPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image teamAccent;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI identityText;
        [SerializeField] private TextMeshProUGUI traitsText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI manaText;
        [SerializeField] private Image healthFill;
        [SerializeField] private HealthBarFill healthSegments;
        [SerializeField] private Image manaFill;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private TextMeshProUGUI skillNameText;
        [SerializeField] private TextMeshProUGUI skillText;
        private Unit boundUnit;
        private DataRepository data;
        private int spawnVersion;
        public Unit BoundUnit => boundUnit;
        public TextMeshProUGUI NameText => nameText;
        public TextMeshProUGUI IdentityText => identityText;
        public TextMeshProUGUI HealthText => healthText;
        public TextMeshProUGUI ManaText => manaText;
        public TextMeshProUGUI StatsText => statsText;
        public TextMeshProUGUI SkillText => skillText;
        public Image HealthFill => healthFill;
        public Image ManaFill => manaFill;
        public HealthBarFill HealthSegments=>healthSegments;
#if UNITY_EDITOR
        public void Configure(CanvasGroup canvasGroup, Image accent, TextMeshProUGUI title,
            TextMeshProUGUI identity, TextMeshProUGUI traits, TextMeshProUGUI hp, TextMeshProUGUI mp,
            Image hpFill, Image mpFill, TextMeshProUGUI stats, TextMeshProUGUI skillName, TextMeshProUGUI skill)
        {
            group=canvasGroup; teamAccent=accent; nameText=title; identityText=identity; traitsText=traits;
            healthText=hp; manaText=mp; healthFill=hpFill; manaFill=mpFill;
            statsText=stats; skillNameText=skillName; skillText=skill;
        }
        public void ConfigureHealthSegments(HealthBarFill view)=>healthSegments=view;
#endif
        public void Show(Unit unit, DataRepository repository)
        {
            if (unit == null || !unit.isActiveAndEnabled || unit.Definition == null || unit.Stats == null)
            { Hide(); return; }
            if (boundUnit == unit && spawnVersion == unit.SpawnVersion && ReferenceEquals(data, repository)
                && gameObject.activeSelf) return;
            Unbind();
            boundUnit=unit; data=repository; spawnVersion=unit.SpawnVersion;
            unit.Health.HealthChanged+=OnHealthChanged;
            unit.Mana.Changed+=OnManaChanged;
            unit.StatModifiers.Changed+=RefreshDetails;
            unit.Effects.Changed+=RefreshDetails;
            gameObject.SetActive(true);
            nameText.text=unit.Definition.Name;
            identityText.text=$"{unit.Team}   {new string('*', unit.Star)}   Cost {unit.Definition.Cost}G";
            traitsText.text=$"{TraitName(TraitCategory.Origin, unit.Definition.OriginId)} / {TraitName(TraitCategory.Class, unit.Definition.ClassId)}"
                +$"   |   {(unit.Definition.IsRanged ? "Ranged" : "Melee")}";
            teamAccent.color=unit.Team==UnitTeam.Ally ? new Color(.2f,.85f,.85f) : new Color(1f,.35f,.4f);
            RefreshDetails();
            RefreshHealth(false);
            SetBar(manaFill, unit.Mana.Current, unit.Mana.Maximum, false);
            group.DOKill(); group.alpha=0f; group.DOFade(1f,.12f).SetUpdate(true);
        }
        private string TraitName(TraitCategory category, int id)
        {
            if (data != null) foreach (var trait in data.Traits)
                if (trait.Category==category && trait.GroupId==id) return trait.Name;
            return category.ToString();
        }
        private void OnHealthChanged(int current, int maximum)
        {
            if (!IsCurrent()) return;
            RefreshHealth(current!=0 && current!=maximum);
        }
        private void RefreshHealth(bool animate)
        {
            int shield=boundUnit.Effects.Shield;
            healthText.text=$"HP  {boundUnit.Health.CurrentHp}/{boundUnit.Health.MaxHp}";
            if(shield>0) healthText.text+=$"  <color=#B8B8B8>+{shield}</color>";
            healthSegments.SetValues(boundUnit.Health.CurrentHp,boundUnit.Health.MaxHp,shield,animate);
        }
        private void OnManaChanged(int current, int maximum)
        {
            if (!IsCurrent()) return;
            manaText.text=$"MP  {current}/{maximum}";
            SetBar(manaFill,current,maximum,true);
        }
        private bool IsCurrent() => boundUnit != null && boundUnit.isActiveAndEnabled
            && boundUnit.SpawnVersion==spawnVersion;
        private static void SetBar(Image image, int current, int maximum, bool animate)
        {
            image.transform.DOKill();
            var scale=new Vector3(maximum>0 ? Mathf.Clamp01((float)current/maximum) : 0f,1f,1f);
            if (animate) image.transform.DOScale(scale,.12f).SetUpdate(true);
            else image.transform.localScale=scale;
        }
        private void RefreshDetails()
        {
            if (!IsCurrent()) return;
            var stats=boundUnit.Stats;
            RefreshHealth(boundUnit.Health.IsAlive);
            manaText.text=$"MP  {boundUnit.Mana.Current}/{boundUnit.Mana.Maximum}";
            statsText.text=$"Attack<pos=205>{stats.AttackDamage}\nArmor<pos=205>{stats.Defense}\n"
                +$"Range<pos=205>{stats.AttackRange} hex\nAttack interval<pos=205>{stats.AttackCooldown:0.##}s\n"
                +$"Critical<pos=205>{boundUnit.StatModifiers.CritChance:P0} / {boundUnit.StatModifiers.CritMultiplier:0.##}x\n"
                +$"Skill power<pos=205>{boundUnit.StatModifiers.SkillPower:P0}\nShield<pos=205>{boundUnit.Effects.Shield}";
            var skill=boundUnit.Skill.Definition;
            skillNameText.text=skill?.Name ?? "No skill";
            if (skill==null) { skillText.text="This unit has no active skill."; return; }
            string target=skill.TargetRule==SkillTargetRule.Self ? "Self"
                : skill.TargetRule==SkillTargetRule.LowestHealthAlly ? "Lowest HP ally" : "Current enemy";
            var description=new StringBuilder();
            description.Append($"{target}  |  {skill.ManaCost} MP\nWindup {skill.Windup:0.##}s  /  Cooldown {skill.Cooldown:0.##}s\n");
            description.Append($"Range {skill.CastRange} hex  /  Radius {skill.Radius} hex\n");
            float starScale=boundUnit.Star==2 && data!=null ? data.Rule(EconomyRule.StarMultiplier) : 1f;
            float power=boundUnit.StatModifiers.SkillPower;
            if (skill.BaseDamage>0 || skill.AttackRatio>0)
            {
                float damage=(skill.BaseDamage*starScale+stats.AttackDamage*skill.AttackRatio)*power;
                description.Append($"Damage {damage:0.#} before armor\n");
            }
            if (skill.ArmorIgnore>0) description.Append($"Ignore armor {skill.ArmorIgnore:P0}\n");
            if (data!=null && data.Effects.TryGetValue(skill.EffectId,out var effect))
            {
                float value=effect.Value;
                if (effect.Kind==EffectKind.Heal || effect.Kind==EffectKind.Shield) value*=starScale*power;
                if (effect.Kind==EffectKind.Slow) description.Append($"Slow {value:P0}");
                else if (effect.Kind==EffectKind.Stun) description.Append("Stun");
                else if (effect.Kind==EffectKind.StatModifier) description.Append($"{effect.Stat} {effect.Operation} {value:0.##}");
                else description.Append($"{effect.Kind} {value:0.#}");
                if (effect.Duration>0) description.Append($" for {effect.Duration:0.##}s");
                description.AppendLine();
            }
            if (skill.Delivery==SkillDelivery.Projectile) description.Append("Projectile");
            skillText.text=description.ToString().TrimEnd();
        }
        public void Hide()
        {
            Unbind();
            if (group!=null) { group.DOKill(); group.alpha=0f; }
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
        private void Unbind()
        {
            if (boundUnit!=null)
            {
                boundUnit.Health.HealthChanged-=OnHealthChanged;
                boundUnit.Mana.Changed-=OnManaChanged;
                boundUnit.StatModifiers.Changed-=RefreshDetails;
                boundUnit.Effects.Changed-=RefreshDetails;
            }
            boundUnit=null; data=null;
            if(healthSegments!=null) healthSegments.Clear();
            if (healthFill!=null) healthFill.transform.DOKill();
            if (manaFill!=null) manaFill.transform.DOKill();
        }
        private void OnDisable() { Unbind(); if(group!=null) group.DOKill(); }
    }
}
