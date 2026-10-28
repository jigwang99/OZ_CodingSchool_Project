using System;
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
        private BattleFlowController flow;
        private Action<string> publish;
        public bool IsInventoryOpen { get; private set; }
        public bool CanEquip=>state!=null && state.CanPrepare && !state.IsOver && !state.Augments.HasPending
            && flow.CurrentPhase==BattlePhase.Preparation;
        public void Initialize(BattleFlowController phases,Action<string> message) { flow=phases; publish=message; }
        public void Bind(RunState current) { state=current; if(current==null) IsInventoryOpen=false; }
        public void SetInventoryOpen(bool open)=>IsInventoryOpen=open;
        public bool Equip(int itemId,int unitId)=>Execute(()=>state.TryEquipItem(itemId,unitId));
        public bool Unequip(int itemId)=>Execute(()=>state.TryUnequipItem(itemId));
        private bool Execute(Func<bool> command)
        {
            if(!CanEquip) return false;
            bool success=command();
            publish(success?"준비":state.LastError);
            SoundManager.PlayEffect(success?SoundId.Place:SoundId.Denied); return success;
        }
    }
}
