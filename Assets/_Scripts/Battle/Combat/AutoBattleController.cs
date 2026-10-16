using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoBattler.Battle.AI;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Units;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Battle.Combat
{
    /// <summary>공유 보드와 전투 수명을 관리하고 고정 ID 순서로 유닛 행동트리 실행.</summary>
    public sealed class AutoBattleController
    {
        private readonly UnitBehaviourTree[] trees;
        private bool started;
        public IReadOnlyList<UnitBehaviourTree> Trees { get; }
        public AutoBattleController(IEnumerable<Unit> units)
        {
            var participants=units.OrderBy(unit=>unit.InstanceId).ToArray();
            var view=Array.AsReadOnly(participants);
            var board=new CombatBoard();
            foreach(var unit in participants)
                if(!unit.CurrentCell.HasValue || !board.TryAdd(unit.InstanceId,unit.CurrentCell.Value))
                    throw new InvalidOperationException("전투 시작 좌표 중복 또는 영역 밖.");
            trees=participants.Select(unit=>new UnitBehaviourTree(new UnitBlackboard(unit,board,view))).ToArray();
            Trees=Array.AsReadOnly(trees);
        }
        public async UniTask<bool> RunAsync(float duration,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(started) throw new InvalidOperationException("같은 전투 실행은 한 번만 가능.");
            started=true;
            float deadline=Time.time+duration;
            await RunEntriesAsync(token);
            while(true)
            {
                token.ThrowIfCancellationRequested();
                bool allies=trees.Any(tree=>tree.Blackboard.Unit.Team==UnitTeam.Ally && tree.Blackboard.Unit.Health.IsAlive);
                bool enemies=trees.Any(tree=>tree.Blackboard.Unit.Team==UnitTeam.Enemy && tree.Blackboard.Unit.Health.IsAlive);
                if(!allies || !enemies || Time.time>=deadline) return allies && !enemies;
                foreach(var tree in trees) tree.Tick(token);
                await UniTask.Delay(50,cancellationToken:token);
            }
        }
        private async UniTask RunEntriesAsync(CancellationToken token)
        {
            var entries=new List<(UnitBlackboard context,CombatUnitReference owner,CombatUnitReference target,HexCoord from,HexCoord to)>();
            // 양 팀 모두 시작 좌표로 판단. 모든 예약을 마친 다음 도약 시작.
            foreach(var tree in trees)
            {
                var context=tree.Blackboard; var unit=context.Unit;
                if(unit.Definition.ClassId!=AssassinEntryPlanner.AssassinClassId || !unit.Move.CanMove) continue;
                var target=AssassinEntryPlanner.SelectTarget(unit,context.Participants);
                if(target==null) continue;
                context.Target=target;
                if(AssassinEntryPlanner.TryChooseLanding(unit,target,context.Board,out var destination)
                    && context.Board.TryReserveJump(unit.InstanceId,unit.CurrentCell.Value,destination))
                    entries.Add((context,new CombatUnitReference(unit),new CombatUnitReference(target),unit.CurrentCell.Value,destination));
            }
            var tasks=new List<UniTask>();
            foreach(var entry in entries)
                tasks.Add(JumpAsync(entry.context,entry.owner,entry.target,entry.from,entry.to,token));
            await UniTask.WhenAll(tasks);
            token.ThrowIfCancellationRequested();
        }
        private static async UniTask JumpAsync(UnitBlackboard context,CombatUnitReference owner,CombatUnitReference target,
            HexCoord from,HexCoord to,CancellationToken token)
        {
            bool arrived=false;
            try
            {
                if(!owner.IsAlive) return;
                arrived=await owner.Unit.Move.JumpToCellAsync(to,token);
                if(arrived && owner.IsAlive)
                {
                    if(!context.Board.TryArrive(owner.InstanceId,from,to))
                        throw new InvalidOperationException("도약 착지 예약 소유권 불일치.");
                    context.Target=target.IsAlive?target.Unit:null;
                }
            }
            finally
            {
                context.Board.CancelReservation(owner.InstanceId,to);
                if(!owner.IsAlive) context.Board.Release(owner.InstanceId);
                context.Path=null; context.Revision=-1;
            }
        }
    }
}
