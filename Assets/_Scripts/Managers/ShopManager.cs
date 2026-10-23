using System;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Run;
using AutoBattler.Utility;
using UnityEngine;

namespace AutoBattler.Managers
{
    /// <summary>준비 단계의 구매·판매·상점 갱신·경험치 명령과 입력 제한 담당.</summary>
    [DisallowMultipleComponent]
    public sealed class ShopManager : Singleton<ShopManager>
    {
        private RunState state;
        private BattleFlowController flow;
        private UnitDragController drag;
        private Action<string> publish;
        public bool CanTrade=>state!=null && state.CanPrepare && !state.IsOver && flow.CurrentPhase==BattlePhase.Preparation;
        public void Initialize(BattleFlowController phases,UnitDragController input,Action<string> message)
        { flow=phases; drag=input; publish=message; }
        public void Bind(RunState current)=>state=current;
        public bool Buy(int slot)=>Execute(()=>state.TryBuy(slot));
        public bool RefreshShop()=>Execute(()=>state.TryRefresh());
        public bool BuyXp()=>Execute(()=>state.TryBuyXp());
        public bool SellSelected()
        { int id=drag.SelectedUnit?.State?.InstanceId ?? 0; return Execute(()=>state.TrySell(id)); }
        private bool Execute(Func<bool> command)
        {
            if(!CanTrade) return false;
            drag.CancelDrag(); bool success=command(); publish(success?"준비":state.LastError); return success;
        }
    }
}
