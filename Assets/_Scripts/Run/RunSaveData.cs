using System;

namespace AutoBattler.Run
{
    public enum RunSavePhase { Preparation, Result }

    /// <summary>정의 ID와 값만 기록하는 진행도 저장 형식.</summary>
    [Serializable]
    public sealed class RunSaveData
    {
        public int saveVersion=1;
        public int gridLayoutVersion=1;
        public string dataVersion;
        public int stageId;
        public string savedAtUtc;
        public RunSavePhase phase;
        public int round, gold, lives, level, xp, winStreak, nextInstanceId;
        public bool isOver, wonRun, augmentWindowOpen;
        public string lastResult;
        public int randomSeed, randomDrawCount;
        public int[] shop;
        public SavedStock[] stock;
        public SavedUnit[] units;
        public SavedAugments augments;
    }

    [Serializable]
    public sealed class SavedUnit
    {
        public int definitionId, instanceId, star;
        public AutoBattler.Battle.Placement.UnitLocationKind location;
        public int col, row, benchSlot;
    }

    [Serializable]
    public sealed class SavedStock { public int definitionId, available; }

    [Serializable]
    public sealed class SavedAugments
    {
        public int randomSeed, randomDrawCount, offeredRound;
        public int[] selected, completedRounds, offers;
    }
}
