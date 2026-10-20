using System;
using System.Collections.Generic;
using System.Linq;
using AutoBattler.Data.Snapshot;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Data.Augments;

namespace AutoBattler.Run
{
    public sealed class OwnedUnit
    {
        public int DefinitionId { get; }
        public int Star { get; internal set; }=1;
        public UnitState Placement { get; }
        public OwnedUnit(int id, int instanceId) { DefinitionId=id; Placement=new UnitState(instanceId); }
    }
    /// <summary>상점 예약, 구매·판매·합성 및 라운드 정산을 한 도전의 상태로 관리.</summary>
    public sealed class RunState
    {
        public DataRepository Data { get; }
        public AugmentState Augments { get; }
        public BoardState Board { get; }=new BoardState();
        public PlacementService Placement { get; }
        public int Gold { get; private set; } public int Level { get; private set; }=1;
        public int Xp { get; private set; } public int Lives { get; private set; }
        public int Round { get; private set; }=1; public int WinStreak { get; private set; }
        public bool IsOver { get; private set; } public bool WonRun { get; private set; }
        public bool CanPrepare { get; set; }=true;
        public string LastError { get; private set; }
        public string LastResult { get; private set; }
        public IReadOnlyList<OwnedUnit> Units => owned.AsReadOnly();
        public IReadOnlyList<int> Shop => Array.AsReadOnly(shop);
        public event Action Changed;
        private readonly List<OwnedUnit> owned=new List<OwnedUnit>();
        private readonly Dictionary<int,int> stock=new Dictionary<int,int>();
        private readonly int[] shop=new int[5];
        private readonly Random random;
        private int nextId=1, settledRound;
        public RunState(DataRepository data,int seed)
        {
            Data=data; random=new Random(seed); Placement=new PlacementService(Board);
            Augments=new AugmentState(data.Augments,seed);
            Gold=data.IntRule(EconomyRule.StartGold); Lives=data.IntRule(EconomyRule.StartLives);
            foreach(var unit in data.Units.Values) stock.Add(unit.Id,data.PoolCopies(unit.Cost));
            int starter=Draw(new int[]{100,0,0,0,0});
            if(starter==0) throw new InvalidOperationException("시작용 1코스트 기물 필요.");
            var initial=new OwnedUnit(starter,nextId++);
            Placement.TryRegisterUnit(initial.Placement,UnitLocation.OnBench(0)); owned.Add(initial);
            Reroll();
            Augments.EnterRound(Round,Data.Rounds[Round].OffersAugment);
        }
        public int Stock(int definitionId)=>stock[definitionId];
        private bool Ready()=>CanPrepare && !IsOver && !Augments.HasPending;
        public bool TrySelectAugment(int id)
        {
            if(!CanPrepare || IsOver || !Augments.TrySelect(Round,id,out var definition)) return false;
            if(definition.Effect==AugmentEffect.ImmediateGold) Gold+=(int)definition.Value;
            Notify(); return true;
        }
        private bool Fail(string message) { LastError=message; return false; }
        private void Notify() { LastError=null; Changed?.Invoke(); }
        public bool TryRefresh()
        {
            if(!Ready()) return Fail("Refresh is available during preparation.");
            int cost=Data.IntRule(EconomyRule.RefreshCost);
            if(Gold<cost) return Fail("Not enough gold.");
            Gold-=cost; Reroll(); Notify(); return true;
        }
        public bool TryBuy(int slot)
        {
            if(!Ready() || slot<0 || slot>=shop.Length || shop[slot]==0) return Fail("This shop slot is unavailable.");
            int id=shop[slot], cost=Data.Units[id].Cost;
            if(Gold<cost) return Fail("Not enough gold.");
            var matches=owned.Where(x=>x.DefinitionId==id && x.Star==1)
                .OrderBy(x=>x.Placement.Location.Kind==UnitLocationKind.Board ? 0:1)
                .ThenBy(x=>PositionOrder(x.Placement.Location)).ThenBy(x=>x.Placement.InstanceId).Take(2).ToArray();
            int free=Placement.FindFreeBenchSlot();
            if(free<0 && matches.Length<2) return Fail("The bench is full.");
            // 변경 조건을 모두 확인한 뒤 예약 1개와 보유 2개를 한 번에 합성.
            if(matches.Length==2)
            {
                Board.TryUnregister(matches[1].Placement); owned.Remove(matches[1]); matches[0].Star=2;
            }
            else
            {
                var unit=new OwnedUnit(id,nextId);
                if(!Placement.TryRegisterUnit(unit.Placement,UnitLocation.OnBench(free))) return Fail("Could not place the purchased unit.");
                nextId++; owned.Add(unit);
            }
            shop[slot]=0; Gold-=cost; Notify(); return true;
        }
        private static int PositionOrder(UnitLocation location)
        {
            if(location.Kind==UnitLocationKind.Bench) return location.BenchSlot;
            location.Cell.ToOffset(out int col,out int row); return col*7+row;
        }
        public bool TrySell(int instanceId)
        {
            if(!Ready()) return Fail("Sell during preparation.");
            var unit=owned.FirstOrDefault(x=>x.Placement.InstanceId==instanceId);
            if(unit==null) return Fail("Select a unit first.");
            int copies=unit.Star==2?3:1, cost=Data.Units[unit.DefinitionId].Cost;
            if(!Board.TryUnregister(unit.Placement)) return Fail("Placement state is missing.");
            owned.Remove(unit); stock[unit.DefinitionId]+=copies; Gold+=unit.Star==2 ? cost*3-1:cost;
            Notify(); return true;
        }
        public bool TryBuyXp()
        {
            if(!Ready() || Level==10) return Fail("XP purchase is unavailable.");
            int cost=Data.IntRule(EconomyRule.XpCost);
            if(Gold<cost) return Fail("Not enough gold.");
            Gold-=cost; AddXp(Data.IntRule(EconomyRule.PurchasedXp)); Notify(); return true;
        }
        private void AddXp(int amount)
        {
            if(Level==10) return;
            Xp+=amount;
            while(Level<10 && Xp>=Data.RequiredXp(Level)) { Xp-=Data.RequiredXp(Level); Level++; }
            if(Level==10) Xp=0;
            Placement.SetPlayerLevel(Level);
        }
        public bool TrySettle(bool won)
        {
            if(IsOver || settledRound==Round || CanPrepare || Augments.HasPending) return false;
            settledRound=Round; WinStreak=won?WinStreak+1:0;
            int streak=WinStreak>=5?3:WinStreak==4?2:WinStreak==3?1:0;
            int interest=Math.Min(Data.IntRule(EconomyRule.InterestCap),Gold/Data.IntRule(EconomyRule.InterestDivisor));
            int augmentGold=won?Augments.Selected.Where(x=>x.Effect==AugmentEffect.VictoryGold).Sum(x=>(int)x.Value):0;
            int income=Data.Rounds[Round].Reward+streak+interest+augmentGold;
            Gold+=income; AddXp(Data.IntRule(EconomyRule.RoundXp));
            if(!won) Lives--;
            IsOver=Lives<=0 || Round==Data.Rounds.Count;
            WonRun=IsOver && won && Round==Data.Rounds.Count;
            LastResult=$"{(won?"Victory":"Defeat")}   +{income} gold (interest {interest}, streak {streak}, augments {augmentGold})";
            Notify(); return true;
        }
        public bool TryNextRound()
        {
            if(IsOver || settledRound!=Round || CanPrepare) return false;
            Round++; CanPrepare=true; Reroll(); Augments.EnterRound(Round,Data.Rounds[Round].OffersAugment); Notify(); return true;
        }
        private void Reroll()
        {
            for(int i=0;i<shop.Length;i++) { if(shop[i]!=0) stock[shop[i]]++; shop[i]=0; }
            var rates=Data.ShopWeights(Level);
            for(int i=0;i<shop.Length;i++) shop[i]=Draw(rates);
        }
        private int Draw(IReadOnlyList<int> rates)
        {
            int total=0;
            for(int cost=1;cost<=5;cost++)
                if(Data.Units.Values.Any(x=>x.Cost==cost && stock[x.Id]>0)) total+=rates[cost-1];
            if(total==0) return 0;
            int tierPick=random.Next(total), tier=0;
            for(int cost=1;cost<=5;cost++)
            {
                if(!Data.Units.Values.Any(x=>x.Cost==cost && stock[x.Id]>0)) continue;
                tierPick-=rates[cost-1]; if(tierPick<0) { tier=cost; break; }
            }
            var candidates=Data.Units.Values.Where(x=>x.Cost==tier && stock[x.Id]>0).OrderBy(x=>x.Id).ToArray();
            int copyPick=random.Next(candidates.Sum(x=>stock[x.Id]));
            foreach(var unit in candidates)
            {
                copyPick-=stock[unit.Id]; if(copyPick>=0) continue;
                stock[unit.Id]--; return unit.Id;
            }
            throw new InvalidOperationException("기물 추첨 실패.");
        }
    }
}
