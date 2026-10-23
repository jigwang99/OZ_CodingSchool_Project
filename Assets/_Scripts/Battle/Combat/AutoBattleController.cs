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
            if(started) throw new InvalidOperationException("같은 전투 실행은 한 번만 가능.");
            started=true;
            try
            {
                token.ThrowIfCancellationRequested();
                foreach(var tree in trees) tree.Blackboard.Token=token;
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
            finally { foreach(var tree in trees) tree.Dispose(); }
        }
        private async UniTask RunEntriesAsync(CancellationToken token)
        {
            var entries=new List<(UnitBehaviourTree tree,CombatUnitReference owner,CombatUnitReference target,HexCoord from,HexCoord to)>();
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
                    entries.Add((tree,new CombatUnitReference(unit),new CombatUnitReference(target),unit.CurrentCell.Value,destination));
            }
            var tasks=new List<UniTask>();
            foreach(var entry in entries)
                tasks.Add(JumpAsync(entry.tree,entry.owner,entry.target,entry.from,entry.to,token));
            await UniTask.WhenAll(tasks);
            token.ThrowIfCancellationRequested();
        }
        private static async UniTask JumpAsync(UnitBehaviourTree tree,CombatUnitReference owner,CombatUnitReference target,
            HexCoord from,HexCoord to,CancellationToken token)
        {
            var context=tree.Blackboard;
            try
            {
                if(!owner.IsAlive) return;
                bool arrived=await tree.States.JumpAsync(from,to,token);
                if(arrived && owner.IsAlive)
                {
                    context.Target=target.IsAlive?target.Unit:null;
                }
            }
            finally
            {
                if(!owner.IsAlive) tree.States.ReleaseUnit();
                context.Path=null; context.Revision=-1;
            }
        }
    }
}
