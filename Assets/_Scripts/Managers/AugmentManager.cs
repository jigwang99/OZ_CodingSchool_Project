using System;
using System.Collections.Generic;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Units;
using AutoBattler.Run;
using AutoBattler.Utility;
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
        public void Initialize(BattleFlowController phases,UnitDragController input,Action<string> message)
        { flow=phases; drag=input; publish=message; }
        public void Bind(RunState current)=>state=current;
        public void EnterPreparation()
        {
            if(state==null) return;
            if(state.Augments.HasPending) flow.TryBeginAugmentSelection();
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
            flow.TryFinishAugmentSelection(); drag.enabled=true; publish("준비"); return true;
        }
        public void ApplyBattleStart(IEnumerable<Unit> units)
        { if(state!=null) AugmentApplicator.ApplyBattleStart(units,state.Augments.Selected); }
    }
}
