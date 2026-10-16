#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Battle.Units;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using AutoBattler.Presentation.Battle;
using AutoBattler.Presentation.Units;
using AutoBattler.Data.Snapshot;
using System.IO;
using UnityEngine.AddressableAssets;

namespace AutoBattler.EditorTools
{
    [InitializeOnLoad]
    public static class MilestonePlayChecks
    {
        private const string Key="PixelTactics.PlayChecks";
        private static bool running;
        static MilestonePlayChecks() { EditorApplication.update+=CheckStart; }
        [MenuItem("PixelTactics/Checks/Run Play Checks")]
        public static void Start()
        {
            SessionState.SetBool(Key,true);
            SessionState.SetFloat(Key+".started",(float)EditorApplication.timeSinceStartup);
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            EditorSceneManager.OpenScene(ProjectSetupSettings.Current.ScenePath);
            var sceneHud=UnityEngine.Object.FindFirstObjectByType<RunHUD>();
            if(sceneHud==null || PrefabUtility.GetCorrespondingObjectFromSource(sceneHud)==null)
                throw new InvalidOperationException("BattleHUD 프리팹을 씬에 연결한 뒤 검사 필요.");
            EditorApplication.isPlaying=true;
        }
        private static void CheckStart()
        {
            if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || running) return;
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".started",0)>90)
            { Debug.LogError("Play checks: 초기 데이터 로드 시간 초과."); Finish(1); return; }
            var run=UnityEngine.Object.FindFirstObjectByType<RunController>();
            if(run==null || !run.IsReady) return;
            running=true; Run(run).Forget();
        }
        private static void Expect(bool condition,string message) { if(!condition) throw new Exception("Play check: "+message); }
        private static async UniTask Run(RunController run)
        {
            try
            {
                Time.timeScale=10f;
                await SynergyUIChecks.RunPlay(run,UnityEngine.Object.FindFirstObjectByType<RunHUD>());
                await AssassinEntryChecks.RunPlay(run);
                await TraitChecks.RunPlay(run);
                await DamageEventChecks.RunPlay(run);
                await SkillChecks.RunPlay(run);
                await ObjectPoolChecks.RunPlay(run);
                await BehaviourTreeChecks.RunPlay(run);
                await UnitEffectsChecks.RunPlay(run);
                await UnitFacingPlayChecks.Run(run);
                await CheckCharacterPrefabs(run);
                Expect(run.State.Gold==4 && run.State.Level==1 && run.State.Units.Count==1,"초기 도전");
                var huds=UnityEngine.Object.FindObjectsByType<RunHUD>(FindObjectsSortMode.None);
                Expect(huds.Length==1 && huds[0].Controller==run,"씬의 BattleHUD 한 개와 진행 참조 사용");
                var hud=huds[0];
                using var renamedUI=new RenamedUIProbe(hud);
                CheckShopItemView(run,hud);
                await CheckReusableViews(hud);
                var starterView=UnityEngine.Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).First(x=>x.Team==UnitTeam.Ally);
                Expect(starterView.GetComponentsInChildren<UnitHealthBar>(true).Length==1,"기물별 공통 체력바 한 개");
                await UniTask.DelayFrame(1);
                Expect(!starterView.GetComponentInChildren<UnitHealthBar>().GetComponent<Canvas>().enabled,"대기석 체력바 숨김");
                Expect(huds.Length==1,"런타임 HUD 중복 생성 없음");
                Expect(hud.GoldText.text=="Gold 4","초기 골드 표시");
                Expect(!hud.ResultPanel.transform.gameObject.activeSelf,"준비 중 결과 패널 숨김");
                hud.enabled=false; hud.enabled=true;
                Click(hud.RefreshButton);
                Expect(run.State.Gold==2,"재활성화 후 갱신 버튼 비용 한 번 차감");
                Expect(hud.GoldText.text=="Gold 2","버튼 명령 뒤 골드 갱신");
                Expect(!run.StartBattle(),"0명 시작 차단");
                int slot=Enumerable.Range(0,5).First(i=>run.State.Shop[i]!=0);
                var shopItem=hud.ShopItems[slot];
                var definition=run.State.Data.Units[run.State.Shop[slot]];
                Expect(shopItem.SlotIndex==slot && shopItem.DisplayedUnitId==definition.Id,"상점 슬롯과 종류 연결");
                Expect(shopItem.NameText.text==definition.Name,"상점 이름 표시");
                shopItem.gameObject.SetActive(false); shopItem.gameObject.SetActive(true);
                Click(shopItem.BuyButton);
                Expect(run.State.Gold==1 && run.State.Units.Count==2,"상점 버튼 구매");
                Expect(shopItem.IsEmpty && shopItem.EmptyText.transform.gameObject.activeSelf && !shopItem.Content.activeSelf,"구매한 상점 칸 표시 갱신");
                Expect(!shopItem.GetComponent<Button>().interactable,"빈 슬롯 구매 잠금");
                Expect(run.BuyXp() == false,"골드 부족 원상 유지");
                var owned=run.State.Units[0]; HexCoord prepared=HexCoord.FromOffset(1,2);
                var dragged=UnityEngine.Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).First(x=>x.InstanceId==owned.Placement.InstanceId);
                var mouse=Mouse.current ?? InputSystem.AddDevice<Mouse>();
                Vector2 screen=Camera.main.WorldToScreenPoint(dragged.transform.position);
                InputSystem.QueueStateEvent(mouse,new MouseState {position=screen});
                await UniTask.DelayFrame(2);
                InputSystem.QueueStateEvent(mouse,new MouseState {position=screen}.WithButton(MouseButton.Left));
                await UniTask.DelayFrame(2);
                if(run.Drag.SelectedUnit!=dragged.View)
                    Debug.Log($"DRAG_DIAGNOSTIC enabled={run.Drag.enabled} mouse={mouse.position.ReadValue()} pressed={mouse.leftButton.isPressed} layer={dragged.gameObject.layer} screen={screen} size={Screen.width}x{Screen.height} UI={UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()} hit={Physics2D.OverlapPoint(dragged.transform.position,1<<8)}");
                Expect(run.Drag.SelectedUnit==dragged.View,"실제 마우스 기물 선택");
                screen=Camera.main.WorldToScreenPoint(dragged.GridLayout.CellToWorld(prepared));
                InputSystem.QueueStateEvent(mouse,new MouseState {position=screen}.WithButton(MouseButton.Left));
                await UniTask.DelayFrame(2);
                InputSystem.QueueStateEvent(mouse,new MouseState {position=screen});
                await UniTask.DelayFrame(2);
                Expect(owned.Placement.Cell==prepared,"실제 마우스 드래그 배치");
                Expect(run.AllySynergies.Statuses.Count==8
                    && run.AllySynergies.Count(AutoBattler.Data.Traits.TraitCategory.Origin,dragged.Definition.OriginId)==1,
                    "실제 배치 후 시너지 집계 자동 갱신과 대기석 제외");
                Expect(ReferenceEquals(hud.SynergyPanel.DisplayedSnapshot,run.AllySynergies)
                    && hud.SynergyPanel.Rows.Single(row=>row.Category==AutoBattler.Data.Traits.TraitCategory.Origin
                        && row.GroupId==dragged.Definition.OriginId).Count==1,"실제 드래그 배치 후 시너지 UI 자동 갱신");
                await UniTask.DelayFrame(1);
                var healthBar=dragged.GetComponentInChildren<UnitHealthBar>();
                Expect(healthBar.GetComponent<Canvas>().enabled,"전장 배치 후 체력바 표시");
                Expect(healthBar.HealthText.text==$"{dragged.Health.CurrentHp}/{dragged.Health.MaxHp}","실제 유닛 체력 표시");
                Click(hud.StartBattleButton);
                Expect(run.Flow.CurrentPhase==BattlePhase.Combat,"전투 시작 버튼");
                Expect(!hud.RefreshButton.interactable,"전투 중 UI 잠금");
                Expect(hud.GetComponentsInChildren<ShopItem>().All(x=>!x.GetComponent<Button>().interactable),"전투 중 상점 카드 잠금");
                Expect(!run.Buy(slot) && !run.RefreshShop(),"전투 중 구매·갱신 차단");
                await UniTask.WaitUntil(()=>run.Flow.CurrentPhase==BattlePhase.Result).Timeout(TimeSpan.FromSeconds(20));
                Expect(hud.ResultPanel.transform.gameObject.activeSelf,"전투 종료 결과 패널 표시");
                Expect(hud.ResultPanel.ResultText.text==run.State.LastResult,"정산 내용 표시");
                var resultPanel=hud.ResultPanel;
                resultPanel.gameObject.SetActive(false); resultPanel.gameObject.SetActive(true);
                Expect(resultPanel.GetComponent<CanvasGroup>().alpha==1f,"결과 패널 재활성화 시 불투명도 복원");
                await UniTask.Delay(250,ignoreTimeScale:true);
                if(Application.isBatchMode) CapturePreview("BattleHUD_Result.png");
                Expect(owned.Placement.Cell==prepared,"전투 중 준비 좌표 보존");
                int gold=run.State.Gold; Expect(!run.State.TrySettle(true) && run.State.Gold==gold,"정산 중복 방지");
                Click(hud.ResultPanel.NextRoundButton);
                Expect(run.State.Round==2,"다음 라운드 버튼");
                await UniTask.Yield();
                var unit=UnityEngine.Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).First(x=>x.InstanceId==owned.Placement.InstanceId && x.Team==UnitTeam.Ally);
                Expect(unit.Health.CurrentHp==unit.Health.MaxHp && unit.CurrentCell==prepared,"부대 체력·좌표 복원");
                Expect(unit.GetComponentInChildren<UnitHealthBar>().FillImage.transform.localScale.x==1f,"다음 라운드 체력바 복원");
                Expect(run.State.Round==2 && run.State.CanPrepare,"다음 라운드 상점 활성화");
                Expect(!hud.ResultPanel.transform.gameObject.activeSelf,"다음 준비에서 결과 패널 숨김");
                Expect(hud.RoundText.text=="Round 1-2","다음 라운드 표시 갱신");
                Expect(hud.GetComponentsInChildren<ShopItem>().All(x=>x.DisplayedUnitId==run.State.Shop[x.SlotIndex]),"다음 라운드 상점 카드 갱신");
                await UniTask.Delay(250,ignoreTimeScale:true);
                if(Application.isBatchMode) CapturePreview();
                var benchUnit=UnityEngine.Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).First(x=>x.Team==UnitTeam.Ally && x.View.State.Location.Kind==UnitLocationKind.Bench);
                screen=Camera.main.WorldToScreenPoint(benchUnit.transform.position);
                InputSystem.QueueStateEvent(mouse,new MouseState {position=screen});
                await UniTask.DelayFrame(2);
                InputSystem.QueueStateEvent(mouse,new MouseState {position=screen}.WithButton(MouseButton.Left));
                await UniTask.DelayFrame(2);
                InputSystem.QueueStateEvent(mouse,new MouseState {position=screen});
                await UniTask.DelayFrame(2);
                Expect(run.Drag.SelectedUnit==benchUnit.View,"판매할 기물 선택");
                Expect(hud.SelectedUnitText.text==benchUnit.Definition.Name,"선택 기물 이름 표시");
                int beforeSell=run.State.Gold, sellPrice=benchUnit.Definition.Cost, beforeUnits=run.State.Units.Count;
                Click(hud.SellButton);
                Expect(run.State.Gold==beforeSell+sellPrice && run.State.Units.Count==beforeUnits-1,"판매 버튼 정산");
                int beforeXp=run.State.Gold;
                Click(hud.BuyExperienceButton);
                Expect(run.State.Gold==beforeXp-4 && run.State.Level==3,"경험치 버튼 구매와 레벨 상승");
                Expect(hud.LevelText.text=="Level 3","레벨 표시 갱신");
                // 실제 Addressables 동시 요청과 취소 수명 확인.
                using(var service=new AssetService())
                {
                    var a=service.AcquireAsync<TextAsset>(run.Catalog.Snapshot,CancellationToken.None);
                    var b=service.AcquireAsync<TextAsset>(new AssetReferenceT<TextAsset>(run.Catalog.Snapshot.AssetGUID),CancellationToken.None);
                    var leases=await UniTask.WhenAll(a,b);
                    Expect(service.ActiveHandleCount==1 && leases.Item1.Asset==leases.Item2.Asset,"중복 로드 핸들 공유");
                    leases.Item1.Dispose(); Expect(service.ActiveHandleCount==1,"한 사용자 해제 후 유지");
                    leases.Item2.Dispose(); Expect(service.ActiveHandleCount==0,"마지막 사용자 해제");
                    using(var cancel=new CancellationTokenSource())
                    {
                        var catalog=run.Catalog;
                        var pending=service.AcquireAsync<GameObject>(catalog.VisualPrefab(run.State.Data.Units.Values.First().VisualId),cancel.Token);
                        bool wasPending=pending.Status==UniTaskStatus.Pending;
                        cancel.Cancel(); bool canceled=false;
                        try { var lease=await pending; lease.Dispose(); } catch(OperationCanceledException) { canceled=true; }
                        Expect((!wasPending || canceled) && service.ActiveHandleCount==0,"로드 중 취소 핸들 정리");
                    }
                }
                Debug.Log("PLAY_CHECKS_PASS purchase-placement-autobattle-settlement-restore-addressables");
                Debug.Log("REFERENCE_PLAY_PASS renamed-ui-guid-load-handle-sharing-pool");
                Finish(0);
            }
            catch(Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static async UniTask CheckCharacterPrefabs(RunController run)
        {
            var catalog=run.Catalog;
            using(var service=new AssetService())
            {
                var parent=new GameObject("CharacterPrefabProbes");
                parent.transform.position=new Vector3(-1000f,-1000f,0f);
                var pool=new UnitPrefabPool(service,parent.transform);
                try
                {
                    var units=new HashSet<Unit>();
                    foreach(var definition in run.State.Data.Units.Values)
                    {
                        int character=definition.VisualId+1;
                        var address=catalog.VisualPrefab(definition.VisualId);
                        var setupEntry=ProjectSetupSettings.Current.Unit(definition.VisualId);
                        Expect(address.AssetGUID==AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(setupEntry.UnitPrefab)),"외형 ID별 캐릭터 참조 연결");
                        await pool.PreloadAsync(address,CancellationToken.None);
                        var unit=pool.Rent(address);
                        Expect(units.Add(unit),"외형별 별도 프리팹 인스턴스");
                        var animator=unit.GetComponent<Animator>();
                        var source=setupEntry.SourcePrefab;
                        Expect(animator.runtimeAnimatorController.name==source.GetComponent<Animator>().runtimeAnimatorController.name,"제작한 캐릭터 Animator 사용");
                        unit.Configure(definition,1,60000+character,UnitTeam.Enemy,run.State.Data,
                            run.Flow,run.Grid,run.Bench,HexCoord.FromOffset(7,0));
                        unit.gameObject.SetActive(true);
                        await UniTask.DelayFrame(1);
                        var bar=unit.GetComponentInChildren<UnitHealthBar>(true);
                        Expect(unit.Health.MaxHp==definition.Hp && bar.GetComponent<Canvas>().enabled,"종류별 JSON 체력과 체력바 연결");
                        unit.Health.TakeDamage(1);
                        Expect(unit.Health.CurrentHp==definition.Hp-1,"새 캐릭터 피해 처리");
                        pool.Return(unit);
                        var reused=pool.Rent(address);
                        Expect(reused==unit,"종류별 풀 재사용");
                        reused.gameObject.SetActive(true);
                        pool.Return(reused);
                    }
                    Expect(units.Count==8,"유닛 8종 로드");
                }
                finally { await pool.DisposeAsync(); UnityEngine.Object.Destroy(parent); }
                Expect(service.ActiveHandleCount==0,"8종 프리팹 핸들 해제");
            }
            Debug.Log("CHARACTER_PREFABS_PLAY_PASS 8-addressables-configure-health-pool-release");
        }
        private static async UniTask CheckReusableViews(RunHUD hud)
        {
            var resultRoot=UnityEngine.Object.Instantiate(ProjectSetupSettings.Current.ResultPanel.gameObject,hud.transform);
            var result=resultRoot.GetComponent<ResultPanel>();
            try
            {
                int requests=0;
                result.ContinueRequested+=()=>requests++;
                result.Show("Battle result","Victory +3 gold",true,false);
                Expect(resultRoot.activeSelf && result.CanContinue,"결과 표시와 다음 라운드 허용");
                resultRoot.SetActive(false); resultRoot.SetActive(true);
                var button=result.NextRoundButton;
                ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
                Expect(requests==1,"결과 패널 재활성화 후 요청 한 번 전달");
                result.Show("Challenge complete!","Victory",false,false);
                Expect(!result.CanContinue && !button.gameObject.activeSelf && result.EndMessageText.transform.gameObject.activeSelf,"도전 종료 시 다음 라운드 대신 종료 안내");
                ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
                Expect(requests==1,"도전 종료 이후 다음 라운드 요청 차단");
                result.Hide();
                Expect(!resultRoot.activeSelf && !result.GetComponent<CanvasGroup>().blocksRaycasts,"결과 숨김과 입력 차단 해제");
            }
            finally { UnityEngine.Object.DestroyImmediate(resultRoot); }

            var ownerRoot=new GameObject("HealthBarProbe",typeof(UnitHealth),typeof(UnitView),typeof(Unit));
            ownerRoot.transform.position=new Vector3(-1000f,-1000f,0f);
            var owner=ownerRoot.GetComponent<Unit>();
            var board=new BoardState();
            var placement=new PlacementService(board);
            var state=new UnitState(50000);
            Expect(placement.TryRegisterUnit(state,UnitLocation.OnBoard(HexCoord.FromOffset(0,0))),"검사용 기물 전장 등록");
            owner.View.Bind(state,board);
            var barRoot=UnityEngine.Object.Instantiate(ProjectSetupSettings.Current.HealthBar.gameObject,owner.transform,false);
            var bar=barRoot.GetComponent<UnitHealthBar>();
            var canvas=barRoot.GetComponent<Canvas>();
            var fill=bar.FillImage.transform;
            var label=bar.HealthText;
            GameObject otherRoot=null;
            try
            {
                Expect(!canvas.enabled,"초기화 전 체력바 숨김");
                owner.Health.Initialize(100,0);
                Expect(canvas.enabled && label.text=="100/100" && fill.localScale.x==1f,"바인딩 이후 늦은 체력 초기화 반영");
                owner.Health.TakeDamage(25);
                await UniTask.WaitUntil(()=>Mathf.Abs(fill.localScale.x-.75f)<.001f).Timeout(TimeSpan.FromSeconds(3));
                Expect(label.text=="75/100" && Mathf.Abs(fill.localScale.x-.75f)<.001f,"피해 표시와 체력바 애니메이션");
                bar.enabled=false;
                owner.Health.TakeDamage(10);
                bar.enabled=true;
                Expect(label.text=="65/100" && Mathf.Abs(fill.localScale.x-.65f)<.001f,"재활성화 시 현재 체력 동기화");
                owner.Health.TakeDamage(1000);
                Expect(!canvas.enabled,"사망 시 체력바 숨김");
                owner.Health.RestoreFullHealth();
                Expect(canvas.enabled && fill.localScale.x==1f && label.text=="100/100","체력 복원과 다시 표시");
                owner.Health.Initialize(200,0);
                Expect(fill.localScale.x==1f && label.text=="200/200","풀 재사용과 성급 변경의 최대 체력 갱신");
                placement.TryMoveUnit(state,UnitLocation.OnBench(0));
                await UniTask.DelayFrame(2);
                Expect(!canvas.enabled,"대기석 이동 후 체력바 숨김");
                otherRoot=new GameObject("OtherHealthBarProbe",typeof(UnitHealth),typeof(UnitView),typeof(Unit));
                otherRoot.transform.position=new Vector3(-1000f,-1000f,0f);
                var other=otherRoot.GetComponent<Unit>();
                var otherState=new UnitState(50001);
                Expect(placement.TryRegisterUnit(otherState,UnitLocation.OnBoard(HexCoord.FromOffset(0,0))),"두 번째 검사용 기물 등록");
                other.View.Bind(otherState,board);
                other.Health.Initialize(50,0);
                bar.Bind(other);
                owner.Health.TakeDamage(25);
                Expect(label.text=="50/50" && canvas.enabled,"소유자 변경 시 이전 체력 이벤트 연결 해제");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ownerRoot);
                if(otherRoot!=null) UnityEngine.Object.DestroyImmediate(otherRoot);
            }
            Debug.Log("REUSABLE_UI_PLAY_PASS result-health-initialize-damage-death-restore-rebind");
        }
        private static void CheckShopItemView(RunController run,RunHUD hud)
        {
            var prefab=ProjectSetupSettings.Current.ShopItem.gameObject;
            var root=UnityEngine.Object.Instantiate(prefab,hud.transform);
            var item=root.GetComponent<ShopItem>();
            var button=root.GetComponent<Button>();
            var definition=run.State.Data.Units.Values.First();
            var image=item.IconImage;
            var placeholder=item.IconPlaceholderText;
            var catalog=run.Catalog;
            var previousIcons=catalog.icons;
            var texture=new Texture2D(2,2);
            var sprite=Sprite.Create(texture,new Rect(0,0,2,2),new Vector2(.5f,.5f));
            try
            {
                item.BindSlot(4);
                int requests=0,requestedSlot=-1;
                item.PurchaseRequested+=index=>{requests++; requestedSlot=index;};
                item.Show(definition,null,0,definition.Cost,true);
                Expect(button.interactable && image.sprite==null && placeholder.gameObject.activeSelf,"미등록 아이콘 표시와 예약 재고 0 구매 허용");
                item.Show(definition,null,10,definition.Cost-1,true);
                ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
                Expect(!button.interactable && requests==0,"골드 부족 구매 차단");
                item.Show(definition,null,10,definition.Cost,false);
                Expect(!button.interactable,"준비 단계 외 구매 차단");
                catalog.icons=new[]{new AutoBattler.Assets.UnitIconEntry {id=definition.IconId,sprite=sprite}};
                item.Show(definition,run.UnitIcon(definition.IconId),10,definition.Cost,true);
                Expect(image.sprite==sprite && !placeholder.gameObject.activeSelf,"아이콘 ID 연결 표시");
                Expect(run.UnitIcon(-1)==null,"미등록 아이콘 조회");
                item.gameObject.SetActive(false); item.gameObject.SetActive(true);
                ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
                Expect(requests==1 && requestedSlot==4,"재활성화 후 구매 요청 한 번 전달");
                item.Show(null,null,0,0,false);
                Expect(item.IsEmpty && image.sprite==null && !button.interactable,"빈 슬롯 아이콘 초기화");
                Expect(!item.Content.activeSelf && item.EmptyText.transform.gameObject.activeSelf,"빈 슬롯 내용 숨김");
            }
            finally
            {
                catalog.icons=previousIcons;
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.Destroy(sprite);
                UnityEngine.Object.Destroy(texture);
            }
        }
        private static void Click(Button button)
        {
            Expect(button.IsInteractable(),"버튼 조작 가능: "+button.name);
            ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        }
        // UI 이름을 변경해도 참조 기반 조작과 표시 검사가 통과하는지 확인.
        private sealed class RenamedUIProbe : IDisposable
        {
            private readonly Dictionary<Transform,string> names=new Dictionary<Transform,string>();
            public RenamedUIProbe(RunHUD hud)
            {
                int index=0;
                foreach(var child in hud.GetComponentsInChildren<Transform>(true))
                {
                    if(child==hud.transform) continue;
                    names.Add(child,child.name); child.name="ReferenceProbe_"+index++;
                }
            }
            public void Dispose()
            {
                foreach(var pair in names) if(pair.Key!=null) pair.Key.name=pair.Value;
            }
        }
        private static void CapturePreview(string fileName="MilestonePreview.png")
        {
            Camera camera=Camera.main;
            var target=new RenderTexture(1920,1080,24); target.Create();
            var previous=camera.targetTexture; camera.targetTexture=target;
            var overlay=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(x=>x.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            foreach(var canvas in overlay) { canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1f; }
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
            var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            var active=RenderTexture.active; RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply();
            Directory.CreateDirectory("Diagnostics");
            File.WriteAllBytes("Diagnostics/"+fileName,texture.EncodeToPNG());
            RenderTexture.active=active; camera.targetTexture=previous;
            foreach(var canvas in overlay) { canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null; }
            UnityEngine.Object.Destroy(texture); target.Release(); UnityEngine.Object.Destroy(target);
        }
        private static void Finish(int code)
        {
            Time.timeScale=1f; SessionState.SetBool(Key,false); EditorApplication.isPlaying=false;
            if(Application.isBatchMode) EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
            running=false;
        }
    }
}
#endif
