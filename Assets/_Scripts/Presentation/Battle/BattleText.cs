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
        public static string UnitName(UnitDefinition unit)
        {
            if(unit==null) return string.Empty;
            switch(unit.Id)
            {
                case 1001:return "방패병"; case 1002:return "정찰병"; case 1003:return "치유사"; case 1004:return "석궁병";
                case 1005:return "전사"; case 1006:return "암살자"; case 1007:return "화염술사"; case 1008:return "추적자";
                default:return unit.Name;
            }
        }
        public static string TraitName(TraitCategory category,int id,string fallback)
        {
            if(category==TraitCategory.Origin)
                switch(id) {case 1:return "인간";case 2:return "엘프";case 3:return "언데드";case 4:return "오크";}
            else
                switch(id) {case 1:return "검사";case 2:return "마법사";case 3:return "궁수";case 4:return "암살자";}
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
                default:return string.Empty;
            }
        }
        public static string AugmentDescription(AugmentDefinition definition)
        {
            string value=definition.Value.ToString("0.#",CultureInfo.InvariantCulture);
            string percent=(definition.Value*100f).ToString("0.#",CultureInfo.InvariantCulture);
            switch(definition.Effect)
            {
                case AugmentEffect.StatModifier:
                    return $"아군의 {StatName(definition.Stat)} +{(definition.Operation==ModifierOperation.Percent?percent+"%":value)}";
                case AugmentEffect.StartingShield:
                    return $"전투 시작 시 아군에게 최대 체력의 {percent}% 보호막 부여\n{definition.Duration:0.#}초 지속";
                case AugmentEffect.StartingMana:return $"아군 시작 마나 +{value} (최대 100)";
                case AugmentEffect.ImmediateGold:return $"즉시 {value}골드 획득";
                case AugmentEffect.VictoryGold:return $"전투 승리 시 추가 {value}골드 획득";
                default:return definition.Description;
            }
        }
    }
}
