using System.Linq;
using System.Globalization;
using AutoBattler.Battle.Traits;
using AutoBattler.Data.Traits;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>시너지 한 그룹의 종류 수·최고 단계·효과 표시.</summary>
    [DisallowMultipleComponent]
    public sealed class TraitRowView : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        [SerializeField] private TraitCategory category;
        [SerializeField] private int groupId;
        [SerializeField] private Image background;
        [SerializeField] private Image indicator;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private TextMeshProUGUI effectText;
        [SerializeField] private SynergyPanel panel;
        [SerializeField] private Color inactiveColor=new Color(.12f,.12f,.12f,1f);
        [SerializeField] private Color activeColor=new Color(.08f,.23f,.19f,1f);
        [SerializeField] private Color inactiveAccent=new Color(.6f,.6f,.6f,1f);
        [SerializeField] private Color activeAccent=new Color(.45f,.94f,.72f,1f);
        [SerializeField,Min(0f)] private float tweenDuration=.15f;
        private bool hasShown;
        public TraitCategory Category=>category;
        public int GroupId=>groupId;
        public int Count { get; private set; }
        public int RequiredCount { get; private set; }
        public int DefinitionId { get; private set; }
        public bool IsActive { get; private set; }
        public TextMeshProUGUI NameText=>nameText;
        public TextMeshProUGUI CountText=>countText;
        public TextMeshProUGUI EffectText=>effectText;
        public Image Background=>background;
        public string TooltipDescription { get; private set; }=string.Empty;
        public void Show(TraitSnapshot snapshot)
        {
            TraitStatus? unlocked=null,locked=null;
            foreach(TraitStatus status in snapshot.Statuses)
            {
                TraitDefinition definition=status.Definition;
                if(definition.Category!=category || definition.GroupId!=groupId) continue;
                if(status.IsActive && (!unlocked.HasValue || definition.RequiredCount>unlocked.Value.Definition.RequiredCount)) unlocked=status;
                if(!locked.HasValue || definition.RequiredCount<locked.Value.Definition.RequiredCount) locked=status;
            }
            TraitStatus[] stages=snapshot.Statuses.Where(status=>status.Definition.Category==category && status.Definition.GroupId==groupId)
                .OrderBy(status=>status.Definition.RequiredCount).ToArray();
            TooltipDescription=string.Join("\n",stages.Select(status=>
                $"<color={(unlocked.HasValue && status.Definition.Id==unlocked.Value.Definition.Id ? "#73F0B8" : "#BFBFBF")}>({status.Definition.RequiredCount}) {Describe(status.Definition)}</color>"));
            TraitStatus? selected=unlocked ?? locked;
            if(!selected.HasValue)
            {
                Count=RequiredCount=DefinitionId=0; IsActive=false;
                gameObject.SetActive(false); return;
            }
            TraitStatus value=selected.Value; bool animate=hasShown && IsActive!=value.IsActive;
            Count=value.Count; RequiredCount=value.Definition.RequiredCount; DefinitionId=value.Definition.Id; IsActive=value.IsActive;
            // 배치 종류가 없는 그룹은 숨김. 재배치 시 현재 수치로 복원.
            if(Count==0) { gameObject.SetActive(false); return; }
            gameObject.SetActive(true);
            nameText.text=BattleText.TraitName(category,groupId,value.Definition.Name);
            // 충족한 단계에서는 현재 단계, 중간 수에서는 다음 목표 단계 표시.
            int target=stages.Where(status=>status.Definition.RequiredCount>=Count)
                .Select(status=>status.Definition.RequiredCount).DefaultIfEmpty(stages.Last().Definition.RequiredCount).First();
            countText.text=$"{Count}/{target}";
            effectText.text=Describe(value.Definition);
            effectText.gameObject.SetActive(false);
            StopAnimation();
            Color panelColor=IsActive?activeColor:inactiveColor; Color accent=IsActive?activeAccent:inactiveAccent;
            if(animate)
            {
                background.DOColor(panelColor,tweenDuration).SetUpdate(true);
                indicator.DOColor(accent,tweenDuration).SetUpdate(true);
                countText.DOColor(accent,tweenDuration).SetUpdate(true);
            }
            else { background.color=panelColor; indicator.color=accent; countText.color=accent; }
            nameText.color=IsActive?activeAccent:new Color(.75f,.75f,.75f,1f);
            effectText.color=IsActive?new Color(.71f,.85f,.77f,1f):inactiveAccent;
            hasShown=true;
            if(panel!=null) panel.RefreshTooltip(this);
        }
        public void OnPointerEnter(PointerEventData eventData)
        { if(panel!=null) panel.ShowTooltip(this); }
        public void OnPointerExit(PointerEventData eventData)
        { if(panel!=null) panel.HideTooltip(this); }
        private static string Number(float value)=>value.ToString("0.#",CultureInfo.InvariantCulture);
        private static string Percent(float value)=>Number(value*100f)+"%";
        private static string Describe(TraitDefinition trait)
        {
            switch(trait.Effect)
            {
                case TraitEffect.ShieldAtStart: return $"최대 체력의 {Percent(trait.Value)} 보호막 ({Number(trait.Duration)}초)";
                case TraitEffect.StartingMana: return $"시작 마나 +{Number(trait.Value)}";
                case TraitEffect.HealOnKill: return $"처치 시 최대 체력의 {Percent(trait.Value)} 회복";
                case TraitEffect.LowHealthAttack: return $"체력 {Percent(trait.Threshold)} 이하 시 공격력 +{Percent(trait.Value)}";
                case TraitEffect.BasicAttackPower: return $"기본 공격 피해 +{Percent(trait.Value)}";
                case TraitEffect.SkillPower: return $"스킬 피해·회복·보호막 +{Percent(trait.Value)}";
                case TraitEffect.AttackSpeed: return $"공격 속도 +{Percent(trait.Value)}";
                case TraitEffect.Critical: return $"치명타 확률 +{Number(trait.Value*100f)}%p, 치명타 피해량 +{Percent(trait.SecondaryValue)}";
                case TraitEffect.MaxHealth: return $"최대 체력 +{Percent(trait.Value)}";
                case TraitEffect.DamageLifeSteal: return $"가한 체력 피해의 {Percent(trait.Value)} 회복";
                case TraitEffect.DemonPower: return $"체력·공격력·공격 속도 {(trait.Value<0?"-":"+")}{Percent(Mathf.Abs(trait.Value))}";
                case TraitEffect.BasicArmorIgnore: return $"기본 공격 방어력 {Percent(trait.Value)} 무시";
                default: return "";
            }
        }
        private void StopAnimation() { background.DOKill(); indicator.DOKill(); countText.DOKill(); }
        private void OnDisable()
        { if(panel!=null) panel.HideTooltip(this); if(background!=null) StopAnimation(); hasShown=false; }
    }
}
