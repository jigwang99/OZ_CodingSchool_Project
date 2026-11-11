using System;
using System.Linq;
using AutoBattler.Audio;
using AutoBattler.Battle.Flow;
using AutoBattler.Run;
using AutoBattler.Utility;
using UnityEngine;

namespace AutoBattler.Managers
{
    /// <summary>장착·해제 요청과 인벤토리 입력 상태 관리.</summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentManager : Singleton<EquipmentManager>
    {
        private RunState state;
        private UnitManager units;
        private BattleFlowController flow;
        private Action<string> publish;
        private Action<string> failure;
        public bool IsInventoryOpen { get; private set; }
        public bool CanEquip=>state!=null && state.CanManageRoster
            && (flow.CurrentPhase==BattlePhase.Preparation || flow.CurrentPhase==BattlePhase.Combat
                || flow.CurrentPhase==BattlePhase.Result && state.HasPendingRoundReward);
        public bool CanUnequip=>CanEquip && flow.CurrentPhase!=BattlePhase.Combat;
        public void Initialize(BattleFlowController phases,UnitManager roster,Action<string> message,Action<string> failureMessage=null)
        { flow=phases; units=roster; publish=message; failure=failureMessage; }
        private bool Reject(string message) { failure?.Invoke(message); SoundManager.PlayEffect(SoundId.Denied); return false; }
        public void Bind(RunState current) { state=current; if(current==null) IsInventoryOpen=false; }
        public void SetInventoryOpen(bool open)=>IsInventoryOpen=open;
        public bool Equip(int itemId,int unitId)
        {
            if(!CanEquip) return Reject("현재 아이템을 장착할 수 없습니다.");
            if(!units.Allies.Any(unit=>unit.InstanceId==unitId && unit.Health.IsAlive)) return Reject("살아 있는 아군 유닛에 장착해 주세요.");
            return Execute(()=>state.TryEquipItem(itemId,unitId));
        }
        public bool Unequip(int itemId)=>CanUnequip ? Execute(()=>state.TryUnequipItem(itemId)) : Reject("준비 단계 또는 결과 대기 중에 해제할 수 있습니다.");
        public bool FuseStored(int firstItemId,int secondItemId)=>CanEquip ? Execute(()=>state.TryFuseItems(firstItemId,secondItemId)) : Reject("현재 아이템을 합성할 수 없습니다.");
        private bool Execute(Func<bool> command)
        {
            if(!CanEquip) return Reject("현재 아이템을 사용할 수 없습니다.");
            bool success=command();
            publish(success?(flow.CurrentPhase==BattlePhase.Combat?"전투 중":flow.CurrentPhase==BattlePhase.Result?state.LastResult:"준비"):state.LastError);
            if(!success) failure?.Invoke(state.LastError);
            SoundManager.PlayEffect(success?SoundId.Place:SoundId.Denied); return success;
        }
    }
}
