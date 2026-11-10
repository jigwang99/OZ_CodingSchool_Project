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
        private Action<string> failure;
        public bool CanTrade=>state!=null && state.CanUseEconomy;
        public bool CanBuy => state!=null && state.CanTradeUnits
            && (flow.CurrentPhase==BattlePhase.Preparation || flow.CurrentPhase==BattlePhase.Combat
                || flow.CurrentPhase==BattlePhase.Result && state.IsSettled);
        public bool CanSell(int instanceId) => CanBuy && state.Units.Any(unit=>unit.Placement.InstanceId==instanceId
            && (flow.CurrentPhase!=BattlePhase.Combat || unit.Placement.Location.Kind==AutoBattler.Battle.Placement.UnitLocationKind.Bench));
        public void Initialize(BattleFlowController phases,UnitDragController input,Action<string> message,Action<string> failureMessage=null)
        { flow=phases; drag=input; publish=message; failure=failureMessage; }
        private string TradeFailure=>state==null ? "데이터를 불러오는 중입니다." : state.Augments.HasPending ? "증강을 먼저 선택해 주세요." : "현재 거래할 수 없습니다.";
        private bool IsEmptySlot(int slot)=>state!=null && slot>=0 && slot<state.Shop.Count && state.Shop[slot]==0;
        private bool IsLockedSlot(int slot)=>state!=null && slot>=0 && slot<state.Shop.Count
            && state.Shop[slot]!=0 && !state.IsUnitAvailable(state.Shop[slot]);
        public bool CanBuySlot(int slot)=>CanBuy && !IsEmptySlot(slot) && !IsLockedSlot(slot) && state.BuyFailureReason(slot)==null;
        public string BuyFailureReason(int slot)=>IsEmptySlot(slot) || IsLockedSlot(slot) ? null : !CanBuy ? TradeFailure : state.BuyFailureReason(slot);
        public string RefreshFailureReason=>!CanTrade ? null : state.Gold<state.Data.IntRule(AutoBattler.Data.Snapshot.EconomyRule.RefreshCost) ? "골드가 부족합니다." : null;
        public string ExperienceFailureReason=>!CanTrade ? null : state.Level>=10 ? "최대 레벨입니다." : state.Gold<state.Data.IntRule(AutoBattler.Data.Snapshot.EconomyRule.XpCost) ? "골드가 부족합니다." : null;
        private bool Reject(string reason) { failure?.Invoke(reason); SoundManager.PlayEffect(SoundId.Denied); return false; }
        public void Bind(RunState current)=>state=current;
        public bool Buy(int slot)=>!IsEmptySlot(slot) && !IsLockedSlot(slot) && Execute(()=>state.TryBuy(slot),SoundId.Trade,CanBuy);
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
            if(!(allowed ?? CanTrade))
                return allowed.HasValue && Reject(CanBuy ? "전투 중에는 대기석 유닛만 판매할 수 있습니다." : TradeFailure);
            drag.CancelDrag(); bool success=command(); publish(success?(flow.CurrentPhase==BattlePhase.Combat?"전투 중":flow.CurrentPhase==BattlePhase.Result?state.LastResult:"준비"):state.LastError);
            if(!success) failure?.Invoke(state.LastError);
            SoundManager.PlayEffect(success?effect:SoundId.Denied); return success;
        }
    }
}
