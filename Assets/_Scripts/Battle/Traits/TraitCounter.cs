using System;
using System.Collections.Generic;
using System.Linq;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Snapshot;
using AutoBattler.Data.Traits;

namespace AutoBattler.Battle.Traits
{
    public readonly struct TraitStatus
    {
        public TraitDefinition Definition { get; }
        public int Count { get; }
        public bool IsActive => Count>=Definition.RequiredCount;
        internal TraitStatus(TraitDefinition definition,int count) { Definition=definition; Count=count; }
    }

    /// <summary>팀별로 확정한 집계. 전투 중 사망이나 이동으로 변경하지 않음.</summary>
    public sealed class TraitSnapshot
    {
        public static TraitSnapshot Empty { get; }=new TraitSnapshot(Array.Empty<TraitStatus>());
        public IReadOnlyList<TraitStatus> Statuses { get; }
        public IReadOnlyList<TraitDefinition> Active { get; }
        internal TraitSnapshot(TraitStatus[] statuses)
        {
            Statuses=Array.AsReadOnly(statuses);
            Active=Array.AsReadOnly(statuses.Where(status=>status.IsActive)
                .GroupBy(status=>(status.Definition.Category,status.Definition.GroupId))
                .Select(group=>group.OrderByDescending(status=>status.Definition.RequiredCount).ThenBy(status=>status.Definition.Id).First().Definition)
                .OrderBy(definition=>definition.Id).ToArray());
        }
        public int Count(TraitCategory category,int groupId)
        {
            foreach(var status in Statuses)
                if(status.Definition.Category==category && status.Definition.GroupId==groupId) return status.Count;
            return 0;
        }
    }

    public static class TraitCounter
    {
        public static TraitSnapshot Count(IEnumerable<UnitDefinition> deployed,IEnumerable<TraitDefinition> definitions)
        {
            var kinds=deployed.Where(unit=>unit!=null).GroupBy(unit=>unit.Id).Select(group=>group.First()).ToArray();
            return new TraitSnapshot(definitions.OrderBy(definition=>definition.Id)
                .Select(definition=>new TraitStatus(definition,kinds.Count(definition.Matches))).ToArray());
        }
        public static TraitSnapshot CountTeam(IEnumerable<Unit> roster,UnitTeam team,IEnumerable<TraitDefinition> definitions)
            =>Count(roster.Where(unit=>unit!=null && unit.isActiveAndEnabled && unit.Team==team && unit.IsOnBoard)
                .Select(unit=>unit.Definition),definitions);
    }
}
