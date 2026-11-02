using System;
using System.Linq;
using AutoBattler.Audio;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Run;
using AutoBattler.Utility;
using UnityEngine;

namespace AutoBattler.Managers
{
    /// <summary>단계별 구매·판매·상점 갱신·경험치 명령과 입력 제한 담당.</summary>
    [DisallowMultipleComponent]
    public sealed class ShopManager : Singleton<ShopManager>
    {
        private RunState state;
        private BattleFlowController flow;
        private UnitDragController drag;
        private Action<string> publish;
        public bool CanTrade=>state!=null && state.CanPrepare && !state.IsOver && flow.CurrentPhase==BattlePhase.Preparation;
        public bool CanBuy => state!=null && state.CanTradeUnits
            && (flow.CurrentPhase==BattlePhase.Preparation || flow.CurrentPhase==BattlePhase.Combat
                || flow.CurrentPhase==BattlePhase.Result && state.IsSettled);
        public bool CanSell(int instanceId) => CanBuy && state.Units.Any(unit=>unit.Placement.InstanceId==instanceId
            && (flow.CurrentPhase!=BattlePhase.Combat || unit.Placement.Location.Kind==AutoBattler.Battle.Placement.UnitLocationKind.Bench));
        public void Initialize(BattleFlowController phases,UnitDragController input,Action<string> message)
        { flow=phases; drag=input; publish=message; }
        public void Bind(RunState current)=>state=current;
        public bool Buy(int slot)=>Execute(()=>state.TryBuy(slot),SoundId.Trade,CanBuy);
        public bool RefreshShop()=>Execute(()=>state.TryRefresh(),SoundId.Refresh);
        public bool BuyXp()=>Execute(()=>state.TryBuyXp(),SoundId.Trade);
        public bool SellSelected()
        { int id=drag.SelectedUnit?.State?.InstanceId ?? 0; return Sell(id); }
        public bool Sell(int instanceId)
        {
            bool success=Execute(()=>state.TrySell(instanceId),SoundId.Trade,CanSell(instanceId));
            if(success) drag.ClearSelection();
            return success;
        }
        private bool Execute(Func<bool> command,SoundId effect,bool? allowed=null)
        {
            if(!(allowed ?? CanTrade)) return false;
            drag.CancelDrag(); bool success=command(); publish(success?(flow.CurrentPhase==BattlePhase.Combat?"전투 중":flow.CurrentPhase==BattlePhase.Result?state.LastResult:"준비"):state.LastError);
            SoundManager.PlayEffect(success?effect:SoundId.Denied); return success;
        }
    }
}
