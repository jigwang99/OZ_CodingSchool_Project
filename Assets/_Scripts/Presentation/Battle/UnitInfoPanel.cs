using System.Linq;
using AutoBattler.Run;
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
        [SerializeField] private ScrollRect skillScroll;
        [SerializeField] private AutoBattler.Assets.AssetCatalog catalog;
        [SerializeField] private Image unitIcon,skillIcon,shieldIcon;
        public Image UnitIcon=>unitIcon;
        public Image SkillIcon=>skillIcon;
        [Header("장착 아이템")]
        [SerializeField] private ItemIconButton[] equipmentSlots = new ItemIconButton[3];
        private RunController run;
        public ItemIconButton[] EquipmentSlots => equipmentSlots;
        public void BindRun(RunController controller)
        {
            if (run != null) run.DisplayChanged -= RefreshEquipment;
            run = controller;
            if (run != null) run.DisplayChanged += RefreshEquipment;
        }
        private void Awake()
        { foreach (ItemIconButton slot in equipmentSlots) slot.RightPressed += Unequip; }
        private void Unequip(int itemId)
        {
            Unit unit = boundUnit;
            if (unit != null && run != null && run.UnequipItem(itemId))
            { run.Drag.Inspect(unit); RefreshEquipment(); }
        }
        private void RefreshEquipment()
        {
            for (int i = 0; i < equipmentSlots.Length; i++)
            {
                OwnedItem item = IsCurrent() && run != null && run.IsReady && boundUnit.Team == UnitTeam.Ally
                    ? run.State.Items.Equipped(boundUnit.InstanceId).FirstOrDefault(owned => owned.Slot == i) : null;
                AutoBattler.Data.Items.ItemDefinition definition = item != null ? run.State.Data.Items[item.DefinitionId] : null;
                equipmentSlots[i].Show(item?.InstanceId ?? 0, boundUnit != null ? boundUnit.Equipment.Icon(i) : null,
                    definition?.Name ?? "빈 슬롯", false, run != null && run.Equipment.CanUnequip);
            }
        }
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
        public ScrollRect SkillScroll=>skillScroll;
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
            unit.Equipment.Changed += RefreshEquipment;
            gameObject.SetActive(true);
            nameText.text=$"{BattleText.UnitName(unit.Definition)} <color=#FFD159>{new string('★', unit.Star)}</color>";
            identityText.text=CurrencyText.Amount(UnitStarRules.SalePrice(unit.Definition.Cost,unit.Star));
            traitsText.text=$"{TraitName(TraitCategory.Origin, unit.Definition.OriginId)} / {TraitName(TraitCategory.Class, unit.Definition.ClassId)}";
            teamAccent.color=unit.Team==UnitTeam.Ally ? new Color(.2f,.85f,.85f) : new Color(1f,.35f,.4f);
            if(unitIcon!=null) { unitIcon.sprite=catalog.Icon(unit.Definition.IconId); unitIcon.enabled=unitIcon.sprite!=null; }
            if(skillIcon!=null) { skillIcon.sprite=catalog.BattleVisuals.Skill(unit.Skill.Definition?.Id ?? 0)?.icon; skillIcon.enabled=skillIcon.sprite!=null; }
            RefreshDetails();
            skillScroll.verticalNormalizedPosition=1f;
            RefreshEquipment();
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
            RefreshEquipment();
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
            float starScale=data!=null?UnitStarRules.StatMultiplier(boundUnit.Star,data.Rule(EconomyRule.StarMultiplier)):1f;
            skillText.text=SkillDescriptionText.Describe(skill,data,stats.AttackDamage,stats.MaxHp,starScale,boundUnit.StatModifiers.SkillPower);
            float height=skillText.GetPreferredValues(skillText.text,skillScroll.viewport.rect.width,0f).y;
            skillText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
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
                boundUnit.Equipment.Changed -= RefreshEquipment;
            }
            boundUnit=null; data=null;
            if(healthSegments!=null) healthSegments.Clear();
            if (healthFill!=null) healthFill.transform.DOKill();
            if (manaFill!=null) manaFill.transform.DOKill();
        }
        private void OnDestroy()
        { BindRun(null); foreach (ItemIconButton slot in equipmentSlots) slot.RightPressed -= Unequip; }
        private void OnDisable() { Unbind(); if(group!=null) group.DOKill(); }
    }
}
