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
        [SerializeField] private AutoBattler.Assets.AssetCatalog catalog;
        [SerializeField] private Image unitIcon,skillIcon,shieldIcon;
        public Image UnitIcon=>unitIcon;
        public Image SkillIcon=>skillIcon;
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
        public TextMeshProUGUI SkillNameText=>skillNameText;
        public Image HealthFill => healthFill;
        public Image ManaFill => manaFill;
        public HealthBarFill HealthSegments=>healthSegments;
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
            nameText.text=BattleText.UnitName(unit.Definition);
            identityText.text=$"{(unit.Team==UnitTeam.Ally ? "아군" : "적군")}   {new string('*', unit.Star)}   비용 {unit.Definition.Cost}G";
            traitsText.text=$"{TraitName(TraitCategory.Origin, unit.Definition.OriginId)} / {TraitName(TraitCategory.Class, unit.Definition.ClassId)}"
                +$"   |   {(unit.Definition.IsRanged ? "원거리" : "근거리")}";
            teamAccent.color=unit.Team==UnitTeam.Ally ? new Color(.2f,.85f,.85f) : new Color(1f,.35f,.4f);
            if(unitIcon!=null) { unitIcon.sprite=catalog.Icon(unit.Definition.IconId); unitIcon.enabled=unitIcon.sprite!=null; }
            if(skillIcon!=null) { skillIcon.sprite=catalog.BattleVisuals.Skill(unit.Skill.Definition?.Id ?? 0)?.icon; skillIcon.enabled=skillIcon.sprite!=null; }
            RefreshDetails();
            RefreshHealth(false);
            SetBar(manaFill, unit.Mana.Current, unit.Mana.Maximum, false);
            group.DOKill(); group.alpha=0f; group.DOFade(1f,.12f).SetUpdate(true);
        }
        private string TraitName(TraitCategory category, int id)
        {
            if (data != null) foreach (TraitDefinition trait in data.Traits)
                if (trait.Category==category && trait.GroupId==id) return BattleText.TraitName(category,id,trait.Name);
            return BattleText.TraitName(category,id,category==TraitCategory.Origin ? "종족" : "직업");
        }
        private void OnHealthChanged(int current, int maximum)
        {
            if (!IsCurrent()) return;
            RefreshHealth(current!=0 && current!=maximum);
        }
        private void RefreshHealth(bool animate)
        {
            int shield=boundUnit.Effects.Shield;
            if(shieldIcon!=null) shieldIcon.enabled=shield>0;
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
            Vector3 scale=new Vector3(maximum>0 ? Mathf.Clamp01((float)current/maximum) : 0f,1f,1f);
            if (animate) image.transform.DOScale(scale,.12f).SetUpdate(true);
            else image.transform.localScale=scale;
        }
        private void RefreshDetails()
        {
            if (!IsCurrent()) return;
            UnitStat stats=boundUnit.Stats;
            RefreshHealth(boundUnit.Health.IsAlive);
            manaText.text=$"MP  {boundUnit.Mana.Current}/{boundUnit.Mana.Maximum}";
            statsText.text=$"공격력<pos=205>{stats.AttackDamage}\n방어력<pos=205>{stats.Defense}\n"
                +$"사거리<pos=205>{stats.AttackRange}칸\n공격 간격<pos=205>{stats.AttackCooldown:0.##}초\n"
                +$"치명타<pos=205>{boundUnit.StatModifiers.CritChance:P0} / {boundUnit.StatModifiers.CritMultiplier:0.##}배\n"
                +$"스킬 위력<pos=205>{boundUnit.StatModifiers.SkillPower:P0}\n보호막<pos=205>{boundUnit.Effects.Shield}";
            SkillDefinition skill=boundUnit.Skill.Definition;
            skillNameText.text=skill?.Name ?? "스킬 없음";
            if (skill==null) { skillText.text=""; return; }
            string target=skill.TargetRule==SkillTargetRule.Self ? "자신"
                : skill.TargetRule==SkillTargetRule.LowestHealthAlly ? "체력이 가장 낮은 아군" : "현재 공격 대상";
            StringBuilder description=new StringBuilder();
            description.Append($"{target}  |  {skill.ManaCost} MP\n시전 준비 {skill.Windup:0.##}초  /  재사용 {skill.Cooldown:0.##}초\n");
            description.Append($"사거리 {skill.CastRange}칸  /  반경 {skill.Radius}칸\n");
            float starScale=boundUnit.Star==2 && data!=null ? data.Rule(EconomyRule.StarMultiplier) : 1f;
            float power=boundUnit.StatModifiers.SkillPower;
            if (skill.BaseDamage>0 || skill.AttackRatio>0)
            {
                float damage=(skill.BaseDamage*starScale+stats.AttackDamage*skill.AttackRatio)*power;
                description.Append($"피해량 {damage:0.#} (방어력 적용 전)\n");
            }
            if (skill.ArmorIgnore>0) description.Append($"방어력 무시 {skill.ArmorIgnore:P0}\n");
            if (data!=null && data.Effects.TryGetValue(skill.EffectId,out EffectDefinition effect))
            {
                float value=effect.Value;
                if (effect.Kind==EffectKind.Heal || effect.Kind==EffectKind.Shield) value*=starScale*power;
                if (effect.Kind==EffectKind.Slow) description.Append($"둔화 {value:P0}");
                else if (effect.Kind==EffectKind.Stun) description.Append("기절");
                else if (effect.Kind==EffectKind.StatModifier) description.Append($"{StatName(effect.Stat)} {(effect.Operation==ModifierOperation.Flat ? "고정" : "비율")} {value:0.##}");
                else description.Append($"{(effect.Kind==EffectKind.Heal ? "체력 회복" : "보호막")} {value:0.#}");
                if (effect.Duration>0) description.Append($" ({effect.Duration:0.##}초)");
                description.AppendLine();
            }
            if (skill.Delivery==SkillDelivery.Projectile) description.Append("투사체");
            skillText.text=description.ToString().TrimEnd();
        }
        private static string StatName(StatType stat) => stat switch
        {
            StatType.MaxHp => "최대 체력",
            StatType.AttackDamage => "공격력",
            StatType.Defense => "방어력",
            StatType.AttackSpeed => "공격 속도",
            StatType.MoveSpeed => "이동 속도",
            StatType.SkillPower => "스킬 위력",
            StatType.CritChance => "치명타 확률",
            StatType.CritMultiplier => "치명타 피해",
            StatType.BasicAttackPower => "기본 공격 위력",
            _ => "능력치"
        };
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
