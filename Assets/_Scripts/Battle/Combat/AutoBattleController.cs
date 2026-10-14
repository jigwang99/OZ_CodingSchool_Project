using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoBattler.Battle.AI;
using AutoBattler.Battle.Units;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Battle.Combat
{
    /// <summary>공유 보드와 전투 수명을 관리하고 고정 ID 순서로 유닛 행동트리 실행.</summary>
    public sealed class AutoBattleController
    {
        private readonly UnitBehaviourTree[] trees;
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
            float deadline=Time.time+duration;
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
    }
}
