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
        public ItemInventoryState Items { get; private set; }
        public BoardState Board { get; }=new BoardState();
        public PlacementService Placement { get; }
        public int Gold { get; private set; } public int Level { get; private set; }=1;
        public int Xp { get; private set; } public int Lives { get; private set; }
        public int Round { get; private set; }=1; public int WinStreak { get; private set; }
        public bool IsOver { get; private set; } public bool WonRun { get; private set; }
        public bool CanPrepare { get; set; }=true;
        public bool IsSettled=>settledRound==Round;
        public string LastError { get; private set; }
        public string LastResult { get; private set; }
        public IReadOnlyList<OwnedUnit> Units => owned.AsReadOnly();
        public IReadOnlyList<int> Shop => Array.AsReadOnly(shop);
        public event Action Changed;
        private readonly List<OwnedUnit> owned=new List<OwnedUnit>();
        private readonly Dictionary<int,int> stock=new Dictionary<int,int>();
        private readonly int[] shop=new int[5];
        private readonly RunRandom random;
        private int nextId=1, settledRound;
        public RunState(DataRepository data,int seed,int stageId=1)
            :this(data,new RunRandom(seed),new AugmentState(data.Augments,seed))
        {
            Items=new ItemInventoryState(data.Items,stageId,seed^0x59B17);
            Gold=data.IntRule(EconomyRule.StartGold); Lives=data.IntRule(EconomyRule.StartLives);
            foreach(UnitDefinition unit in data.Units.Values) stock.Add(unit.Id,data.PoolCopies(unit.Cost));
            int starter=Draw(new int[]{100,0,0,0,0});
            if(starter==0) throw new InvalidOperationException("시작용 1코스트 기물 필요.");
            OwnedUnit initial=new OwnedUnit(starter,nextId++);
            Placement.TryRegisterUnit(initial.Placement,UnitLocation.OnBench(0)); owned.Add(initial);
            Reroll();
            Augments.EnterRound(Round,Data.Rounds[Round].OffersAugment);
        }
        private RunState(DataRepository data,RunRandom random,AugmentState augments)
        { Data=data; this.random=random; Augments=augments; Placement=new PlacementService(Board);
            Items=new ItemInventoryState(data.Items,1,random.Seed^0x59B17); }

        public RunSaveData Capture(int stageId,bool augmentWindowOpen)
        {
            if(stageId!=Items.StageId) throw new InvalidOperationException("현재 도전의 스테이지와 아이템 스테이지 불일치.");
            if(!CanPrepare && !IsSettled) throw new InvalidOperationException("전투 중에는 전투 직전 저장 상태를 유지합니다.");
            if(owned.Any(unit=>!Board.HasValidLocation(unit.Placement) || !Placement.IsValidLocation(unit.Placement.Location)))
                throw new InvalidOperationException("배치가 해제된 유닛은 저장할 수 없습니다.");
            return new RunSaveData
            {
                dataVersion=Data.Version, stageId=stageId, savedAtUtc=DateTime.UtcNow.ToString("O"),
                phase=CanPrepare?RunSavePhase.Preparation:RunSavePhase.Result,
                round=Round,gold=Gold,lives=Lives,level=Level,xp=Xp,winStreak=WinStreak,nextInstanceId=nextId,
                isOver=IsOver,wonRun=WonRun,lastResult=LastResult,augmentWindowOpen=augmentWindowOpen,
                randomSeed=random.Seed,randomDrawCount=random.DrawCount,shop=(int[])shop.Clone(),
                stock=stock.OrderBy(pair=>pair.Key).Select(pair=>new SavedStock {definitionId=pair.Key,available=pair.Value}).ToArray(),
                units=owned.Select(unit=>
                {
                    unit.Placement.Location.Cell.ToOffset(out int col,out int row);
                    return new SavedUnit {definitionId=unit.DefinitionId,instanceId=unit.Placement.InstanceId,star=unit.Star,
                        location=unit.Placement.Location.Kind,col=col,row=row,benchSlot=unit.Placement.Location.BenchSlot};
                }).ToArray(),
                augments=Augments.Capture(),items=Items.Capture()
            };
        }

        public static RunState Restore(DataRepository data,RunSaveData saved)
        {
            if(saved==null || saved.saveVersion!=1 || saved.gridLayoutVersion!=1 || saved.dataVersion!=data.Version
                || !data.Rounds.ContainsKey(saved.round) || saved.level<1 || saved.level>10
                || saved.gold<0 || saved.lives<0 || saved.lives>data.IntRule(EconomyRule.StartLives)
                || saved.xp<0 || saved.level==10 && saved.xp!=0
                || saved.level<10 && saved.xp>=data.RequiredXp(saved.level)
                || saved.winStreak<0 || saved.winStreak>saved.round
                || saved.phase!=RunSavePhase.Preparation && saved.phase!=RunSavePhase.Result
                || saved.phase==RunSavePhase.Preparation && saved.isOver
                || saved.phase==RunSavePhase.Result && saved.isOver!=(saved.lives==0 || saved.round==data.Rounds.Count)
                || saved.wonRun && (!saved.isOver || saved.round!=data.Rounds.Count)
                || !saved.isOver && saved.lives==0
                || saved.shop==null || saved.shop.Length!=5 || saved.stock==null || saved.units==null || saved.augments==null
                || saved.units.Length>saved.level+PlacementService.BenchCapacity || saved.nextInstanceId<=0)
                throw new ArgumentException("저장 형식 또는 게임 데이터 버전이 맞지 않습니다.");
            RunState state=new RunState(data,new RunRandom(saved.randomSeed,saved.randomDrawCount),AugmentState.Restore(data.Augments,saved.augments))
            {
                Round=saved.round,Gold=saved.gold,Lives=saved.lives,Level=saved.level,Xp=saved.xp,
                WinStreak=saved.winStreak,CanPrepare=saved.phase==RunSavePhase.Preparation,
                IsOver=saved.isOver,WonRun=saved.wonRun,LastResult=saved.lastResult,
                nextId=saved.nextInstanceId,settledRound=saved.phase==RunSavePhase.Result?saved.round:saved.round-1
            };
            state.Placement.SetPlayerLevel(state.Level);
            foreach(SavedStock entry in saved.stock)
            {
                if(entry==null || !data.Units.ContainsKey(entry.definitionId) || entry.available<0
                    || state.stock.ContainsKey(entry.definitionId)) throw new ArgumentException("저장된 기물 재고가 올바르지 않습니다.");
                state.stock.Add(entry.definitionId,entry.available);
            }
            if(state.stock.Count!=data.Units.Count || saved.shop.Any(id=>id!=0 && !data.Units.ContainsKey(id)))
                throw new ArgumentException("저장된 상점 정보가 올바르지 않습니다.");
            Array.Copy(saved.shop,state.shop,5);
            foreach(SavedUnit entry in saved.units)
            {
                if(entry==null || !data.Units.ContainsKey(entry.definitionId) || entry.instanceId<=0 || entry.instanceId>=state.nextId
                    || state.owned.Any(unit=>unit.Placement.InstanceId==entry.instanceId) || entry.star!=1 && entry.star!=2
                    || entry.location!=UnitLocationKind.Board && entry.location!=UnitLocationKind.Bench)
                    throw new ArgumentException("저장된 유닛 정보가 올바르지 않습니다.");
                OwnedUnit unit=new OwnedUnit(entry.definitionId,entry.instanceId) { Star=entry.star };
                UnitLocation location=entry.location==UnitLocationKind.Board ? UnitLocation.OnBoard(HexCoord.FromOffset(entry.col,entry.row)) : UnitLocation.OnBench(entry.benchSlot);
                if(!state.Placement.TryRegisterUnit(unit.Placement,location)) throw new ArgumentException("저장된 유닛 배치가 올바르지 않습니다.");
                state.owned.Add(unit);
            }
            foreach(UnitDefinition definition in data.Units.Values)
                if(state.stock[definition.Id]+state.shop.Count(id=>id==definition.Id)
                    +state.owned.Where(unit=>unit.DefinitionId==definition.Id).Sum(unit=>unit.Star==2?3:1)!=data.PoolCopies(definition.Cost))
                    throw new ArgumentException("기물 재고와 상점 예약·보유 수가 일치하지 않습니다.");
            int[] completed=saved.augments.completedRounds;
            if(completed.Any(round=>!data.Rounds.ContainsKey(round) || !data.Rounds[round].OffersAugment || round>state.Round)
                || data.Rounds.Values.Any(round=>round.Id<state.Round && round.OffersAugment && !completed.Contains(round.Id))
                || state.Augments.OfferedRound<0 || state.Augments.OfferedRound>state.Round
                || state.Augments.HasPending && (!state.CanPrepare || state.Augments.OfferedRound!=state.Round || !data.Rounds[state.Round].OffersAugment || completed.Contains(state.Round))
                || data.Rounds[state.Round].OffersAugment && !completed.Contains(state.Round) && !state.Augments.HasPending
                || saved.augments.randomSeed!=(saved.randomSeed^0x41A96B))
                throw new ArgumentException("저장된 증강 진행 이력이 일치하지 않습니다.");
            state.Items=ItemInventoryState.Restore(data.Items,saved.items,saved.stageId,saved.randomSeed^0x59B17,
                state.owned.Select(unit=>unit.Placement.InstanceId).ToArray(),saved.round);
            return state;
        }
        public bool TryEquipItem(int itemInstanceId,int unitInstanceId)
        {
            if(!Ready() || !owned.Any(unit=>unit.Placement.InstanceId==unitInstanceId)
                || !Items.TryEquip(itemInstanceId,unitInstanceId)) return Fail("준비 단계의 아군에게 빈 장착 칸이 필요합니다.");
            Notify(); return true;
        }
        public bool TryUnequipItem(int itemInstanceId)
        {
            if(!Ready() || !Items.TryUnequip(itemInstanceId)) return Fail("준비 단계에서 장착한 아이템만 해제할 수 있습니다.");
            Notify(); return true;
        }
        public int Stock(int definitionId)=>stock[definitionId];
        private bool Ready()=>CanPrepare && !IsOver && !Augments.HasPending;
        public bool TrySelectAugment(int id)
        {
            if(!CanPrepare || IsOver || !Augments.TrySelect(Round,id,out AugmentDefinition definition)) return false;
            if(definition.Effect==AugmentEffect.ImmediateGold) Gold+=(int)definition.Value;
            Notify(); return true;
        }
        private bool Fail(string message) { LastError=message; return false; }
        private void Notify() { LastError=null; Changed?.Invoke(); }
        public bool TryRefresh()
        {
            if(!Ready()) return Fail("준비 단계에서 새로고침할 수 있습니다.");
            int cost=Data.IntRule(EconomyRule.RefreshCost);
            if(Gold<cost) return Fail("골드가 부족합니다.");
            Gold-=cost; Reroll(); Notify(); return true;
        }
        public bool TryBuy(int slot)
        {
            if(!Ready() || slot<0 || slot>=shop.Length || shop[slot]==0) return Fail("구매할 수 없는 상점입니다.");
            int id=shop[slot], cost=Data.Units[id].Cost;
            if(Gold<cost) return Fail("골드가 부족합니다.");
            OwnedUnit[] matches=owned.Where(x=>x.DefinitionId==id && x.Star==1)
                .OrderBy(x=>x.Placement.Location.Kind==UnitLocationKind.Board ? 0:1)
                .ThenBy(x=>PositionOrder(x.Placement.Location)).ThenBy(x=>x.Placement.InstanceId).Take(2).ToArray();
            int free=Placement.FindFreeBenchSlot();
            if(free<0 && matches.Length<2) return Fail("대기석이 가득 찼습니다.");
            // 변경 조건을 모두 확인한 뒤 예약 1개와 보유 2개를 한 번에 합성.
            if(matches.Length==2)
            {
                Items.MergeUnits(matches[0].Placement.InstanceId,matches[1].Placement.InstanceId);
                Board.TryUnregister(matches[1].Placement); owned.Remove(matches[1]); matches[0].Star=2;
            }
            else
            {
                OwnedUnit unit=new OwnedUnit(id,nextId);
                if(!Placement.TryRegisterUnit(unit.Placement,UnitLocation.OnBench(free))) return Fail("구매한 기물을 배치할 수 없습니다.");
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
            if(!Ready()) return Fail("준비 단계에서 판매할 수 있습니다.");
            OwnedUnit unit=owned.FirstOrDefault(x=>x.Placement.InstanceId==instanceId);
            if(unit==null) return Fail("판매할 기물을 선택해 주세요.");
            int copies=unit.Star==2?3:1, price=SalePrice(instanceId);
            if(!Board.TryUnregister(unit.Placement)) return Fail("기물의 배치 정보를 찾을 수 없습니다.");
            Items.ReturnFromUnit(instanceId);
            owned.Remove(unit); stock[unit.DefinitionId]+=copies; Gold+=price;
            Notify(); return true;
        }
        public int SalePrice(int instanceId)
        {
            OwnedUnit unit=owned.FirstOrDefault(value=>value.Placement.InstanceId==instanceId);
            if(unit==null) return 0;
            int cost=Data.Units[unit.DefinitionId].Cost;
            return unit.Star==2 ? cost*3-1 : cost;
        }
        public bool TryBuyXp()
        {
            if(!Ready() || Level==10) return Fail("경험치를 구매할 수 없습니다.");
            int cost=Data.IntRule(EconomyRule.XpCost);
            if(Gold<cost) return Fail("골드가 부족합니다.");
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
            Items.ClearResult();
            if(won) Items.GrantVictory(Round);
            IsOver=Lives<=0 || Round==Data.Rounds.Count;
            WonRun=IsOver && won && Round==Data.Rounds.Count;
            LastResult=$"{(won?"승리":"패배")}   +{income}골드\n이자 {interest} · 연승 {streak} · 증강 {augmentGold}";
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
            IReadOnlyList<int> rates=Data.ShopWeights(Level);
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
            UnitDefinition[] candidates=Data.Units.Values.Where(x=>x.Cost==tier && stock[x.Id]>0).OrderBy(x=>x.Id).ToArray();
            int copyPick=random.Next(candidates.Sum(x=>stock[x.Id]));
            foreach(UnitDefinition unit in candidates)
            {
                copyPick-=stock[unit.Id]; if(copyPick>=0) continue;
                stock[unit.Id]--; return unit.Id;
            }
            throw new InvalidOperationException("기물 추첨 실패.");
        }
    }
}
