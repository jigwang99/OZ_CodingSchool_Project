using System.Globalization;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Augments;
using AutoBattler.Data.Snapshot;
using AutoBattler.Data.Traits;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>기존 데이터 ID와 수치를 유지하는 전투 화면 한국어 표시.</summary>
    public static class BattleText
    {
        public static string UnitName(UnitDefinition unit)=>unit?.Name ?? string.Empty;
        public static string TraitName(TraitCategory category,int id,string fallback)
        {
            if(category==TraitCategory.Origin)
                switch(id) {case 1:return "인간";case 2:return "엘프";case 3:return "언데드";case 4:return "오크";case 5:return "수인";case 6:return "유령";case 7:return "악마";}
            else
                switch(id) {case 1:return "검사";case 2:return "마법사";case 3:return "궁수";case 4:return "암살자";case 5:return "격투가";case 6:return "창병";}
            return fallback;
        }
        public static string AugmentName(AugmentDefinition definition)
        {
            switch(definition.Id)
            {
                case 5001:return "강철 체력";case 5002:return "전투 훈련";case 5003:return "날렵한 손놀림";case 5004:return "비전 연구";
                case 5005:return "선제 보호";case 5006:return "용병 계약";case 5007:return "전리품 수집";case 5008:return "마나 충전";
                default:return definition.Name;
            }
        }
        public static string StatName(StatType stat)
        {
            switch(stat)
            {
                case StatType.MaxHp:return "최대 체력";case StatType.AttackDamage:return "공격력";case StatType.Defense:return "방어력";
                case StatType.AttackSpeed:return "공격 속도";case StatType.MoveSpeed:return "이동 속도";
                case StatType.SkillPower:return "스킬 위력";case StatType.CritChance:return "치명타 확률";
                case StatType.CritMultiplier:return "치명타 배율";case StatType.BasicAttackPower:return "기본 공격 피해";
                case StatType.Mana:return "MP";case StatType.BasicArmorIgnore:return "기본 공격 방어 관통";
                case StatType.DamageReduction:return "받는 피해 감소";
                default:return string.Empty;
            }
        }
        public static string AugmentDescription(AugmentDefinition definition)
        {
            string value=definition.Value.ToString("0.#",CultureInfo.InvariantCulture);
            string percent=(definition.Value*100f).ToString("0.#",CultureInfo.InvariantCulture);
            string secondary=definition.SecondaryValue.ToString("0.#",CultureInfo.InvariantCulture);
            string secondaryPercent=(definition.SecondaryValue*100f).ToString("0.#",CultureInfo.InvariantCulture);
            switch(definition.Effect)
            {
                case AugmentEffect.StatModifier:
                    return $"아군의 {StatName(definition.Stat)} +{(definition.Operation==ModifierOperation.Percent?percent+"%":value)}";
                case AugmentEffect.StartingShield:
                    return $"전투 시작 시 아군에게 최대 체력의 {percent}% 보호막 부여\n{definition.Duration:0.#}초 지속";
                case AugmentEffect.StartingMana:return $"아군 시작 마나 +{value} (최대 100)";
                case AugmentEffect.ImmediateGold:return $"즉시 {CurrencyText.Amount((int)definition.Value)} 획득";
                case AugmentEffect.VictoryGold:return $"전투 승리 시 추가 {CurrencyText.Amount((int)definition.Value)} 획득";
                case AugmentEffect.TrainingDummy:
                    return $"전투 시작 시 허수아비 소환\n체력 {value} + 레벨당 {secondary}\n방어력 20, 이동·공격 불가\n배치 인원·시너지 제외";
                case AugmentEffect.IsolatedShield:
                    return $"인접 아군 없이 전투 시작 시\n최대 체력의 {percent}% 보호막";
                case AugmentEffect.Comrades:
                    return $"인접 아군이 정확히 1기일 때\n받는 피해 {percent}% 감소\n전투 시작 시 판정";
                case AugmentEffect.EliteArmy:
                    return $"배치 한도보다 적게 배치 시\n아군 체력·공격력 +{percent}%";
                case AugmentEffect.LastStand:
                    return $"아군이 1기만 남으면\n최대 체력의 {secondaryPercent}% 보호막·공격 속도 +{percent}%";
                case AugmentEffect.HealOnKill:
                    return $"적 처치 시 최대 체력의 {percent}% 회복\nMP {secondary} 획득";
                case AugmentEffect.EmergencyShield:
                    return $"체력 {definition.Threshold*100f:0.#}% 이하 피격 시\n최대 체력의 {percent}% 보호막\n{definition.Duration:0.#}초 지속·유닛당 전투 1회";
                case AugmentEffect.ManaCycle:return $"스킬 발동 시 MP {value} 회복";
                case AugmentEffect.EchoSpell:
                    return $"첫 스킬을 {definition.Duration:0.#}초 뒤 추가 발동\n피해·회복·보호막 위력 {percent}%\nMP 소모 없음";
                case AugmentEffect.Unarmed:
                    return $"장비 없는 아군의\n공격력·공격 속도 +{percent}%\n장비 장착 시 해제";
                case AugmentEffect.FullyEquipped:
                    return $"장비 3개 장착 시\n최대 체력의 {percent}% 보호막\n전투 중 장착도 적용·전투 1회";
                case AugmentEffect.MaterialSupply:return $"즉시 무작위 하위 아이템 {value}개 획득";
                case AugmentEffect.ActiveTraitPower:
                    return $"활성 시너지 1개당\n아군 스킬 위력 +{percent}%";
                default:return definition.Description;
            }
        }
    }
}
