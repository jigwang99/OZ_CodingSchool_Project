#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Snapshot;
using AutoBattler.EditorTools.Sheets;
using AutoBattler.Run;
using UnityEditor;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    /// <summary>현재 마감 범위의 경제 보존, 경로·예약과 스냅샷 입력 확인.</summary>
    public static class MilestoneChecks
    {
        private static int checks;
        private static void Expect(bool condition,string message) { if(!condition) throw new Exception(message); checks++; }
        private static GameSnapshot Load()=>JsonUtility.FromJson<GameSnapshot>(File.ReadAllText(SnapshotImporter.OutputPath));
        [MenuItem("PixelTactics/Checks/Run Domain Checks")]
        public static void RunAll()
        {
            checks=0; ImportChecks(); PathChecks(); EconomyChecks();
            var health=new UnitHealthState(100);
            Expect(health.TakeDamage(DamageResolver.Calculate(20,100))==10,"방어 피해 공식");
            Expect(health.TakeDamage(DamageResolver.Calculate(1,100))==1,"최소 피해"); health.TakeDamage(10000);
            Expect(!health.IsAlive && health.TakeDamage(20)==0,"사망 후 피해 중복"); health.RestoreFullHealth();
            Expect(health.CurrentHp==100,"체력 복원");
            Expect(DamageResolver.Calculate(23,15,1.5f)==30,"치명타 후 방어 적용 및 최종 피해 절삭");
            Debug.Log("DOMAIN_CHECKS_PASS "+checks);
        }
        private static Dictionary<string,CsvTable> Tables()
        {
            return SnapshotImporter.Tabs.ToDictionary(tab=>tab,tab=>new CsvTable(tab,File.ReadAllText("Docs/Sheets/"+tab+".csv")));
        }
        private static void ImportChecks()
        {
            var parsed=SnapshotImporter.Build(Tables());
            Expect(parsed.units.Length==8 && parsed.rounds.Length==12,"초기 CSV 전체 읽기");
            var csv=new CsvTable("Quoted","id,name\r\n1,\"A, \"\"B\"\"\nC\"\r\n");
            Expect(csv.Get(1,"name")=="A, \"B\"\nC","CSV 쉼표·따옴표·줄바꿈");
            var api=GoogleSheetImporterWindow.ParseApiRows("{\"valueRanges\":[{\"range\":\"Units!A1:C2\",\"values\":[[\"id\",\"number\",\"flag\"],[1001,0.3,true]]}]} ")[0];
            Expect(api.Name=="Units" && api.Int(1,"id")==1001 && api.Float(1,"number")==.3f && api.Bool(1,"flag"),"Sheets API 원시 숫자·불리언 해석");
            string original=File.ReadAllText(SnapshotImporter.OutputPath);
            var duplicate=Tables(); duplicate["Units"].Rows.Add(new List<string>(duplicate["Units"].Rows[1]));
            Reject(duplicate,"중복 ID");
            var rate=Tables(); rate["ShopRates"].Rows[1][1]="99"; Reject(rate,"확률 합계");
            var reference=Tables(); reference["RoundUnits"].Rows[1][2]="999999"; Reject(reference,"잘못된 참조");
            var occupied=Tables(); occupied["RoundUnits"].Rows[3][4]=occupied["RoundUnits"].Rows[2][4]; occupied["RoundUnits"].Rows[3][5]=occupied["RoundUnits"].Rows[2][5]; Reject(occupied,"적 배치 중복");
            var missing=Tables(); missing.Remove("Levels"); Reject(missing,"표 누락");
            Expect(File.ReadAllText(SnapshotImporter.OutputPath)==original,"가져오기 실패 시 기존 JSON 유지");
        }
        private static void Reject(Dictionary<string,CsvTable> tables,string name)
        {
            bool rejected=false; try { SnapshotImporter.Build(tables); } catch(FormatException) { rejected=true; }
            Expect(rejected,name+" 거부");
        }
        private static void PathChecks()
        {
            for(int col=0;col<8;col++) for(int row=0;row<7;row++)
            {
                var cell=HexCoord.FromOffset(col,row); cell.ToOffset(out int c,out int r);
                Expect(c==col && r==row,"좌표 왕복");
                for(int i=0;i<6;i++) Expect(cell.DistanceTo(cell.GetNeighbor(i))==1,"6방향 거리");
            }
            var board=new CombatBoard(); var start=HexCoord.FromOffset(0,0); var target=HexCoord.FromOffset(4,3);
            Expect(board.TryAdd(1,start) && board.TryAdd(2,target),"전투 점유 등록");
            var first=HexPathfinder.Find(board,1,start,target,1); var second=HexPathfinder.Find(board,1,start,target,1);
            Expect(first!=null && first.SequenceEqual(second) && first.Last().DistanceTo(target)==1,"A* 재현·사거리 목표");
            Expect(board.TryReserve(1,start,first[0]),"다음 셀 예약");
            Expect(!board.IsFree(start,2) && !board.IsFree(first[0],2),"이동 중 현재·다음 점유 유지");
            Expect(!board.TryAdd(3,first[0]),"예약 셀 경쟁 차단");
            Expect(board.TryArrive(1,start,first[0]) && board.IsFree(start,2),"도착 후 이전 셀 해제");
            board.Release(1); Expect(board.IsFree(first[0],2),"사망 점유 해제");
            var blocked=new CombatBoard(); blocked.TryAdd(1,start); blocked.TryAdd(2,target);
            for(int i=0;i<6;i++) { var neighbor=start.GetNeighbor(i); if(CombatBoard.IsInside(neighbor)) blocked.TryAdd(i+3,neighbor); }
            Expect(HexPathfinder.Find(blocked,1,start,target,1)==null,"접근 불가 경로");
        }
        private static void EconomyChecks()
        {
            var data=Load(); data.units=data.units.Where(x=>x.cost==1).ToArray();
            data.poolCounts[0].copiesPerUnit=90;
            data.economy.First(x=>x.ruleId==(int)EconomyRule.StartGold).value=50000;
            var state=new RunState(new DataRepository(data),1);
            Conservation(state);
            int initial=state.Units[0].DefinitionId;
            if(initial!=1001) { Expect(state.TrySell(state.Units[0].Placement.InstanceId),"시작 기물 판매"); Buy(state,1001); }
            for(int i=0;i<18;i++) Buy(state,1002);
            Expect(state.Units.Count(x=>x.DefinitionId==1002 && x.Star==2)==6,"연속 2성 합성");
            Buy(state,1001); Expect(state.Units.Count==8,"대기석 8칸 채우기");
            // 전장 기물을 우선 보존하는 합성 규칙.
            var preserved=state.Units.First(x=>x.DefinitionId==1001);
            Expect(state.Placement.TryMoveUnit(preserved.Placement,HexCoord.FromOffset(0,0)).Success,"전장 배치");
            var other=state.Units.First(x=>x.DefinitionId==1002);
            Expect(state.Placement.TryMoveUnit(preserved.Placement,other.Placement.Location).Success,"전장·대기석 교환");
            Expect(state.Placement.TryMoveUnit(preserved.Placement,HexCoord.FromOffset(0,0)).Success,"다시 교환");
            // 전장 1 + 대기석 8 상태에서도 3번째 구매가 합성으로 공간 확보.
            Buy(state,1002); Expect(state.Units.Count==9,"전장과 대기석 개수");
            Buy(state,1001); Expect(state.Units.Any(x=>ReferenceEquals(x,preserved) && x.Star==2),"합성에서 전장 기물 보존");
            Conservation(state);
            Buy(state,1001);
            int failedSlot=EnsureSlot(state,1002),gold=state.Gold,owned=state.Units.Count;
            Expect(!state.TryBuy(failedSlot) && state.Gold==gold && state.Units.Count==owned && state.Shop[failedSlot]==1002,"가득 찬 대기석 구매 실패 원상 유지");
            Expect(state.TrySell(preserved.Placement.InstanceId),"2성 판매"); Expect(state.Gold==gold+2,"2성 판매 가격"); Conservation(state);
            Expect(state.TryBuyXp() && state.Level==2 && state.Xp==2,"구매 XP 이월");
            while(state.Level<10) Expect(state.TryBuyXp(),"레벨 상승");
            gold=state.Gold; Expect(!state.TryBuyXp() && state.Gold==gold,"최대 레벨 구매 차단");
            state.CanPrepare=false; gold=state.Gold;
            Expect(state.TrySettle(true) && state.Gold==gold+8,"기본 보상 + 이자");
            gold=state.Gold; Expect(!state.TrySettle(true) && state.Gold==gold,"중복 정산 방지");
            Expect(state.TryNextRound() && state.CanPrepare && state.Round==2,"다음 준비 단계"); Conservation(state);
            int lives=state.Lives; state.CanPrepare=false; Expect(state.TrySettle(false) && state.Lives==lives-1 && state.WinStreak==0,"패배 정산");
            Expect(state.TryNextRound(),"패배 후 다음 라운드");
            // 모든 1코스트 재고가 고갈되면 빈 상점. 확률 0인 코스트는 추첨하지 않음.
            var exhausted=Load(); exhausted.units=exhausted.units.Where(x=>x.cost==1).Take(1).ToArray(); exhausted.poolCounts[0].copiesPerUnit=1;
            var empty=new RunState(new DataRepository(exhausted),1);
            Expect(empty.Shop.All(x=>x==0),"재고 고갈 빈 상점");
        }
        private static int EnsureSlot(RunState state,int id)
        {
            for(int tries=0;tries<1000;tries++)
            {
                for(int i=0;i<5;i++) if(state.Shop[i]==id) return i;
                if(!state.TryRefresh()) break;
            }
            throw new Exception("테스트 상점 추첨 실패");
        }
        private static void Buy(RunState state,int id) { Expect(state.TryBuy(EnsureSlot(state,id)),"구매 "+id+": "+state.LastError); Conservation(state); }
        private static void Conservation(RunState state)
        {
            foreach(var unit in state.Data.Units.Values)
            {
                int total=state.Stock(unit.Id)+state.Shop.Count(x=>x==unit.Id)+state.Units.Where(x=>x.DefinitionId==unit.Id).Sum(x=>x.Star==2?3:1);
                Expect(total==state.Data.PoolCopies(unit.Cost),"재고 보존 "+unit.Id);
            }
        }
    }
}
#endif
