using System;
using AutoBattler.Battle.Combat;
using System.Collections.Generic;
using System.Linq;
using AutoBattler.Data.Snapshot;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Data.Augments;
using AutoBattler.Battle.Units;

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
        public bool HasPendingRoundReward { get; private set; }
        public bool CanTradeUnits => (!IsOver || HasPendingRoundReward) && !Augments.HasPending;
        // 상점 갱신·경험치 구매는 전투·결과 대기·증강 선택 대기에도 허용.
        public bool CanUseEconomy => !IsOver || HasPendingRoundReward;
        public bool CanManageRoster => CanTradeUnits && (CanPrepare || !IsSettled || HasPendingRoundReward);
        public bool ShopLocked { get; private set; }
        public string LastError { get; private set; }
        public string LastResult { get; private set; }
        public BattleReport LastBattleReport { get; private set; }
        internal void SetBattleReport(BattleReport report) => LastBattleReport=report;
        public bool LastBattleWon => IsSettled && WinStreak > 0;
        public IReadOnlyList<OwnedUnit> Units => owned.AsReadOnly();
        public IReadOnlyList<int> Shop => Array.AsReadOnly(shop);
        public event Action Changed;
        private readonly List<OwnedUnit> owned=new List<OwnedUnit>();
        private readonly Dictionary<int,int> stock=new Dictionary<int,int>();
        private readonly int[] shop=new int[5];
        private readonly RunRandom random;
        private readonly HashSet<int> availableUnitIds;
        public IReadOnlyList<int> AvailableUnitIds=>Array.AsReadOnly(availableUnitIds.OrderBy(id=>id).ToArray());
        public bool IsUnitAvailable(int unitId)=>availableUnitIds.Contains(unitId);
        private int nextId=1, settledRound;
        private bool pendingRoundWon;
        private readonly Dictionary<int,bool> roundOutcomes=new Dictionary<int,bool>();
        public bool TryGetRoundOutcome(int roundId,out bool won)=>roundOutcomes.TryGetValue(roundId,out won);
        public RunState(DataRepository data,int seed,int stageId=1,IEnumerable<int> availableUnitIds=null)
            :this(data.ForStage(stageId),new RunRandom(seed),new AugmentState(data.Augments,seed),availableUnitIds ?? InitialUnits(data))
        {
            Items=new ItemInventoryState(data.Items,stageId,seed^0x59B17,data.ItemRecipes);
            Gold=data.IntRule(EconomyRule.StartGold); Lives=data.IntRule(EconomyRule.StartLives);
            foreach(UnitDefinition unit in data.Units.Values) stock.Add(unit.Id,data.PoolCopies(unit.Cost));
            int starter=Draw(new int[]{100,0,0,0,0});
            if(starter==0) throw new InvalidOperationException("시작용 1코스트 기물 필요.");
            OwnedUnit initial=new OwnedUnit(starter,nextId++);
            Placement.TryRegisterUnit(initial.Placement,UnitLocation.OnBench(0)); owned.Add(initial);
            Reroll();
            Augments.EnterRound(Round,Data.Rounds[Round].OffersAugment);
        }
        private static IEnumerable<int> InitialUnits(DataRepository data)=>data.InitialUnlockedUnitIds;
        private RunState(DataRepository data,RunRandom random,AugmentState augments,IEnumerable<int> availableUnits)
        { Data=data; this.random=random; Augments=augments; Placement=new PlacementService(Board);
            availableUnitIds=new HashSet<int>(availableUnits);
            if(availableUnitIds.Count==0 || availableUnitIds.Any(id=>!data.Units.ContainsKey(id))) throw new ArgumentException("사용 가능한 유닛 목록 오류.");
            Items=new ItemInventoryState(data.Items,1,random.Seed^0x59B17,data.ItemRecipes); }

        public RunSaveData Capture(int stageId,bool augmentWindowOpen,bool replayCombat=false)
        {
            if(stageId!=Items.StageId) throw new InvalidOperationException("현재 도전의 스테이지와 아이템 스테이지 불일치.");
            if(!CanPrepare && !IsSettled && !replayCombat) throw new InvalidOperationException("전투 중에는 전투 직전 저장 상태를 유지합니다.");
            if(owned.Any(unit=>!Board.HasValidLocation(unit.Placement) || !Placement.IsValidLocation(unit.Placement.Location)))
                throw new InvalidOperationException("배치가 해제된 유닛은 저장할 수 없습니다.");
            return new RunSaveData
            {
                dataVersion=Data.Version, stageId=stageId, roundsPerSegment=Data.RoundsPerSegment,savedAtUtc=DateTime.UtcNow.ToString("O"),
                // 전투 중 경제·장비 변경은 현재 논리 배치로 준비 단계 재시작 저장.
                phase=IsSettled?RunSavePhase.Result:RunSavePhase.Preparation,
                round=Round,gold=Gold,lives=Lives,level=Level,xp=Xp,winStreak=WinStreak,nextInstanceId=nextId,
                isOver=IsOver,wonRun=WonRun,lastResult=LastResult,augmentWindowOpen=augmentWindowOpen,lastBattleReport=LastBattleReport,
                shopLocked=ShopLocked,
                randomSeed=random.Seed,randomDrawCount=random.DrawCount,shop=(int[])shop.Clone(),
                unitDefinitionIds=Data.Units.Keys.OrderBy(id=>id).ToArray(),availableUnitIds=availableUnitIds.OrderBy(id=>id).ToArray(),
                stock=stock.OrderBy(pair=>pair.Key).Select(pair=>new SavedStock {definitionId=pair.Key,available=pair.Value}).ToArray(),
                units=owned.Select(unit=>
                {
                    unit.Placement.Location.Cell.ToOffset(out int col,out int row);
                    return new SavedUnit {definitionId=unit.DefinitionId,instanceId=unit.Placement.InstanceId,star=unit.Star,
                        location=unit.Placement.Location.Kind,col=col,row=row,benchSlot=unit.Placement.Location.BenchSlot};
                }).ToArray(),
                augments=Augments.Capture(),items=Items.Capture(),
                hasPendingRoundReward=HasPendingRoundReward,pendingRoundWon=pendingRoundWon,
                roundOutcomes=roundOutcomes.OrderBy(pair=>pair.Key).Select(pair=>new SavedRoundOutcome {roundId=pair.Key,won=pair.Value}).ToArray()
            };
        }

        public static RunState Restore(DataRepository data,RunSaveData saved,IEnumerable<int> availableUnitIds=null)
        {
            if(saved!=null && saved.stageId>0) data=data.ForStage(saved.stageId);
            saved=RunSaveMigration.Adapt(data,saved);
            if(saved==null || saved.saveVersion!=1 || saved.gridLayoutVersion!=1 || !data.IsSaveCompatible(saved.dataVersion)
                || !data.Rounds.ContainsKey(saved.round) || saved.level<1 || saved.level>10
                || saved.gold<0 || saved.lives<0 || saved.lives>data.IntRule(EconomyRule.StartLives)
                || saved.xp<0 || saved.level==10 && saved.xp!=0
                || saved.level<10 && saved.xp>=data.RequiredXp(saved.level)
                || saved.winStreak<0 || saved.winStreak>saved.round
                || saved.phase!=RunSavePhase.Preparation && saved.phase!=RunSavePhase.Result
                || saved.phase==RunSavePhase.Preparation && saved.isOver
                || saved.hasPendingRoundReward && saved.phase!=RunSavePhase.Result
                || saved.phase==RunSavePhase.Result && saved.isOver!=(saved.lives==0 || saved.round==data.Rounds.Count)
                || saved.wonRun && (!saved.isOver || saved.round!=data.Rounds.Count)
                || !saved.isOver && saved.lives==0
                || saved.shop==null || saved.shop.Length!=5 || saved.stock==null || saved.units==null || saved.augments==null
                || saved.units.Length>saved.level+PlacementService.BenchCapacity || saved.nextInstanceId<=0)
                throw new ArgumentException("저장 형식 또는 게임 데이터 버전이 맞지 않습니다.");
            if(saved.availableUnitIds!=null && (saved.availableUnitIds.Distinct().Count()!=saved.availableUnitIds.Length
                || saved.availableUnitIds.Any(id=>!data.Units.ContainsKey(id)))) throw new ArgumentException("저장된 사용 가능 유닛 목록 오류.");
            RunState state=new RunState(data,new RunRandom(saved.randomSeed,saved.randomDrawCount),AugmentState.Restore(data.Augments,saved.augments),
                availableUnitIds ?? saved.availableUnitIds ?? InitialUnits(data))
            {
                Round=saved.round,Gold=saved.gold,Lives=saved.lives,Level=saved.level,Xp=saved.xp,
                WinStreak=saved.winStreak,CanPrepare=saved.phase==RunSavePhase.Preparation,
                IsOver=saved.isOver,WonRun=saved.wonRun,LastResult=saved.lastResult,LastBattleReport=saved.lastBattleReport,
                ShopLocked=saved.shopLocked,
                nextId=saved.nextInstanceId,settledRound=saved.phase==RunSavePhase.Result?saved.round:saved.round-1,
                // 이전 저장은 대기 보상이 없으므로 이미 지급된 결과로 복원.
                HasPendingRoundReward=saved.hasPendingRoundReward,pendingRoundWon=saved.pendingRoundWon
            };
            state.Placement.SetPlayerLevel(state.Level);
            int lastCompleted=saved.phase==RunSavePhase.Result?saved.round:saved.round-1;
            if(saved.roundOutcomes!=null)
            {
                foreach(SavedRoundOutcome outcome in saved.roundOutcomes)
                {
                    if(outcome==null || !data.Rounds.ContainsKey(outcome.roundId) || outcome.roundId>lastCompleted
                        || state.roundOutcomes.ContainsKey(outcome.roundId)) throw new ArgumentException("저장된 전투 승패 이력이 올바르지 않습니다.");
                    state.roundOutcomes.Add(outcome.roundId,outcome.won);
                }
            }
            else if(lastCompleted>0)
            {
                // 이전 저장은 확실한 마지막 연승·패배만 복원하고 나머지는 미확인으로 유지.
                if(saved.winStreak==0) state.roundOutcomes.Add(lastCompleted,false);
                else for(int id=Math.Max(1,lastCompleted-saved.winStreak+1);id<=lastCompleted;id++) state.roundOutcomes.Add(id,true);
            }
            if(state.roundOutcomes.TryGetValue(lastCompleted,out bool lastWon) && lastWon!=(saved.winStreak>0))
                throw new ArgumentException("저장된 마지막 승패와 연승 정보가 일치하지 않습니다.");
            foreach(SavedStock entry in saved.stock)
            {
                if(entry==null || !data.Units.ContainsKey(entry.definitionId) || entry.available<0
                    || state.stock.ContainsKey(entry.definitionId)) throw new ArgumentException("저장된 기물 재고가 올바르지 않습니다.");
                state.stock.Add(entry.definitionId,entry.available);
            }
            // 저장 당시의 유닛 목록으로 재고 누락과 콘텐츠 추가를 구분.
            int[] known=saved.unitDefinitionIds ?? Enumerable.Range(1001,8).ToArray();
            if(known.Distinct().Count()!=known.Length || !new HashSet<int>(known).SetEquals(state.stock.Keys)
                || saved.shop.Any(id=>id!=0 && !data.Units.ContainsKey(id)))
                throw new ArgumentException("저장된 상점 정보가 올바르지 않습니다.");
            foreach(UnitDefinition definition in data.Units.Values)
                if(!state.stock.ContainsKey(definition.Id)) state.stock.Add(definition.Id,data.PoolCopies(definition.Cost));
            Array.Copy(saved.shop,state.shop,5);
            foreach(SavedUnit entry in saved.units)
            {
                if(entry==null || !data.Units.ContainsKey(entry.definitionId) || entry.instanceId<=0 || entry.instanceId>=state.nextId
                    || state.owned.Any(unit=>unit.Placement.InstanceId==entry.instanceId) || entry.star<1 || entry.star>UnitStarRules.Maximum
                    || entry.location!=UnitLocationKind.Board && entry.location!=UnitLocationKind.Bench)
                    throw new ArgumentException("저장된 유닛 정보가 올바르지 않습니다.");
                OwnedUnit unit=new OwnedUnit(entry.definitionId,entry.instanceId) { Star=entry.star };
                UnitLocation location=entry.location==UnitLocationKind.Board ? UnitLocation.OnBoard(HexCoord.FromOffset(entry.col,entry.row)) : UnitLocation.OnBench(entry.benchSlot);
                if(!state.Placement.TryRegisterUnit(unit.Placement,location)) throw new ArgumentException("저장된 유닛 배치가 올바르지 않습니다.");
                state.owned.Add(unit);
            }
            foreach(UnitDefinition definition in data.Units.Values)
                if(state.stock[definition.Id]+state.shop.Count(id=>id==definition.Id)
                    +state.owned.Where(unit=>unit.DefinitionId==definition.Id).Sum(unit=>UnitStarRules.Copies(unit.Star))!=data.PoolCopies(definition.Cost))
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
                state.owned.Select(unit=>unit.Placement.InstanceId).ToArray(),saved.round,data.ItemRecipes,state.Augments.Selected);
            if(state.CanPrepare) state.MergePendingUnits();
            // 이전 저장의 보유 기물은 유지하고, 잠긴 상점 예약만 반환 후 다시 추첨.
            for(int slot=0;slot<state.shop.Length;slot++)
                if(state.shop[slot]!=0 && !state.IsUnitAvailable(state.shop[slot]))
                {state.stock[state.shop[slot]]++;state.shop[slot]=0;state.shop[slot]=state.Draw(data.ShopWeights(state.Level));}
            state.RemoveMaxStarOffers();
            return state;
        }
        public bool TryEquipItem(int itemInstanceId,int unitInstanceId)
        {
            if(!CanManageRoster) return Fail("현재 아이템을 장착할 수 없습니다.");
            if(!owned.Any(unit=>unit.Placement.InstanceId==unitInstanceId)) return Fail("아군 유닛에 장착해 주세요.");
            if(Items.Find(itemInstanceId)==null || Items.Find(itemInstanceId).OwnerUnitId!=0) return Fail("보관 중인 아이템을 선택해 주세요.");
            if(!Items.TryEquip(itemInstanceId,unitInstanceId)) return Fail("장착 칸이 가득 찼습니다. 합성 가능한 아이템을 사용해 주세요.");
            Notify(); return true;
        }
        public bool TryFuseItems(int firstItemId,int secondItemId)
        {
            if(!CanManageRoster || !Items.TryFuse(firstItemId,secondItemId,out _)) return Fail("합성 가능한 보관 아이템 두 개가 필요합니다.");
            Notify(); return true;
        }
        public bool TryUnequipItem(int itemInstanceId)
        {
            if((!Ready() && !HasPendingRoundReward) || !Items.TryUnequip(itemInstanceId))
                return Fail("준비 단계 또는 결과 대기 중에 해제할 수 있습니다.");
            Notify(); return true;
        }
        public int Stock(int definitionId)=>stock[definitionId];
        private bool Ready()=>CanPrepare && !IsOver && !Augments.HasPending;
        public bool TrySelectAugment(int id)
        {
            if(!CanPrepare || IsOver || !Augments.TrySelect(Round,id,out AugmentDefinition definition)) return false;
            if(definition.Effect==AugmentEffect.ImmediateGold) Gold+=(int)definition.Value;
            else if(definition.Effect==AugmentEffect.MaterialSupply) Items.GrantMaterials(definition);
            Notify(); return true;
        }
        private bool Fail(string message) { LastError=message; return false; }
        private void Notify() { LastError=null; Changed?.Invoke(); }
        public bool TryRefresh()
        {
            if(!CanUseEconomy) return Fail(null);
            int cost=Data.IntRule(EconomyRule.RefreshCost);
            if(Gold<cost) return Fail("골드가 부족합니다.");
            Gold-=cost; Reroll(); Notify(); return true;
        }
        public bool TryToggleShopLock()
        {
            if(!CanUseEconomy) return false;
            ShopLocked=!ShopLocked;
            Notify(); return true;
        }
        public string BuyFailureReason(int slot)
        {
            if(!CanTradeUnits) return Augments.HasPending ? "증강을 먼저 선택해 주세요." : "현재 기물을 구매할 수 없습니다.";
            if(slot<0 || slot>=shop.Length || shop[slot]==0) return "빈 상점입니다.";
            int id=shop[slot];
            if(!CanOfferUnit(id)) return "현재 구매할 수 없는 유닛입니다.";
            if(Gold<Data.Units[id].Cost) return "골드가 부족합니다.";
            if(Placement.FindFreeBenchSlot()<0 && PurchaseMergeCandidates(id).Length<2) return "대기석이 가득 찼습니다.";
            return null;
        }
        private OwnedUnit[] PurchaseMergeCandidates(int id)=>IsSettled?Array.Empty<OwnedUnit>():
            owned.Where(unit=>unit.DefinitionId==id && unit.Star==1
                && (CanPrepare || unit.Placement.Location.Kind==UnitLocationKind.Bench))
            .OrderBy(unit=>unit.Placement.Location.Kind==UnitLocationKind.Board ? 0:1)
            .ThenBy(unit=>PositionOrder(unit.Placement.Location)).ThenBy(unit=>unit.Placement.InstanceId).Take(2).ToArray();
        public bool TryBuy(int slot)
        {
            string reason=BuyFailureReason(slot);
            if(reason!=null) return Fail(reason);
            int id=shop[slot], cost=Data.Units[id].Cost;
            OwnedUnit[] matches=PurchaseMergeCandidates(id);
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
            shop[slot]=0; Gold-=cost;
            if(!IsSettled) MergeUnitsOfType(id);
            RemoveMaxStarOffers();
            Notify(); return true;
        }
        private static int PositionOrder(UnitLocation location)
        {
            if(location.Kind==UnitLocationKind.Bench) return location.BenchSlot;
            location.Cell.ToOffset(out int col,out int row); return col*7+row;
        }
        public bool TrySell(int instanceId)
        {
            if(!CanTradeUnits) return Fail("현재 판매할 수 없습니다.");
            OwnedUnit unit=owned.FirstOrDefault(x=>x.Placement.InstanceId==instanceId);
            if(unit==null) return Fail("판매할 기물을 선택해 주세요.");
            if(!CanPrepare && !IsSettled && unit.Placement.Location.Kind!=UnitLocationKind.Bench)
                return Fail("전투 중에는 대기석 유닛만 판매할 수 있습니다.");
            int copies=UnitStarRules.Copies(unit.Star), price=SalePrice(instanceId);
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
            return UnitStarRules.SalePrice(cost,unit.Star);
        }
        public bool TryBuyXp()
        {
            if(!CanUseEconomy) return Fail(null);
            if(Level==10) return Fail("최대 레벨입니다.");
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
            roundOutcomes[Round]=won;
            AddXp(Data.IntRule(EconomyRule.RoundXp));
            if(!won) Lives--;
            Items.ClearResult();
            bool finalRound=Round==Data.Rounds.Count;
            IsOver=Lives<=0 || finalRound;
            WonRun=finalRound && won;
            HasPendingRoundReward=true; pendingRoundWon=won;
            LastResult=$"{(won?"승리":"패배")}\n보상은 전투 종료 타이머가 끝나면 지급";
            Notify(); return true;
        }
        public RoundIncome PreviewRoundIncome(bool won)
        {
            if (IsOver && !HasPendingRoundReward) return default;
            int round = IsSettled && !HasPendingRoundReward ? Round + 1 : Round;
            // 승패 확정 후에는 실제 결과로 고정하고, 지급 시점의 잔액으로 이자 계산.
            bool outcome = HasPendingRoundReward ? pendingRoundWon : won;
            int streak = HasPendingRoundReward ? WinStreak : outcome ? WinStreak + 1 : 0;
            int streakGold = streak >= 5 ? 3 : streak == 4 ? 2 : streak == 3 ? 1 : 0;
            int interest = Math.Min(Data.IntRule(EconomyRule.InterestCap), Gold / Data.IntRule(EconomyRule.InterestDivisor));
            int augment = outcome ? Augments.Selected.Where(definition => definition.Effect == AugmentEffect.VictoryGold).Sum(definition => (int)definition.Value) : 0;
            RoundDefinition definition=Data.Rounds[round];
            return new RoundIncome(definition.Reward+(outcome?definition.BossVictoryGold:0), interest, streakGold, augment);
        }
        public bool TryGrantRoundRewards()
        {
            if(!IsSettled || !HasPendingRoundReward) return false;
            bool won=pendingRoundWon;
            RoundIncome income = PreviewRoundIncome(won);
            Gold += income.Total;
            if(won) Items.GrantVictory(Round);
            // 알림과 저장 전에 대기 상태를 해제하여 재진입·이어하기 중복 지급 차단.
            HasPendingRoundReward=false; pendingRoundWon=false;
            LastResult=$"{(won?"승리":"패배")}   +{income.Total}골드\n이자 {income.InterestGold} · 연승 {income.StreakGold} · 증강 {income.AugmentGold}";
            Notify(); return true;
        }
        public bool TryNextRound()
        {
            if(IsOver || settledRound!=Round || CanPrepare || HasPendingRoundReward) return false;
            Round++; CanPrepare=true; MergePendingUnits(); RemoveMaxStarOffers();
            if(!ShopLocked) Reroll();
            Augments.EnterRound(Round,Data.Rounds[Round].OffersAugment); Notify(); return true;
        }
        private void MergePendingUnits()
        {
            foreach(int definition in owned.Select(unit=>unit.DefinitionId).Distinct().ToArray()) MergeUnitsOfType(definition);
        }
        private void MergeUnitsOfType(int definition)
        {
            for(int star=1;star<UnitStarRules.Maximum;star++)
            {
                while(true)
                {
                    OwnedUnit[] matches=owned.Where(unit=>unit.DefinitionId==definition && unit.Star==star
                            && (CanPrepare || unit.Placement.Location.Kind==UnitLocationKind.Bench))
                        .OrderBy(unit=>unit.Placement.Location.Kind==UnitLocationKind.Board?0:1)
                        .ThenBy(unit=>PositionOrder(unit.Placement.Location)).ThenBy(unit=>unit.Placement.InstanceId).Take(3).ToArray();
                    if(matches.Length<3) break;
                    OwnedUnit survivor=matches[0];
                    foreach(OwnedUnit consumed in matches.Skip(1))
                    {
                        Items.MergeUnits(survivor.Placement.InstanceId,consumed.Placement.InstanceId);
                        Board.TryUnregister(consumed.Placement); owned.Remove(consumed);
                    }
                    survivor.Star=star+1;
                }
            }
        }
        private void Reroll()
        {
            for(int i=0;i<shop.Length;i++) { if(shop[i]!=0) stock[shop[i]]++; shop[i]=0; }
            IReadOnlyList<int> rates=Data.ShopWeights(Level);
            for(int i=0;i<shop.Length;i++) shop[i]=Draw(rates);
        }
        private bool HasMaxStarUnit(int id)=>owned.Any(unit=>unit.DefinitionId==id && unit.Star==UnitStarRules.Maximum);
        private bool CanOfferUnit(int id)=>IsUnitAvailable(id) && !HasMaxStarUnit(id);
        private void RemoveMaxStarOffers()
        {
            for(int slot=0;slot<shop.Length;slot++)
            {
                int id=shop[slot];
                if(id==0 || !HasMaxStarUnit(id)) continue;
                stock[id]++; shop[slot]=0;
            }
        }
        private int Draw(IReadOnlyList<int> rates)
        {
            UnitDefinition[] eligible=Data.Units.Values.Where(unit=>CanOfferUnit(unit.Id) && stock[unit.Id]>0).ToArray();
            int total=0;
            for(int cost=1;cost<=5;cost++)
                if(eligible.Any(unit=>unit.Cost==cost)) total+=rates[cost-1];
            if(total==0) return 0;
            int tierPick=random.Next(total), tier=0;
            for(int cost=1;cost<=5;cost++)
            {
                if(!eligible.Any(unit=>unit.Cost==cost)) continue;
                tierPick-=rates[cost-1]; if(tierPick<0) { tier=cost; break; }
            }
            UnitDefinition[] candidates=eligible.Where(unit=>unit.Cost==tier).OrderBy(unit=>unit.Id).ToArray();
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
