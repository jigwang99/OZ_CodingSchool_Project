using System;
using AutoBattler.Audio;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Units;
using AutoBattler.Run;
using AutoBattler.Battle.Traits;
using AutoBattler.Utility;
using AutoBattler.Data.Augments;
using System.Linq;
using UnityEngine;

namespace AutoBattler.Managers
{
    /// <summary>증강 선택·모달 열기와 닫기·배치 허용·전투 시작 강화 담당.</summary>
    [DisallowMultipleComponent]
    public sealed class AugmentManager : Singleton<AugmentManager>
    {
        private RunState state;
        private BattleFlowController flow;
        private UnitDragController drag;
        private Action<string> publish;
        private AugmentBattleSession session;
        public event Action<AugmentDefinition> Activated;
        public void Initialize(BattleFlowController phases,UnitDragController input,Action<string> message)
        { flow=phases; drag=input; publish=message; }
        public void Bind(RunState current) { EndBattle(); state=current; }
        public void EnterPreparation(bool windowOpen=true)
        {
            if(state==null) return;
            if(state.Augments.HasPending)
            { flow.TryBeginAugmentSelection(); flow.SetAugmentWindowOpen(windowOpen); }
            drag.enabled=flow.CanPlaceUnits;
        }
        public bool ToggleWindow()
        {
            if(state==null || !state.Augments.HasPending || flow.CurrentPhase!=BattlePhase.AugmentSelection) return false;
            drag.CancelDrag();
            if(!flow.SetAugmentWindowOpen(!flow.IsAugmentWindowOpen)) return false;
            drag.enabled=flow.CanPlaceUnits;
            publish("증강 선택");
            return true;
        }
        public bool Select(int id)
        {
            if(state==null || flow.CurrentPhase!=BattlePhase.AugmentSelection || !flow.IsAugmentWindowOpen) return false;
            if(!state.TrySelectAugment(id)) return false;
            flow.TryFinishAugmentSelection(); drag.enabled=true; publish("준비"); SoundManager.PlayEffect(SoundId.Augment);
            AugmentDefinition selected=state.Data.Augments[id];
            if(selected.Effect==AugmentEffect.ImmediateGold || selected.Effect==AugmentEffect.MaterialSupply)
                ReportActivation(selected);
            return true;
        }
        public void ApplyBattleStart(Unit[] units,TraitSnapshot traits)
        {
            EndBattle();
            if(state==null) return;
            session=new AugmentBattleSession(units,state.Augments.Selected,state.Data,state.Placement.PlayerLevel,
                traits.Active.Count,ReportActivation);
            session.Start();
            AugmentApplicator.ApplyBattleStart(units,state.Augments.Selected,ReportActivation);
            bool hasAlly=units.Any(unit=>unit.Team==UnitTeam.Ally && !unit.IsSummon && unit.Health.IsAlive);
            bool hasDummy=units.Any(unit=>unit.Team==UnitTeam.Ally && unit.IsSummon);
            foreach(AugmentDefinition augment in state.Augments.Selected)
                if((hasAlly && (augment.Effect==AugmentEffect.StatModifier || augment.Effect==AugmentEffect.StartingMana))
                    || (hasDummy && augment.Effect==AugmentEffect.TrainingDummy))
                    ReportActivation(augment);
        }
        public void ReportVictoryRewards()
        {
            foreach(AugmentDefinition augment in state.Augments.Selected)
                if(augment.Effect==AugmentEffect.VictoryGold) ReportActivation(augment);
        }
        private void ReportActivation(AugmentDefinition augment)=>Activated?.Invoke(augment);
        public void EndBattle() { session?.Dispose(); session=null; }
        protected override void OnDestroy() { EndBattle(); base.OnDestroy(); }
    }
}
