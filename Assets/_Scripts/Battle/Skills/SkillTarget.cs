using System.Collections.Generic;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Skills;

namespace AutoBattler.Battle.Skills
{
    /// <summary>풀 재사용과 같은 ID의 재생성을 구별하는 대상 참조.</summary>
    public readonly struct SkillTarget
    {
        public Unit Unit { get; }
        public int InstanceId { get; }
        public int SpawnVersion { get; }
        public bool IsValid=>Unit!=null && Unit.InstanceId==InstanceId && Unit.SpawnVersion==SpawnVersion
            && Unit.IsInCombat && Unit.Health.IsAlive && Unit.CurrentCell.HasValue;
        public SkillTarget(Unit unit)
        { Unit=unit; InstanceId=unit.InstanceId; SpawnVersion=unit.SpawnVersion; }
    }
    public sealed class SkillCastContext
    {
        public Unit Caster { get; }
        public SkillDefinition Definition { get; }
        public SkillTarget Target { get; internal set; }
        public IReadOnlyList<Unit> Participants { get; }
        public HexCoord Center { get; internal set; }
        public SkillCastContext(Unit caster,SkillDefinition definition,SkillTarget target,IReadOnlyList<Unit> participants)
        { Caster=caster; Definition=definition; Target=target; Participants=participants; Center=target.Unit.CurrentCell.Value; }
    }
}
