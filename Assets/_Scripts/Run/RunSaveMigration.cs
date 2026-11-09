using System;
using UnityEngine;
using AutoBattler.Data.Snapshot;

namespace AutoBattler.Run
{
    /// <summary>기존 3×4 전투 저장을 지역별 구간 길이에 맞게 변환. 원본 파일은 변경하지 않음.</summary>
    public static class RunSaveMigration
    {
        public static RunSaveData Adapt(DataRepository data,RunSaveData saved)
        {
            if(saved==null || data.Stages.Count==0 || saved.dataVersion==data.Version
                || !data.IsSaveCompatible(saved.dataVersion) || saved.roundsPerSegment>0 && saved.roundsPerSegment!=4) return saved;
            if(saved.round<1 || saved.round>12) return saved;
            RunSaveData result=JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(saved));
            int length=data.RoundsPerSegment;
            result.round=MapRound(saved.round,length);
            if(result.augments!=null)
            {
                if(result.augments.completedRounds!=null)
                    for(int i=0;i<result.augments.completedRounds.Length;i++) result.augments.completedRounds[i]=MapAugment(result.augments.completedRounds[i],length);
                result.augments.offeredRound=MapAugment(result.augments.offeredRound,length);
            }
            if(result.items?.rewardedRounds!=null)
                for(int i=0;i<result.items.rewardedRounds.Length;i++) result.items.rewardedRounds[i]=MapRound(result.items.rewardedRounds[i],length);
            if(result.roundOutcomes!=null)
                foreach(SavedRoundOutcome outcome in result.roundOutcomes) if(outcome!=null) outcome.roundId=MapRound(outcome.roundId,length);
            result.roundsPerSegment=length; result.dataVersion=data.Version;
            return result;
        }
        private static int MapAugment(int round,int length)=>round switch
        {0=>0,2=>2,5=>length+1,9=>length*2+1,_=>MapRound(round,length)};
        private static int MapRound(int round,int length)
        {
            if(round<1 || round>12) return round;
            int segment=(round-1)/4,index=(round-1)%4;
            int within=index==3?length:index==0?1:segment==0?(index==1?2:Math.Max(3,length-1)):1+(length-1)*index/3;
            return segment*length+within;
        }
    }
}
