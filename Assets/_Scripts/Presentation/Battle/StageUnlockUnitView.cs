using AutoBattler.Data.Snapshot;
using AutoBattler.Data.Traits;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>해금 유닛 하나의 기본 모습·종족·직업·스킬 표시.</summary>
    public sealed class StageUnlockUnitView : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Image skillIcon;
        [SerializeField] private TextMeshProUGUI nameText,traitsText,skillNameText,skillDescriptionText;
        [SerializeField] private TextMeshProUGUI unlockStatusText;
        [SerializeField] private LayoutElement layout;
        public UnitDefinition Unit { get; private set; }
        public Image Portrait=>portrait;
        public TextMeshProUGUI NameText=>nameText;
        public TextMeshProUGUI TraitsText=>traitsText;
        public TextMeshProUGUI SkillNameText=>skillNameText;
        public TextMeshProUGUI SkillDescriptionText=>skillDescriptionText;
        public TextMeshProUGUI UnlockStatusText=>unlockStatusText;
        public bool IsUnlocked { get; private set; }
        public void SetUnlocked(bool unlocked)
        {
            IsUnlocked=unlocked;unlockStatusText.text=unlocked?"해금":"잠김";
            unlockStatusText.color=unlocked?new Color(.45f,.94f,.72f):new Color(.65f,.65f,.65f);
            portrait.color=unlocked?Color.white:new Color(.45f,.45f,.45f);
        }
        public void Show(UnitDefinition unit,DataRepository data,Sprite icon)
        {
            Unit=unit;portrait.sprite=null;portrait.enabled=false;
            nameText.text=$"{unit.Name}  <size=75%><color=#FFD159>{unit.Cost}G</color></size>";
            traitsText.text=$"{BattleText.TraitName(TraitCategory.Origin,unit.OriginId,"종족")} / {BattleText.TraitName(TraitCategory.Class,unit.ClassId,"직업")}";
            data.Skills.TryGetValue(unit.SkillId,out AutoBattler.Data.Skills.SkillDefinition skill);
            skillNameText.text=skill?.Name ?? "스킬 없음";
            skillIcon.sprite=icon;skillIcon.enabled=icon!=null;
            skillDescriptionText.text=SkillPreviewText.Describe(unit,skill,data);
            float height=skillDescriptionText.GetPreferredValues(skillDescriptionText.text,skillDescriptionText.rectTransform.rect.width,0f).y;
            skillDescriptionText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            layout.preferredHeight=Mathf.Max(240f,166f+height+16f);
            gameObject.SetActive(true);
        }
        public void SetPortrait(Sprite sprite) {portrait.sprite=sprite;portrait.enabled=sprite!=null;}
        public void Clear()
        {
            Unit=null;portrait.sprite=null;portrait.enabled=false;
            skillIcon.sprite=null;skillIcon.enabled=false;
            gameObject.SetActive(false);
        }
    }
}
