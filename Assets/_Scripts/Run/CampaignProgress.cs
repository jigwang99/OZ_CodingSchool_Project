using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoBattler.Run
{
    [Serializable]
    public sealed class CampaignSaveData
    {
        public int saveVersion=2;
        public int[] clearedStageIds=Array.Empty<int>();
        public int[] unlockedUnitIds=Array.Empty<int>();
    }

    /// <summary>한 도전과 분리한 스테이지 클리어·유닛 해금 영구 기록.</summary>
    public sealed class CampaignProgress
    {
        private readonly HashSet<int> cleared;
        private readonly HashSet<int> unlocked;
        public IReadOnlyList<int> ClearedStageIds=>Array.AsReadOnly(cleared.OrderBy(id=>id).ToArray());
        public IReadOnlyList<int> UnlockedUnitIds=>Array.AsReadOnly(unlocked.OrderBy(id=>id).ToArray());
        public CampaignProgress():this(new CampaignSaveData()) { }
        public CampaignProgress(CampaignSaveData saved)
        {
            if(saved==null || saved.saveVersion<1 || saved.saveVersion>2 || saved.clearedStageIds==null
                || saved.clearedStageIds.Any(id=>id<=0) || saved.clearedStageIds.Distinct().Count()!=saved.clearedStageIds.Length)
                throw new ArgumentException("지원하지 않는 스테이지 클리어 기록.");
            cleared=new HashSet<int>(saved.clearedStageIds);
            int[] units=saved.unlockedUnitIds ?? Array.Empty<int>();
            if(units.Any(id=>id<=0) || units.Distinct().Count()!=units.Length) throw new ArgumentException("유닛 해금 기록 오류.");
            unlocked=new HashSet<int>(units);
        }
        public bool Contains(int stageId)=>cleared.Contains(stageId);
        public bool IsUnitUnlocked(int unitId)=>unlocked.Contains(unitId);
        public CampaignSaveData WithUnlocked(IEnumerable<int> unitIds)=>new CampaignSaveData
        {clearedStageIds=cleared.OrderBy(id=>id).ToArray(),unlockedUnitIds=unlocked.Concat(unitIds).Distinct().OrderBy(id=>id).ToArray()};
        public CampaignSaveData WithClear(int stageId)
        {
            if(stageId<=0) throw new ArgumentOutOfRangeException(nameof(stageId));
            return new CampaignSaveData {clearedStageIds=cleared.Concat(new[]{stageId}).Distinct().OrderBy(id=>id).ToArray(),unlockedUnitIds=unlocked.OrderBy(id=>id).ToArray()};
        }
    }
}
