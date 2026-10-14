using System.Linq;
using System.Collections.Generic;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Skills;

namespace AutoBattler.Battle.Skills
{
    public static class SkillTargetSelector
    {
        public static bool IsEligible(Unit caster,SkillDefinition skill,Unit target)
        {
            if(target==null || !target.IsInCombat || !target.Health.IsAlive || !target.CurrentCell.HasValue
                || !caster.CurrentCell.HasValue) return false;
            if(caster.CurrentCell.Value.DistanceTo(target.CurrentCell.Value)>skill.CastRange) return false;
            switch(skill.TargetRule)
            {
                case SkillTargetRule.Self:return target==caster;
                case SkillTargetRule.LowestHealthAlly:return target.Team==caster.Team && target.Health.CurrentHp<target.Health.MaxHp;
                default:return target.Team!=caster.Team;
            }
        }
        public static Unit Select(Unit caster,SkillDefinition skill,IReadOnlyList<Unit> participants,Unit preferred=null)
        {
            if(skill.TargetRule==SkillTargetRule.Self) return IsEligible(caster,skill,caster)?caster:null;
            if(skill.TargetRule==SkillTargetRule.CurrentEnemy && IsEligible(caster,skill,preferred)) return preferred;
            var candidates=participants.Where(target=>IsEligible(caster,skill,target));
            if(skill.TargetRule==SkillTargetRule.LowestHealthAlly)
                return candidates.OrderBy(target=>(double)target.Health.CurrentHp/target.Health.MaxHp).ThenBy(target=>target.InstanceId).FirstOrDefault();
            return candidates.OrderBy(target=>caster.CurrentCell.Value.DistanceTo(target.CurrentCell.Value))
                .ThenBy(target=>target.Definition.Role).ThenBy(target=>target.InstanceId).FirstOrDefault();
        }
    }
}
