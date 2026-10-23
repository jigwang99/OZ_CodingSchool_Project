using System.Collections.Generic;
using System.Linq;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Traits;
using AutoBattler.Battle.Units;
using AutoBattler.Run;
using AutoBattler.Utility;
using UnityEngine;

namespace AutoBattler.Managers
{
    /// <summary>진영별 시너지 집계와 전투 효과 세션의 시작·해제 담당.</summary>
    [DisallowMultipleComponent]
    public sealed class SynergyManager : Singleton<SynergyManager>
    {
        private RunState state;
        private UnitManager units;
        private BattleFlowController flow;
        private TraitBattleSession session;
        public TraitSnapshot Allies { get; private set; }=TraitSnapshot.Empty;
        public TraitSnapshot Enemies { get; private set; }=TraitSnapshot.Empty;
        public void Initialize(UnitManager roster,BattleFlowController phases) { units=roster; flow=phases; }
        public void Bind(RunState current)=>state=current;
        public void Refresh()
        {
            if(state==null || flow.CurrentPhase==BattlePhase.Combat) return;
            var roster=units.Allies.Concat(units.Enemies);
            Allies=TraitCounter.CountTeam(roster,UnitTeam.Ally,state.Data.Traits);
            Enemies=TraitCounter.CountTeam(roster,UnitTeam.Enemy,state.Data.Traits);
        }
        public void BeginBattle(IReadOnlyList<Unit> participants)
        { EndBattle(); session=new TraitBattleSession(participants,Allies,Enemies); session.Start(); }
        public void EndBattle() { session?.Dispose(); session=null; }
        protected override void OnDestroy() { EndBattle(); base.OnDestroy(); }
    }
}
