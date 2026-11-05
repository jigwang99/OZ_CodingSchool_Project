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
        public bool IsInventoryOpen { get; private set; }
        public bool CanEquip=>state!=null && state.CanManageRoster
            && (flow.CurrentPhase==BattlePhase.Preparation || flow.CurrentPhase==BattlePhase.Combat);
        public bool CanUnequip=>CanEquip && flow.CurrentPhase==BattlePhase.Preparation;
        public void Initialize(BattleFlowController phases,UnitManager roster,Action<string> message) { flow=phases; units=roster; publish=message; }
        public void Bind(RunState current) { state=current; if(current==null) IsInventoryOpen=false; }
        public void SetInventoryOpen(bool open)=>IsInventoryOpen=open;
        public bool Equip(int itemId,int unitId)
        {
            if(!CanEquip || !units.Allies.Any(unit=>unit.InstanceId==unitId && unit.Health.IsAlive)) return false;
            return Execute(()=>state.TryEquipItem(itemId,unitId));
        }
        public bool Unequip(int itemId)=>CanUnequip && Execute(()=>state.TryUnequipItem(itemId));
        public bool FuseStored(int firstItemId,int secondItemId)=>CanEquip && Execute(()=>state.TryFuseItems(firstItemId,secondItemId));
        private bool Execute(Func<bool> command)
        {
            if(!CanEquip) return false;
            bool success=command();
            publish(success?(flow.CurrentPhase==BattlePhase.Combat?"전투 중":"준비"):state.LastError);
            SoundManager.PlayEffect(success?SoundId.Place:SoundId.Denied); return success;
        }
    }
}
