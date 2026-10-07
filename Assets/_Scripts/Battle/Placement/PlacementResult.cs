namespace AutoBattler.Battle.Placement
{
    /// <summary>배치 성공 여부와 화면 갱신이 필요한 교환 상대를 반환.</summary>
    public readonly struct PlacementResult
    {
        public bool Success { get; }
        public UnitState SwappedUnit { get; }

        public PlacementResult(bool success, UnitState swappedUnit = null)
        {
            Success = success;
            SwappedUnit = success ? swappedUnit : null;
        }
    }
}
