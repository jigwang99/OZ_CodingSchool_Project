using System;
using System.Collections.Generic;
using System.Linq;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Skills;
using UnityEngine;

namespace AutoBattler.Battle.Skills
{
    /// <summary>팀·육각 범위·직선·전방 부채꼴에 맞는 스킬 대상 집계.</summary>
    public static class SkillAreaSelector
    {
        public static SkillTarget[] Select(SkillCastContext context,HexCoord origin,UnitTeam team)
        {
            SkillDefinition skill=context.Definition;
            IEnumerable<Unit> alive=context.CurrentParticipants.Where(unit=>unit!=null && unit.IsInCombat && unit.Health.IsAlive
                && unit.CurrentCell.HasValue).Distinct();
            if(skill.TargetRule==SkillTargetRule.NearestOtherAllies)
                return Limit(alive.Where(unit=>unit!=context.Caster && unit.Team==team
                    && origin.DistanceTo(unit.CurrentCell.Value)<=skill.CastRange)
                    .OrderBy(unit=>origin.DistanceTo(unit.CurrentCell.Value)).ThenBy(unit=>unit.InstanceId),skill.TargetCount);
            if(skill.Shape==SkillShape.Line)
            {
                HashSet<HexCoord> cells=Line(origin,context.Center,skill.CastRange);
                return Limit(alive.Where(unit=>unit.Team!=team && cells.Contains(unit.CurrentCell.Value))
                    .OrderBy(unit=>origin.DistanceTo(unit.CurrentCell.Value)).ThenBy(unit=>unit.InstanceId),skill.TargetCount);
            }
            if(skill.Shape==SkillShape.FrontArc)
            {
                Vector2 facing=Direction(origin,context.Center).normalized;
                return Limit(alive.Where(unit=>unit.Team!=team && origin.DistanceTo(unit.CurrentCell.Value)<=skill.Radius
                    && Vector2.Dot(facing,Direction(origin,unit.CurrentCell.Value).normalized)>=.5f-.0001f)
                    .OrderBy(unit=>origin.DistanceTo(unit.CurrentCell.Value)).ThenBy(unit=>unit.InstanceId),skill.TargetCount);
            }
            if(skill.Radius>0)
            {
                bool hostile=skill.TargetRule==SkillTargetRule.CurrentEnemy;
                return Limit(alive.Where(unit=>(hostile?unit.Team!=team:unit.Team==team)
                    && unit.CurrentCell.Value.DistanceTo(context.Center)<=skill.Radius).OrderBy(unit=>unit.InstanceId),skill.TargetCount);
            }
            return new[]{context.Target};
        }
        public static SkillTarget[] EnemiesAround(SkillCastContext context,HexCoord origin,UnitTeam team,int radius)
            =>context.CurrentParticipants.Where(unit=>unit!=null && unit.IsInCombat && unit.Health.IsAlive && unit.Team!=team
                && unit.CurrentCell.HasValue && origin.DistanceTo(unit.CurrentCell.Value)<=radius).Distinct()
                .OrderBy(unit=>unit.InstanceId).Select(unit=>new SkillTarget(unit)).ToArray();
        private static SkillTarget[] Limit(IEnumerable<Unit> targets,int count)
            =>(count>0?targets.Take(count):targets).Select(unit=>new SkillTarget(unit)).ToArray();
        private static Vector2 Direction(HexCoord from,HexCoord to)
        {
            int q=to.Q-from.Q,r=to.R-from.R;
            return new Vector2(1.5f*q,Mathf.Sqrt(3f)*(r+q*.5f));
        }
        private static HashSet<HexCoord> Line(HexCoord from,HexCoord to,int range)
        {
            HashSet<HexCoord> cells=new HashSet<HexCoord>();
            int distance=from.DistanceTo(to);
            if(distance==0) return cells;
            for(int step=1;step<=range;step++)
            {
                double scale=(double)step/distance;
                double q=from.Q+(to.Q-from.Q)*scale,r=from.R+(to.R-from.R)*scale,s=-q-r;
                int iq=(int)Math.Round(q),ir=(int)Math.Round(r),isValue=(int)Math.Round(s);
                double dq=Math.Abs(iq-q),dr=Math.Abs(ir-r),ds=Math.Abs(isValue-s);
                if(dq>dr && dq>ds) iq=-ir-isValue;
                else if(dr>ds) ir=-iq-isValue;
                cells.Add(new HexCoord(iq,ir));
            }
            cells.Remove(from); return cells;
        }
    }
}
