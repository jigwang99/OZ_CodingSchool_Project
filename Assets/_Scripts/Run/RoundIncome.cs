using System;

namespace AutoBattler.Run
{
    /// <summary>한 번의 정산에서 획득하는 골드의 항목별 금액.</summary>
    public readonly struct RoundIncome : IEquatable<RoundIncome>
    {
        public int BaseGold { get; }
        public int InterestGold { get; }
        public int StreakGold { get; }
        public int AugmentGold { get; }
        public int Total => BaseGold + InterestGold + StreakGold + AugmentGold;
        public RoundIncome(int basic, int interest, int streak, int augment)
        { BaseGold = basic; InterestGold = interest; StreakGold = streak; AugmentGold = augment; }
        public bool Equals(RoundIncome other) => BaseGold == other.BaseGold && InterestGold == other.InterestGold
            && StreakGold == other.StreakGold && AugmentGold == other.AugmentGold;
        public override bool Equals(object obj) => obj is RoundIncome other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(BaseGold, InterestGold, StreakGold, AugmentGold);
    }
}
