#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Units;
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
    public static class UnitInfoChecks
    {
        private static int checks;
        private static void Expect(bool value,string message)
        { if(!value) throw new Exception("Unit info check: "+message); checks++; }
        [MenuItem("PixelTactics/Checks/Run Unit Info UI Checks")]
        public static void BuildAndPlay()
        {
            UnitInfoSetup.Connect();
            var setup=ProjectSetupSettings.Current;
            string before=File.ReadAllText(setup.HUDPath); UnitInfoSetup.Connect();
            Expect(before==File.ReadAllText(setup.HUDPath),"재연결 시 중복 생성 없음");
            Expect(setup.BattleHUD.UnitInfo!=null && !setup.UnitInfo.gameObject.activeSelf,"프리팹 참조와 기본 숨김 직렬화");
            SelectedAugmentsChecks.BuildAndPlay();
        }
        public static async UniTask RunPlay(RunController run,RunHUD hud,Mouse mouse)
        {
            checks=0;
            var panel=hud.UnitInfo;
            var ally=UnityEngine.Object.FindObjectsByType<Unit>(FindObjectsSortMode.None)
                .First(unit=>unit.Team==UnitTeam.Ally && unit.IsOnBoard);
            var enemy=UnityEngine.Object.FindObjectsByType<Unit>(FindObjectsSortMode.None)
                .First(unit=>unit.Team==UnitTeam.Enemy);
            run.Drag.ClearSelection(); await UniTask.DelayFrame(2);
            Expect(panel!=null && !panel.gameObject.activeSelf && panel.BoundUnit==null,"평소 빈 오른쪽 영역");
            Expect(panel.GetComponentsInChildren<Canvas>(true).Length==0,"추가 Canvas 없이 HUD 공유");
            await ClickWorld(mouse,ally.transform.position);
            Expect(panel.gameObject.activeSelf && panel.BoundUnit==ally && panel.NameText.text==ally.Definition.Name,
                "실제 아군 클릭으로 정보 패널 활성화");
            Expect(hud.SellButton.interactable && run.Drag.SelectedUnit==ally.View,"아군 선택과 기존 판매 유지");
            ally.Health.TakeDamage(1); ally.Mana.Add(17);
            await UniTask.Delay(160,ignoreTimeScale:true);
            Expect(panel.HealthText.text==$"HP  {ally.Health.CurrentHp}/{ally.Health.MaxHp}"
                && Mathf.Abs(panel.HealthFill.transform.localScale.x-(float)ally.Health.CurrentHp/ally.Health.MaxHp)<.001f,
                "피해 이벤트와 체력바 갱신");
            Expect(panel.ManaText.text==$"MP  {ally.Mana.Current}/{ally.Mana.Maximum}","마나 이벤트 갱신");
            var source=new ModifierSource(ModifierSourceKind.Skill,99999);
            ally.StatModifiers.AddModifier(source,StatType.AttackDamage,ModifierOperation.Flat,37f);
            Expect(panel.StatsText.text.Contains(ally.Stats.AttackDamage.ToString()),"변경된 최종 능력치 반영");
            ally.StatModifiers.RemoveSource(source); ally.Health.RestoreFullHealth(); ally.Mana.ResetForRound();
            await UniTask.Delay(160,ignoreTimeScale:true);
            var panelPoint=RectTransformUtility.WorldToScreenPoint(null,panel.transform.TransformPoint(new Vector3(-210f,-350f,0f)));
            await ClickScreen(mouse,panelPoint);
            Expect(panel.BoundUnit==ally && run.Drag.SelectedUnit==ally.View,"정보 패널 클릭 시 선택 유지");
            await ClickWorld(mouse,enemy.transform.position);
            Expect(panel.BoundUnit==enemy && panel.IdentityText.text.Contains("Enemy") && run.Drag.SelectedUnit==null
                && !hud.SellButton.interactable,"적 정보 확인과 판매·배치 등록 차단");
            string previousHealth=panel.HealthText.text;
            ally.Health.TakeDamage(1);
            Expect(panel.HealthText.text==previousHealth,"선택 전환 뒤 이전 체력 이벤트 연결 해제");
            ally.Health.RestoreFullHealth();
            Expect(run.Flow.TryStartCombat() && run.Flow.CanInspectUnits && !run.Flow.CanPlaceUnits,"전투 중 조회만 허용");
            var position=ally.transform.position; var cell=ally.View.State.Cell;
            await ClickWorld(mouse,ally.transform.position);
            Expect(panel.BoundUnit==ally,"전투 중 아군 선택");
            await Drag(mouse,position,position+new Vector3(2f,1f,0f));
            Expect(ally.transform.position==position && ally.View.State.Cell==cell,"전투 중 마우스 드래그로 이동 불가");
            await ClickWorld(mouse,enemy.transform.position);
            Expect(panel.BoundUnit==enemy && !hud.SellButton.interactable,"전투 중 적군 조회");
            Expect(run.Flow.TryFinishCombat() && run.Flow.TryBeginPreparation(),"검사 뒤 준비 단계 복원");
            await ClickWorld(mouse,run.Grid.CellToWorld(HexCoord.FromOffset(0,6)));
            Expect(!panel.gameObject.activeSelf && run.Drag.InspectedUnit==null,"빈 보드 클릭 시 숨김");
            // 실제 풀 반환과 재대여로 선택 수명 구분 확인.
            using var assets=new AssetService(); var root=new GameObject("UnitInfoProbes");
            var pool=new UnitPrefabPool(assets,root.transform);
            var address=run.Catalog.VisualPrefab(ally.Definition.VisualId);
            await pool.PreloadAsync(address,CancellationToken.None);
            var probe=pool.Rent(address);
            try
            {
                var probeCell=HexCoord.FromOffset(2,6);
                void Configure(int id)
                {
                    probe.Configure(ally.Definition,1,id,UnitTeam.Enemy,run.State.Data,run.Flow,run.Grid,run.Bench,probeCell);
                    probe.transform.position=run.Grid.CellToWorld(probeCell); probe.gameObject.SetActive(true);
                    Physics2D.SyncTransforms();
                }
                Configure(990001); await ClickWorld(mouse,probe.transform.position);
                Expect(panel.BoundUnit==probe,"풀 기물 선택");
                pool.Return(probe); probe=pool.Rent(address); Configure(990002);
                await UniTask.DelayFrame(2);
                Expect(run.Drag.InspectedUnit==null && !panel.gameObject.activeSelf,"같은 풀 오브젝트 재사용 시 선택 자동 승계 방지");
                foreach(var definition in run.State.Data.Units.Values)
                {
                    probe.Configure(definition,2,990003,UnitTeam.Enemy,run.State.Data,run.Flow,run.Grid,run.Bench,probeCell);
                    panel.Show(probe,run.State.Data); Canvas.ForceUpdateCanvases();
                    Expect(ProjectSetupSettings.Current.UnitInfoFont.HasCharacters(probe.Skill.Definition.Name,out uint[] missing,false,true),
                        "한글 스킬명의 실제 글리프 지원");
                    foreach(var label in panel.GetComponentsInChildren<TextMeshProUGUI>())
                    { label.ForceMeshUpdate(); Expect(!label.isTextOverflowing,"8종 정보 라벨 잘림 없음: "+label.text); }
                }
            }
            finally { pool.Return(probe); await pool.DisposeAsync(); UnityEngine.Object.Destroy(root); }
            await ClickWorld(mouse,ally.transform.position);
            await UniTask.Delay(160,ignoreTimeScale:true);
            if(Application.isBatchMode)
            {
                SynergyUIChecks.Capture(1920,1080,"UnitInfo_1920.png");
                SynergyUIChecks.Capture(1280,720,"UnitInfo_1280.png");
            }
            hud.enabled=false; hud.enabled=true; await UniTask.DelayFrame(2);
            Expect(panel.BoundUnit==ally && panel.gameObject.activeSelf,"HUD 재활성화 시 현재 선택 복원");
            run.Drag.ClearSelection(); await UniTask.DelayFrame(2);
            Debug.Log("UNIT_INFO_PLAY_PASS "+checks+" selection-empty-enemy-live-stats-combat-ui-pool-layout");
        }
        private static UniTask ClickWorld(Mouse mouse,Vector3 position)
            =>ClickScreen(mouse,Camera.main.WorldToScreenPoint(position));
        private static async UniTask ClickScreen(Mouse mouse,Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState {position=point}); await UniTask.DelayFrame(2);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=point}.WithButton(MouseButton.Left)); await UniTask.DelayFrame(2);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=point}); await UniTask.DelayFrame(2);
        }
        private static async UniTask Drag(Mouse mouse,Vector3 from,Vector3 to)
        {
            Vector2 start=Camera.main.WorldToScreenPoint(from), end=Camera.main.WorldToScreenPoint(to);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=start}); await UniTask.DelayFrame(2);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=start}.WithButton(MouseButton.Left)); await UniTask.DelayFrame(2);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=end}.WithButton(MouseButton.Left)); await UniTask.DelayFrame(2);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=end}); await UniTask.DelayFrame(2);
        }
    }
}
#endif
