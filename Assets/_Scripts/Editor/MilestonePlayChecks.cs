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
using System.IO;

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
            EditorSceneManager.OpenScene("Assets/_Scenes/BattleScene.unity");
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
                Expect(run.State.Gold==4 && run.State.Level==1 && run.State.Units.Count==1,"초기 도전");
                Expect(!run.StartBattle(),"0명 시작 차단");
                int slot=Enumerable.Range(0,5).First(i=>run.State.Shop[i]!=0);
                Expect(run.Buy(slot) && run.State.Gold==3 && run.State.Units.Count==2,"UI 명령 구매");
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
                Expect(run.StartBattle(),"전투 시작");
                Expect(!run.Buy(slot) && !run.RefreshShop(),"전투 중 구매·갱신 차단");
                await UniTask.WaitUntil(()=>run.Flow.CurrentPhase==BattlePhase.Result).Timeout(TimeSpan.FromSeconds(20));
                Expect(owned.Placement.Cell==prepared,"전투 중 준비 좌표 보존");
                int gold=run.State.Gold; Expect(!run.State.TrySettle(true) && run.State.Gold==gold,"정산 중복 방지");
                Expect(run.Continue(),"다음 준비");
                await UniTask.Yield();
                var unit=UnityEngine.Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).First(x=>x.InstanceId==owned.Placement.InstanceId && x.Team==UnitTeam.Ally);
                Expect(unit.Health.CurrentHp==unit.Health.MaxHp && unit.CurrentCell==prepared,"부대 체력·좌표 복원");
                Expect(run.State.Round==2 && run.State.CanPrepare,"다음 라운드 상점 활성화");
                if(Application.isBatchMode) CapturePreview();
                // 실제 Addressables 동시 요청과 취소 수명 확인.
                using(var service=new AssetService())
                {
                    var a=service.AcquireAsync<TextAsset>("Data/GameSnapshot",CancellationToken.None);
                    var b=service.AcquireAsync<TextAsset>("Data/GameSnapshot",CancellationToken.None);
                    var leases=await UniTask.WhenAll(a,b);
                    Expect(service.ActiveHandleCount==1 && leases.Item1.Asset==leases.Item2.Asset,"중복 로드 핸들 공유");
                    leases.Item1.Dispose(); Expect(service.ActiveHandleCount==1,"한 사용자 해제 후 유지");
                    leases.Item2.Dispose(); Expect(service.ActiveHandleCount==0,"마지막 사용자 해제");
                    using(var cancel=new CancellationTokenSource())
                    {
                        var pending=service.AcquireAsync<GameObject>("Units/PIXEM_Sample",cancel.Token);
                        bool wasPending=pending.Status==UniTaskStatus.Pending;
                        cancel.Cancel(); bool canceled=false;
                        try { var lease=await pending; lease.Dispose(); } catch(OperationCanceledException) { canceled=true; }
                        Expect((!wasPending || canceled) && service.ActiveHandleCount==0,"로드 중 취소 핸들 정리");
                    }
                }
                Debug.Log("PLAY_CHECKS_PASS purchase-placement-autobattle-settlement-restore-addressables");
                Finish(0);
            }
            catch(Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void CapturePreview()
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
            File.WriteAllBytes("Diagnostics/MilestonePreview.png",texture.EncodeToPNG());
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
