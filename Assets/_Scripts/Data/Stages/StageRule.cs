using System;
using System.Collections.Generic;

namespace AutoBattler.Data.Stages
{
    [Serializable]
    public sealed class StageRow
    {
        public int stageId,chapter,number,roundsPerSegment;
        public string regionName,locationName;
    }
    [Serializable]
    public sealed class StageUnlockPlanRow { public int entryId,stageId,unitId; }

    /// <summary>스테이지의 장소·전투 길이와 진입 시 해금 유닛 정의.</summary>
    public sealed class StageRule
    {
        private readonly int[] plannedUnits;
        public int Id { get; }
        public int Chapter { get; }
        public int Number { get; }
        public int RoundsPerSegment { get; }
        public int TotalRounds=>RoundsPerSegment*3;
        public string RegionName { get; }
        public string LocationName { get; }
        public IReadOnlyList<int> PlannedUnlockUnitIds=>Array.AsReadOnly(plannedUnits);
        public StageRule(StageRow row,int[] plannedUnits)
        {
            Id=row.stageId; Chapter=row.chapter; Number=row.number; RoundsPerSegment=row.roundsPerSegment;
            RegionName=row.regionName; LocationName=row.locationName; this.plannedUnits=(int[])plannedUnits.Clone();
        }
    }
}
