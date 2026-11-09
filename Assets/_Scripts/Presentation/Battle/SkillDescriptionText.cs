using System;
using System.Globalization;
using System.Text;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Skills;
using AutoBattler.Data.Snapshot;
using AutoBattler.Data.Units;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>전투 정보와 해금 미리보기에 공통으로 사용하는 문장형 스킬 설명.</summary>
    public static class SkillDescriptionText
    {
        private static string Number(float value)=>value.ToString("0.##",CultureInfo.InvariantCulture);
        private static string Percent(float value)=>Number(value*100f)+"%";
        private static string ObjectParticle(string word)
        {char last=word[word.Length-1];return last>='가' && last<='힣' && (last-'가')%28!=0?"을":"를";}
        public static string Describe(SkillDefinition skill,DataRepository data,float attack,float maxHp,float starScale=1f,float power=1f)
        {
            if(skill==null) return string.Empty;
            string target=Target(skill);
            StringBuilder text=new StringBuilder();
            bool damage=skill.BaseDamage>0 || skill.AttackRatio>0;
            EffectDefinition effect=null;
            if(data!=null) data.Effects.TryGetValue(skill.EffectId,out effect);
            if(damage)
            {
                float value=(skill.BaseDamage*starScale+attack*skill.AttackRatio)*power;
                if(skill.HitCount>1) text.Append($"{target}에게 {Number(skill.HitInterval)}초 간격으로 총 {skill.HitCount}회, 매번 {Number(value)}의 피해를 입힙니다.");
                else text.Append($"{target}에게 {Number(value)}의 피해를 입힙니다.");
            }
            if(effect!=null)
            {
                if(damage) text.AppendLine();
                AppendEffect(text,damage?"적중한 대상":target,effect,EffectValue(effect,skill.ShieldMaxHpRatio,maxHp,starScale,power));
            }
            else if(!damage) text.Append($"{target}에게 스킬을 사용합니다.");
            if(skill.ArmorIgnore>0) text.Append($"\n대상 방어력의 {Percent(skill.ArmorIgnore)}를 무시합니다.");
            if(skill.ExecuteThreshold>0) text.Append($"\n대상의 체력이 {Percent(skill.ExecuteThreshold)} 이하이면 피해량이 {Number(skill.ExecuteMultiplier)}배로 증가합니다.");
            if(skill.DistanceBonus>0) text.Append($"\n대상과의 거리가 1칸을 초과하면, 초과한 한 칸마다 피해량이 {Percent(skill.DistanceBonus)} 증가합니다.");
            if(skill.LifeStealRatio>0) text.Append($"\n실제로 입힌 체력 피해의 {Percent(skill.LifeStealRatio)}만큼 자신의 체력을 회복합니다.");
            if(skill.EffectLowHpThreshold>0) text.Append($"\n자신의 체력이 {Percent(skill.EffectLowHpThreshold)} 이하이면 이 강화 효과가 {Number(skill.EffectLowHpMultiplier)}배로 증가합니다.");
            if(data!=null && data.Effects.TryGetValue(skill.SecondaryEffectId,out EffectDefinition additional))
            {
                text.Append("\n추가로 ");
                string recipients=skill.SecondaryTarget==SecondarySkillTarget.EnemiesAroundCaster?$"자신 주변 {skill.SecondaryRadius}칸 이내의 적":target;
                AppendEffect(text,recipients,additional,EffectValue(additional,0f,maxHp,starScale,power));
            }
            if(skill.RetargetOnKill) text.Append("\n공격 중 대상이 쓰러지면 다른 적을 다시 지정합니다.");
            if(skill.Delivery==SkillDelivery.Projectile) text.Append("\n투사체가 대상에게 도착하면 효과가 적용됩니다.");
            if(skill.Cooldown>0) text.Append($"\n재사용 대기시간은 {Number(skill.Cooldown)}초입니다.");
            return text.ToString();
        }
        private static float EffectValue(EffectDefinition effect,float shieldRatio,float maxHp,float starScale,float power)
            =>effect.Kind==EffectKind.Shield && shieldRatio>0?maxHp*shieldRatio*power
                :effect.Kind==EffectKind.Heal || effect.Kind==EffectKind.Shield?effect.Value*starScale*power:effect.Value;
        private static string Target(SkillDefinition skill)
        {
            string limit=skill.TargetCount>0?$" 최대 {skill.TargetCount}명":" 모두";
            if(skill.Shape==SkillShape.Line) return "일직선상의 적"+limit;
            if(skill.Shape==SkillShape.FrontArc) return $"자신의 전방 {skill.Radius}칸 이내의 적"+limit;
            if(skill.TargetRule==SkillTargetRule.NearestOtherAllies) return "자신을 제외한 가까운 아군"+limit;
            if(skill.Radius>0)
            {
                if(skill.TargetRule==SkillTargetRule.CurrentEnemy)
                    return skill.CenterOnCaster?$"자신 주변 {skill.Radius}칸 이내의 적":$"현재 공격 대상과 주변 {skill.Radius}칸 이내의 적";
                return skill.TargetRule==SkillTargetRule.Self?$"자신과 주변 {skill.Radius}칸 이내의 아군":$"대상과 주변 {skill.Radius}칸 이내의 아군";
            }
            return skill.TargetRule==SkillTargetRule.Self?"자신":skill.TargetRule==SkillTargetRule.LowestHealthAlly?"체력이 가장 낮은 아군":"현재 공격 대상";
        }
        private static void AppendEffect(StringBuilder text,string target,EffectDefinition effect,float value)
        {
            string duration=effect.Duration>0?$"{Number(effect.Duration)}초 동안 ":string.Empty;
            switch(effect.Kind)
            {
                case EffectKind.Shield:text.Append($"{target}에게 {duration}{Number(value)}의 보호막을 부여합니다.");break;
                case EffectKind.Heal:text.Append($"{target}의 체력을 {Number(value)} 회복합니다.");break;
                case EffectKind.Stun:text.Append($"{target}{ObjectParticle(target)} {duration}기절시킵니다.");break;
                case EffectKind.Slow:text.Append($"{target}의 이동 속도를 {duration}{Percent(value)} 감소시킵니다.");break;
                case EffectKind.StatModifier:
                    string stat=effect.Stat==StatType.CritMultiplier?"치명타 피해량":BattleText.StatName(effect.Stat);
                    string amount=effect.Operation==ModifierOperation.Flat && effect.Stat!=StatType.CritMultiplier?Number(Math.Abs(value)):Percent(Math.Abs(value));
                    text.Append($"{target}의 {stat}{ObjectParticle(stat)} {duration}{amount} {(value<0?"감소":"증가")}시킵니다.");break;
            }
        }
    }
}
