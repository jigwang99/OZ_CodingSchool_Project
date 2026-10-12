using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoBattler.Battle.Units;
using AutoBattler.Battle.Grid;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Battle.Combat
{
    /// <summary>고정 ID 순서로 대상 선정, 경로 갱신과 다음 셀 예약 요청.</summary>
    public sealed class AutoBattleController
    {
        private sealed class Agent
        {
            public Unit Unit,Target; public List<HexCoord> Path;
            public int Revision=-1; public HexCoord TargetCell;
            public float RetryTime, FailedSince=-1f; public bool Released;
        }
        private readonly CombatBoard board=new CombatBoard();
        private readonly List<Agent> agents;
        public AutoBattleController(IEnumerable<Unit> units)
        {
            agents=units.OrderBy(x=>x.InstanceId).Select(x=>new Agent {Unit=x}).ToList();
            foreach(var agent in agents)
                if(!agent.Unit.CurrentCell.HasValue || !board.TryAdd(agent.Unit.InstanceId,agent.Unit.CurrentCell.Value))
                    throw new InvalidOperationException("전투 시작 좌표 중복 또는 영역 밖.");
        }
        public async UniTask<bool> RunAsync(float duration,CancellationToken token)
        {
            float deadline=Time.time+duration;
            while(true)
            {
                token.ThrowIfCancellationRequested();
                bool allies=agents.Any(x=>x.Unit.Team==UnitTeam.Ally && x.Unit.Health.IsAlive);
                bool enemies=agents.Any(x=>x.Unit.Team==UnitTeam.Enemy && x.Unit.Health.IsAlive);
                if(!allies || !enemies || Time.time>=deadline) return allies && !enemies;
                foreach(var agent in agents)
                {
                    if(!agent.Unit.Health.IsAlive)
                    {
                        if(!agent.Released) { board.Release(agent.Unit.InstanceId); agent.Released=true; }
                        continue;
                    }
                    Tick(agent,token);
                }
                await UniTask.Delay(50,cancellationToken:token);
            }
        }
        private void Tick(Agent agent,CancellationToken token)
        {
            Unit unit=agent.Unit;
            if(unit.Move.IsMoving || unit.Combat.IsAttacking || Time.time<agent.RetryTime) return;
            if(agent.Target==null || !agent.Target.Health.IsAlive || (agent.FailedSince>=0f && Time.time-agent.FailedSince>=1f))
            {
                agent.Target=ChooseTarget(agent); agent.Path=null; agent.Revision=-1; agent.FailedSince=-1f;
            }
            if(agent.Target==null) { agent.RetryTime=Time.time+.2f; return; }
            if(unit.Combat.IsInAttackRange(agent.Target.Combat))
            {
                agent.FailedSince=-1f;
                if(unit.Combat.CanAttack) unit.Combat.AttackAsync(agent.Target.Combat,token).Forget();
                return;
            }
            HexCoord targetCell=agent.Target.CurrentCell.Value;
            if(agent.Path==null || agent.Path.Count==0 || agent.Revision!=board.Revision || agent.TargetCell!=targetCell)
            {
                agent.Path=HexPathfinder.Find(board,unit.InstanceId,unit.CurrentCell.Value,targetCell,unit.Stats.AttackRange);
                agent.Revision=board.Revision; agent.TargetCell=targetCell;
            }
            if(agent.Path==null || agent.Path.Count==0 || !board.TryReserve(unit.InstanceId,unit.CurrentCell.Value,agent.Path[0]))
            {
                if(agent.FailedSince<0f) agent.FailedSince=Time.time;
                agent.RetryTime=Time.time+.2f; agent.Revision=-1; return;
            }
            HexCoord from=unit.CurrentCell.Value, next=agent.Path[0];
            agent.Path.RemoveAt(0); agent.FailedSince=-1f;
            MoveAsync(agent,from,next,token).Forget();
        }
        private Unit ChooseTarget(Agent agent)
        {
            var unit=agent.Unit;
            foreach(var target in agents.Select(x=>x.Unit).Where(x=>x.Team!=unit.Team && x.Health.IsAlive)
                .OrderBy(x=>unit.CurrentCell.Value.DistanceTo(x.CurrentCell.Value))
                .ThenBy(x=>x.Definition.Role).ThenBy(x=>x.InstanceId))
            {
                if(unit.Combat.IsInAttackRange(target.Combat) || HexPathfinder.Find(board,unit.InstanceId,
                    unit.CurrentCell.Value,target.CurrentCell.Value,unit.Stats.AttackRange)!=null) return target;
            }
            return null;
        }
        private async UniTask MoveAsync(Agent agent,HexCoord from,HexCoord next,CancellationToken token)
        {
            bool arrived=await agent.Unit.Move.MoveToCellAsync(next,token);
            if(arrived && agent.Unit.Health.IsAlive) board.TryArrive(agent.Unit.InstanceId,from,next);
            else board.CancelReservation(agent.Unit.InstanceId,next);
        }
    }
}
