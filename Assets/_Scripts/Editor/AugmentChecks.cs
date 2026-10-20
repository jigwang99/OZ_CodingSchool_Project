#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Augments;
using AutoBattler.Data.Snapshot;
using AutoBattler.EditorTools.Sheets;
using AutoBattler.Presentation.Battle;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace AutoBattler.EditorTools
{
    public static class AugmentChecks
    {
        private static int checks;
        private static void Expect(bool value,string message)
        { if(!value) throw new Exception("Augment check: "+message); checks++; }
        private static GameSnapshot Snapshot()=>JsonUtility.FromJson<GameSnapshot>(File.ReadAllText(SnapshotImporter.OutputPath));
        [MenuItem("PixelTactics/Checks/Run Augment Checks")]
        public static void BuildAndPlay()
        {
            AugmentSetup.Connect();
            SelectedAugmentsSetup.Connect();
            UnitInfoSetup.Connect();
            var setup=ProjectSetupSettings.Current;
            string before=File.ReadAllText(setup.HUDPath); AugmentSetup.Connect();
            Expect(before==File.ReadAllText(setup.HUDPath),"재연결 시 HUD 중복·변경 없음");
            Expect(setup.BattleHUD.AugmentPanel!=null && setup.AugmentPanel.Cards.Count==3
                && setup.AugmentPanel.GetComponentsInChildren<Canvas>(true).Length==0,"공유 Canvas와 세 카드 참조");
            RunDomain(); SynergyUIChecks.BuildAndPlay();
        }
        private static void RunDomain()
        {
            checks=0;
            var tables=SnapshotImporter.Tabs.ToDictionary(tab=>tab,tab=>new CsvTable(tab,File.ReadAllText("Docs/Sheets/"+tab+".csv")));
            var data=SnapshotImporter.Build(tables); var repo=new DataRepository(data);
            Expect(repo.Augments.Count==8 && data.schemaVersion==4,"스키마 4 강화 표 8종");
            var old=Snapshot(); old.schemaVersion=3; old.augments=null;
            Expect(new DataRepository(old).Augments.Count==0,"이전 스키마 3 호환");
            foreach(int seed in Enumerable.Range(1,12))
            {
                var state=new RunState(repo,seed); var control=new RunState(repo,seed);
                for(int round=1;round<=12;round++)
                {
                    if(state.Augments.HasPending)
                    {
                        var offers=state.Augments.OfferedIds.ToArray();
                        Expect(new[]{2,5,9}.Contains(round) && offers.Length==3 && offers.Distinct().Count()==3
                            && !offers.Intersect(state.Augments.SelectedIds).Any(),"지정 시점에 미선택 강화 세 개");
                        var shop=state.Shop.ToArray(); int gold=state.Gold;
                        Expect(!state.TryBuy(0) && !state.TryRefresh() && !state.TryBuyXp()
                            && !state.TrySell(state.Units[0].Placement.InstanceId) && gold==state.Gold,"선택 전 경제 명령 도메인 차단");
                        state.Augments.EnterRound(round,true);
                        Expect(offers.SequenceEqual(state.Augments.OfferedIds) && shop.SequenceEqual(control.Shop),"재진입 후보 유지와 상점 난수 분리");
                        Expect(!state.TrySelectAugment(-1),"제안 외 선택 차단");
                        int id=offers[0]; Expect(state.TrySelectAugment(id),"유효한 선택 확정");
                        int reward=repo.Augments[id].Effect==AugmentEffect.ImmediateGold?8:0;
                        Expect(state.Gold==gold+reward && !state.TrySelectAugment(id) && state.Gold==gold+reward,"즉시 보상·선택 한 번 처리");
                        control.TrySelectAugment(control.Augments.OfferedIds[0]);
                    }
                    state.CanPrepare=control.CanPrepare=false;
                    int before=state.Gold; bool won=round!=3;
                    Expect(state.TrySettle(won) && !state.TrySettle(won),"승패 정산 한 번 처리");
                    int interest=Math.Min(repo.IntRule(EconomyRule.InterestCap),before/repo.IntRule(EconomyRule.InterestDivisor));
                    int streak=state.WinStreak>=5?3:state.WinStreak==4?2:state.WinStreak==3?1:0;
                    int bonus=won?state.Augments.Selected.Where(x=>x.Effect==AugmentEffect.VictoryGold).Sum(x=>(int)x.Value):0;
                    Expect(state.Gold==before+repo.Rounds[round].Reward+interest+streak+bonus,"승리 추가 골드와 패배 미지급");
                    control.TrySettle(won);
                    if(round<12) { Expect(state.TryNextRound() && control.TryNextRound(),"다음 라운드 진행"); }
                }
                Expect(state.Augments.SelectedIds.Count==3 && state.IsOver && state.WonRun,"세 번 선택 후 최종 결과");
            }
            Debug.Log("AUGMENT_DOMAIN_PASS "+checks);
        }
        public static async UniTask CheckSelection(RunController run,RunHUD hud,Mouse mouse,Unit unit)
        {
            checks=0; var panel=hud.AugmentPanel; var original=unit.View.State.Cell.Value;
            var offered=run.State.Augments.OfferedIds.ToArray(); int gold=run.State.Gold;
            Expect(panel!=null && panel.IsOpen && run.Flow.CurrentPhase==BattlePhase.AugmentSelection
                && !run.Flow.CanPlaceUnits && !run.Drag.enabled,"선택 창 자동 표시와 드래그 차단");
            Expect(panel.Cards.Select(card=>card.DisplayedId).SequenceEqual(offered),"세 후보 카드 표시");
            Expect(!hud.SelectedAugments.CanHover && !hud.SelectedAugments.IsTooltipVisible,"강화 모달에서 보유 증강 호버 차단");
            Expect(hud.SynergyPanel.StatusText.text=="Your deployed army" && run.Message.Contains("Choose an augment"),"선택 대기 중 준비 집계와 입력 안내");
            Expect(!hud.StartBattleButton.interactable && !hud.RefreshButton.interactable && !hud.SellButton.interactable
                && !hud.BuyExperienceButton.interactable && hud.ShopItems.All(item=>!item.BuyButton.IsInteractable()),"열림 상태의 나머지 버튼 잠금");
            Expect(!run.StartBattle() && !run.Buy(0) && !run.SellSelected() && !run.RefreshShop() && !run.BuyXp()
                && run.State.Gold==gold,"열림 상태 명령 우회 차단");
            await UniTask.Delay(250,ignoreTimeScale:true);
            Canvas.ForceUpdateCanvases();
            var pointer=new PointerEventData(EventSystem.current) {position=Camera.main.WorldToScreenPoint(unit.transform.position)};
            var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
            Expect(hits.Count>0 && hits[0].gameObject.transform.IsChildOf(panel.transform),"전체 화면 모달이 뒤쪽 입력 가림");
            foreach(var text in panel.GetComponentsInChildren<TextMeshProUGUI>()) { text.ForceMeshUpdate(); Expect(!text.isTextOverflowing,"강화 라벨 잘림 없음: "+text.text); }
            if(Application.isBatchMode) MilestonePlayChecks.CapturePreview("Augments_Open.png");
            Click(panel.ToggleButton);
            Expect(!panel.IsOpen && panel.ToggleText.text=="Show choices" && run.Flow.CanPlaceUnits && run.Drag.enabled,"닫기 버튼이 열기로 전환하고 배치 허용");
            Expect(hud.SelectedAugments.CanHover,"닫힌 모달에서 보유 증강 확인 허용");
            Expect(run.Message.Contains("unlock the shop and battle"),"닫힌 창에서 남은 선택 의무 안내");
            Expect(run.State.Augments.HasPending && offered.SequenceEqual(run.State.Augments.OfferedIds)
                && !run.SelectAugment(offered[0]),"닫힌 상태의 후보 유지와 숨은 선택 요청 차단");
            Expect(!run.StartBattle() && !run.Buy(0) && !run.SellSelected() && !run.RefreshShop() && !run.BuyXp(),"닫힘 상태에서도 경제·전투 명령 잠금");
            var moved=HexCoord.FromOffset(2,2);
            await Drag(mouse,unit.transform.position,run.Grid.CellToWorld(moved));
            Expect(unit.View.State.Cell==moved && unit.CurrentCell==moved,"닫힌 창에서 실제 마우스 드래그 배치");
            if(Application.isBatchMode) MilestonePlayChecks.CapturePreview("Augments_Closed.png");
            await Drag(mouse,unit.transform.position,run.Grid.CellToWorld(original));
            Expect(unit.View.State.Cell==original,"닫힌 창에서 재배치 복원");
            Click(panel.ToggleButton);
            Expect(panel.IsOpen && !run.Flow.CanPlaceUnits && !run.Drag.enabled
                && offered.SequenceEqual(panel.Cards.Select(card=>card.DisplayedId)),"다시 열어도 동일 후보와 모달 잠금 유지");
            int selected=offered.FirstOrDefault(id=>run.State.Data.Augments[id].Effect==AugmentEffect.StatModifier);
            if(selected==0) selected=offered[0];
            Click(panel.Cards.Single(card=>card.DisplayedId==selected).Button);
            Expect(!panel.gameObject.activeSelf && !run.State.Augments.HasPending && run.Flow.CurrentPhase==BattlePhase.Preparation
                && run.Drag.enabled && hud.RefreshButton.interactable,"선택 완료 후 정상 준비 권한 복원");
            int after=run.State.Gold;
            Expect(!run.SelectAugment(selected) && after==run.State.Gold,"선택 완료 후 재요청 차단");
            await CheckEffects(run);
            await SelectedAugmentsChecks.RunPlay(run,hud,mouse);
            await UnitInfoChecks.RunPlay(run,hud,mouse);
            Debug.Log("AUGMENT_PLAY_PASS "+checks+" modal-toggle-drag-lock-candidates-select-all-effects-pool");
        }
        private static async UniTask CheckEffects(RunController run)
        {
            using var assets=new AssetService(); var root=new GameObject("AugmentProbes");
            var flowRoot=new GameObject("AugmentFlow",typeof(BattleFlowController)); var flow=flowRoot.GetComponent<BattleFlowController>();
            var pool=new UnitPrefabPool(assets,root.transform);
            var prefab=run.Catalog.VisualPrefab(run.State.Data.Units[1001].VisualId);
            await pool.PreloadAsync(prefab,CancellationToken.None); var ally=pool.Rent(prefab); var enemy=pool.Rent(prefab);
            try
            {
                void Configure(Unit value,UnitTeam team,int id)
                {
                    value.Configure(run.State.Data.Units[1001],2,id,team,run.State.Data,flow,run.Grid,run.Bench,HexCoord.FromOffset(team==UnitTeam.Ally?1:5,3));
                    value.gameObject.SetActive(true);
                }
                Configure(ally,UnitTeam.Ally,830001); Configure(enemy,UnitTeam.Enemy,830002);
                var definitions=run.State.Data.Augments.Values.ToArray(); int hp=ally.Health.MaxHp,attack=ally.Stats.AttackDamage; float interval=ally.Stats.AttackCooldown;
                AugmentApplicator.ApplyPersistent(ally,definitions); AugmentApplicator.ApplyPersistent(enemy,definitions);
                int upgraded=ally.Health.MaxHp; AugmentApplicator.ApplyPersistent(ally,definitions);
                Expect(upgraded==Mathf.FloorToInt(hp*1.15f) && ally.Health.MaxHp==upgraded && ally.Health.CurrentHp==upgraded,"체력 강화·2성·중복 적용 후 최대 체력 보존");
                Expect(ally.Stats.AttackDamage==Mathf.FloorToInt(attack*1.15f) && Mathf.Abs(ally.Stats.AttackCooldown-interval/1.15f)<.001f
                    && Mathf.Abs(ally.StatModifiers.SkillPower-1.2f)<.001f,"공격력·공격 속도·스킬 위력 강화");
                var manaSource=new ModifierSource(ModifierSourceKind.OriginTrait,4002);
                ally.Mana.SetStartingBonus(manaSource,20); ally.Mana.ResetForRound();
                Expect(ally.Mana.Current==40 && enemy.Mana.Current==0 && enemy.Health.MaxHp==hp,"시너지 마나 합산과 적에게 강화 미적용");
                // 전투 참가 상태는 별도의 준비 배치로 연결.
                var state=new AutoBattler.Battle.Placement.UnitState(ally.InstanceId); var board=new BoardState();
                board.TryRegister(state,HexCoord.FromOffset(1,3)); ally.View.Bind(state,board); ally.BeginBattle(); enemy.BeginBattle(); flow.TryStartCombat();
                AugmentApplicator.ApplyBattleStart(new[]{ally,enemy},definitions);
                Expect(ally.Effects.Shield==Mathf.FloorToInt(upgraded*.1f) && enemy.Effects.Shield==0 && ally.Mana.Current==40,"강화된 체력 기준 시작 보호막과 마나");
                flow.TryFinishCombat(); Expect(ally.Effects.Shield==0,"전투 종료 시 시작 보호막 정리");
                pool.Return(ally); ally=pool.Rent(prefab); Configure(ally,UnitTeam.Ally,830003);
                Expect(ally.Health.MaxHp==hp && ally.Mana.Starting==0,"풀 재사용 후 이전 강화 출처 제거");
                AugmentApplicator.ApplyPersistent(ally,definitions);
                Expect(ally.Health.MaxHp==upgraded && ally.Mana.Starting==20,"새 유닛·합성 결과에 선택 강화 재적용");
            }
            finally { pool.Return(ally); pool.Return(enemy); await pool.DisposeAsync(); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(flowRoot); }
            Expect(assets.ActiveHandleCount==0,"강화 검사 Addressables 해제");
        }
        private static void Click(Button button)
        {
            Expect(button.IsInteractable(),"사용 가능한 강화 버튼");
            ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        }
        private static async UniTask Drag(Mouse mouse,Vector3 from,Vector3 to)
        {
            Vector2 start=Camera.main.WorldToScreenPoint(from),end=Camera.main.WorldToScreenPoint(to);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=start}); await UniTask.DelayFrame(2);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=start}.WithButton(MouseButton.Left)); await UniTask.DelayFrame(2);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=end}.WithButton(MouseButton.Left)); await UniTask.DelayFrame(2);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=end}); await UniTask.DelayFrame(2);
        }
    }
}
#endif
