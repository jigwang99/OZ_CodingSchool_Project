#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Battle.Units;
using AutoBattler.Presentation.Battle;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AutoBattler.EditorTools
{
    public static class GameEntryChecks
    {
        public const string Key="PixelTactics.GameEntryChecks";
        private static int checks;
        private static void Expect(bool value,string message)
        { if(!value) throw new Exception("Game entry check: "+message); checks++; }
        [MenuItem("PixelTactics/Checks/Run Title and Stage Flow Checks")]
        public static void BuildAndPlay()
        {
            GameEntrySetup.Connect();
            var setup=ProjectSetupSettings.Current;
            string hud=File.ReadAllText(setup.HUDPath),result=File.ReadAllText(setup.ResultPath);
            GameEntrySetup.Connect();
            Expect(hud==File.ReadAllText(setup.HUDPath) && result==File.ReadAllText(setup.ResultPath),"재연결 시 버튼 중복 없음");
            Expect(EditorBuildSettings.scenes[0].path==setup.TitleScenePath && EditorBuildSettings.scenes[1].path==setup.ScenePath,
                "타이틀 첫 장면과 도전 장면 빌드 순서");
            SessionState.SetBool(Key,true); UnitInfoChecks.BuildAndPlay();
        }
        public static async UniTask RunFlowAsync(RunController initialRun)
        {
            checks=0; Time.timeScale=1f;
            var settings=ProjectSetupSettings.Current.Navigation;
            var first=ProjectSetupSettings.Current.FirstStage;
            Expect(settings.DefaultStage==first && first.DisplayName=="1-1","스테이지 1-1 한 개만 등록");
            Expect(await GameNavigation.ReturnToTitleAsync(settings,initialRun),"기존 도전 종료 후 타이틀 이동");
            Expect(initialRun.ActiveAssetHandleCount==0,"장면 이동 전 이전 도전 핸들 해제");
            var menu=await WaitTitle();
            Expect(!menu.IsStageSelectionOpen && menu.OpenStagesButton.gameObject.activeInHierarchy && menu.QuitButton.gameObject.activeInHierarchy,
                "타이틀 시작하기·종료 버튼");
            menu.enabled=false; menu.enabled=true;
            await UniTask.Delay(220,ignoreTimeScale:true);
            CheckLabels(menu);
            if(Application.isBatchMode)
            {
                SynergyUIChecks.Capture(1920,1080,"Title_1920.png");
                SynergyUIChecks.Capture(1280,720,"Title_1280.png");
            }
            var mouse=Mouse.current ?? InputSystem.AddDevice<Mouse>();
            await ClickMouse(mouse,menu.OpenStagesButton);
            if(!menu.IsStageSelectionOpen)
            {
                var hits=new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) {position=mouse.position.ReadValue()},hits);
                Debug.Log($"TITLE_INPUT_DIAGNOSTIC point={mouse.position.ReadValue()} button={menu.OpenStagesButton.transform.position} screen={Screen.width}x{Screen.height} active={menu.OpenStagesButton.IsActive()} interactable={menu.OpenStagesButton.IsInteractable()} hits={string.Join(",",hits.Select(hit=>hit.gameObject.name))}");
            }
            Expect(menu.IsStageSelectionOpen && !menu.StartButton.interactable,"시작하기는 선택 화면으로만 이동");
            Click(menu.StartButton);
            Expect(!GameNavigation.IsTransitioning,"선택 전 시작 차단");
            await ClickMouse(mouse,menu.BackButton);
            Expect(!menu.IsStageSelectionOpen,"선택 화면 뒤로가기");
            await ClickMouse(mouse,menu.OpenStagesButton);
            await ClickMouse(mouse,menu.StageButton);
            Expect(menu.StartButton.interactable && menu.StageNameText.text=="STAGE 1-1","선택 후 시작 활성화");
            await UniTask.Delay(220,ignoreTimeScale:true); CheckLabels(menu);
            if(Application.isBatchMode)
            {
                SynergyUIChecks.Capture(1920,1080,"Stages_1920.png");
                SynergyUIChecks.Capture(1280,720,"Stages_1280.png");
            }
            Click(menu.StartButton);
            Expect(menu.IsLoading && GameNavigation.IsTransitioning,"장면 전환 중 로딩과 입력 잠금");
            Expect(!await GameNavigation.StartStageAsync(settings,first),"빠른 중복 시작 차단");
            var run=await WaitRun();
            CheckNewRun(run);
            Expect(GameNavigation.SelectedStage==first,"선택한 스테이지 유지");
            SettleChallenge(run,true);
            var hud=UnityEngine.Object.FindFirstObjectByType<RunHUD>();
            Expect(run.State.WonRun && hud.ResultPanel.RetryButton.gameObject.activeSelf
                && hud.ResultPanel.TitleButton.gameObject.activeSelf && !hud.ResultPanel.NextRoundButton.gameObject.activeSelf,
                "최종 승리에서 재도전·타이틀 버튼 표시");
            Expect(((RectTransform)hud.ResultPanel.transform).sizeDelta==new Vector2(720f,340f),"HUD 내부 결과 카드 여백 유지");
            await UniTask.Delay(250,ignoreTimeScale:true); CheckLabels(hud.ResultPanel);
            if(Application.isBatchMode) SynergyUIChecks.Capture(1920,1080,"Challenge_Complete.png");
            var previous=run; var oldState=run.State;
            Click(hud.ResultPanel.RetryButton); Click(hud.ResultPanel.RetryButton);
            run=await WaitRun(previous);
            Expect(!ReferenceEquals(run.State,oldState) && previous.ActiveAssetHandleCount==0,"재도전 새 상태와 이전 핸들 해제");
            CheckNewRun(run);
            SettleChallenge(run,false);
            hud=UnityEngine.Object.FindFirstObjectByType<RunHUD>();
            Expect(run.State.IsOver && !run.State.WonRun && run.State.Lives==0
                && hud.ResultPanel.RetryButton.gameObject.activeSelf,"세 번째 패배 종료에도 재도전 제공");
            if(Application.isBatchMode) SynergyUIChecks.Capture(1280,720,"Challenge_Failed.png");
            Click(hud.ResultPanel.TitleButton);
            menu=await WaitTitle();
            Expect(!menu.IsStageSelectionOpen && !GameNavigation.IsTransitioning
                && UnityEngine.Object.FindObjectsByType<RunController>(FindObjectsSortMode.None).Length==0,
                "타이틀 복귀와 도전 오브젝트 정리");
            Click(menu.OpenStagesButton); Click(menu.StageButton); Click(menu.StartButton);
            run=await WaitRun(); CheckNewRun(run);
            // 진행 중 전투 취소와 이중 정리 호출도 같은 정리 작업 공유.
            var owned=run.State.Units[0]; var cell=HexCoord.FromOffset(1,2);
            Expect(run.State.Placement.TryMoveUnit(owned.Placement,UnitLocation.OnBoard(cell)).Success,"취소 검사 부대 배치");
            var unit=UnityEngine.Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).First(value=>value.Team==UnitTeam.Ally);
            unit.View.SnapToLocation(run.Grid,run.Bench);
            Expect(run.StartBattle(),"진행 중 전투 정리 검사 시작");
            var leaving=GameNavigation.ReturnToTitleAsync(settings,run);
            await UniTask.WhenAll(run.ShutdownAsync(),run.ShutdownAsync());
            Expect(await leaving && run.ActiveAssetHandleCount==0,"전투 취소·풀·핸들 정리와 중복 호출 안전성");
            await WaitTitle();
            Expect(GameNavigation.SelectedStage==null && Mathf.Approximately(Time.timeScale,1f),"복귀 시 선택 문맥과 시간 배율 정리");
            Debug.Log("GAME_ENTRY_PLAY_PASS "+checks+" title-stage-start-final-retry-loss-title-cancel-release");
            SessionState.SetBool(Key,false);
        }
        private static void SettleChallenge(RunController run,bool won)
        {
            while(!run.State.IsOver)
            {
                if(run.State.Augments.HasPending)
                    Expect(run.SelectAugment(run.State.Augments.OfferedIds[0]),"도전 중 기존 증강 선택 진행 유지");
                Expect(run.Flow.TryStartCombat(),"라운드 전투 단계 진입"); run.State.CanPrepare=false;
                Expect(run.Flow.TryFinishCombat() && run.State.TrySettle(won),"승패 결과 1회 정산");
                if(!run.State.IsOver) Expect(run.Continue(),"다음 라운드 유지");
            }
        }
        private static void CheckNewRun(RunController run)
        {
            Expect(run.State.Round==1 && run.State.Level==1 && run.State.Gold==4 && run.State.Lives==3 && run.State.Xp==0,
                "초기 라운드·레벨·골드·목숨·경험치");
            Expect(run.State.Units.Count==1 && run.State.Board.DeployedCount==0 && run.State.Augments.SelectedIds.Count==0,
                "초기 기물·배치·증강 상태");
            Expect(run.State.Shop.Count(id=>id!=0)==5 && run.ActiveAssetHandleCount==9+run.Visuals.PrefabLeaseCount,"상점과 데이터·8종 프리팹 로드");
            Expect(UnityEngine.Object.FindObjectsByType<RunHUD>(FindObjectsSortMode.None).Length==1
                && UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"HUD·EventSystem 중복 없음");
        }
        private static async UniTask<TitleMenu> WaitTitle()
        {
            await UniTask.WaitUntil(()=>SceneManager.GetActiveScene().buildIndex==ProjectSetupSettings.Current.Navigation.TitleSceneBuildIndex
                && !GameNavigation.IsTransitioning).Timeout(TimeSpan.FromSeconds(30));
            await UniTask.DelayFrame(2);
            var menu=UnityEngine.Object.FindFirstObjectByType<TitleMenu>(); Expect(menu!=null,"타이틀 프리팹 연결"); return menu;
        }
        private static async UniTask<RunController> WaitRun(RunController previous=null)
        {
            RunController run=null;
            await UniTask.WaitUntil(()=>
            {
                run=UnityEngine.Object.FindFirstObjectByType<RunController>();
                return run!=null && !ReferenceEquals(run,previous) && run.IsReady && !GameNavigation.IsTransitioning;
            }).Timeout(TimeSpan.FromSeconds(30));
            await UniTask.DelayFrame(2); return run;
        }
        private static void CheckLabels(Component view)
        {
            Canvas.ForceUpdateCanvases();
            foreach(var label in view.GetComponentsInChildren<TextMeshProUGUI>())
            {
                label.ForceMeshUpdate();
                Expect(!label.isTextOverflowing,"화면 문구 잘림 없음: "+label.text);
                Expect(label.font.HasCharacters(label.text,out uint[] missing,false,true),"한글 표시 지원");
            }
        }
        private static void Click(Button button)
        {
            ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        }
        private static async UniTask ClickMouse(Mouse mouse,Button button)
        {
            await UniTask.DelayFrame(2); Canvas.ForceUpdateCanvases();
            Vector2 point=RectTransformUtility.WorldToScreenPoint(null,button.transform.position);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=point}); await UniTask.DelayFrame(2);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=point}.WithButton(MouseButton.Left)); await UniTask.DelayFrame(2);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=point}); await UniTask.DelayFrame(2);
        }
    }
}
#endif
