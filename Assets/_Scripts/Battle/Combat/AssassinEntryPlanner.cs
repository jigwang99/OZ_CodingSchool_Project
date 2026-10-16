using System.Collections.Generic;
using System.Linq;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Units;
using UnityEngine;

namespace AutoBattler.Battle.Combat
{
    /// <summary>전투 시작 배치를 기준으로 암살자의 후방 대상과 착지 칸 결정.</summary>
    public static class AssassinEntryPlanner
    {
        public const int AssassinClassId=4;

        public static Unit SelectTarget(Unit assassin,IReadOnlyList<Unit> participants)
        {
            Vector3 rear=EnemyRearDirection(assassin.Team);
            return participants.Where(unit=>unit!=null && unit.isActiveAndEnabled && unit.Health.IsAlive
                    && unit.IsOnBoard && unit.CurrentCell.HasValue && unit.Team!=assassin.Team)
                .OrderByDescending(unit=>Vector3.Dot(unit.GridLayout.CellToWorld(unit.CurrentCell.Value),rear))
                .ThenBy(unit=>unit.Definition.Role).ThenBy(unit=>unit.InstanceId).FirstOrDefault();
        }

        public static bool TryChooseLanding(Unit assassin,Unit target,CombatBoard board,out HexCoord destination)
        {
            var grid=assassin.GridLayout;
            var from=assassin.CurrentCell.Value;
            var targetCell=target.CurrentCell.Value;
            var rear=EnemyRearDirection(assassin.Team);
            var candidates=new List<HexCoord>();
            for(int col=0;col<HexGridLayout.Columns;col++)
                for(int row=0;row<HexGridLayout.Rows;row++)
                {
                    var cell=HexCoord.FromOffset(col,row);
                    // 자기 칸은 이동할 빈칸이 없을 때의 최종 대기 위치로 유지.
                    if(cell!=from && board.IsFree(cell,assassin.InstanceId)) candidates.Add(cell);
                }
            var ordered=candidates.OrderBy(cell=>cell.DistanceTo(targetCell))
                .ThenByDescending(cell=>Vector3.Dot(grid.CellToWorld(cell)-grid.CellToWorld(targetCell),rear))
                .ThenBy(cell=>cell.Q).ThenBy(cell=>OffsetRow(cell));
            foreach(var cell in ordered) { destination=cell; return true; }
            destination=from; return false;
        }

        private static int OffsetRow(HexCoord cell) { cell.ToOffset(out _,out int row); return row; }
        private static Vector3 EnemyRearDirection(UnitTeam team)=>team==UnitTeam.Ally?Vector3.right:Vector3.left;
    }
}
