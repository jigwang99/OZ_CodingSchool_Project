using System.Collections.Generic;
using System.Threading;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Units;

namespace AutoBattler.Battle.AI
{
    public enum UnitBehaviourAction { None, Dead, Stunned, Moving, Attacking, WaitingToRetry, WaitingForTarget, WaitingForAttack, WaitingForPath }

    /// <summary>유닛 한 기의 판단 상태. 문자열 키 없이 대상·경로·재시도 시간을 보관.</summary>
    public sealed class UnitBlackboard
    {
        public Unit Unit { get; }
        public CombatBoard Board { get; }
        public IReadOnlyList<Unit> Participants { get; }
        public Unit Target { get; internal set; }
        public UnitBehaviourAction Action { get; internal set; }
        public float RetryTime { get; internal set; }
        public float FailedSince { get; internal set; }=-1f;
        public bool Released { get; internal set; }
        internal List<HexCoord> Path;
        internal int Revision=-1;
        internal HexCoord TargetCell;
        internal CancellationToken Token;
        public UnitBlackboard(Unit unit,CombatBoard board,IReadOnlyList<Unit> participants)
        { Unit=unit; Board=board; Participants=participants; }
    }
}
