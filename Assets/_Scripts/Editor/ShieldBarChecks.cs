#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Units;
using AutoBattler.Presentation.Battle;
using AutoBattler.Presentation.Units;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    public static class ShieldBarChecks
    {
        private static int checks;
        private static void Expect(bool value,string message)
        { if(!value) throw new Exception("Shield bar check: "+message); checks++; }
        [MenuItem("PixelTactics/Checks/Run Shield HP Bar Checks")]
        public static void BuildAndPlay()
        {
            ShieldBarSetup.Connect();
            var setup=ProjectSetupSettings.Current;
            var paths=new[]{setup.HealthBarPath,setup.UnitInfoPath,setup.TitleMenuPath};
            var before=paths.Select(File.ReadAllText).ToArray(); ShieldBarSetup.Connect();
            Expect(paths.Select(File.ReadAllText).SequenceEqual(before),"재연결 시 중복 없음");
            Expect(setup.TitleMenu.GetComponentsInChildren<TextMeshProUGUI>(true).All(label=>!label.text.Contains("나만의 부대") && !label.text.Contains("첫 번째 도전")),"타이틀·선택 카드 문구 삭제");
            GameEntryChecks.BuildAndPlay();
        }
        public static async UniTask RunPlay(RunController run,RunHUD hud)
        {
            checks=0; hud.enabled=false;
            using var assets=new AssetService(); var root=new GameObject("ShieldBarProbes");
            var flowRoot=new GameObject("ShieldBarFlow",typeof(BattleFlowController)); var flow=flowRoot.GetComponent<BattleFlowController>();
            var pool=new UnitPrefabPool(assets,root.transform);
            var address=run.Catalog.VisualPrefab(run.State.Data.Units[1001].VisualId);
            await pool.PreloadAsync(address,CancellationToken.None);
            var unit=pool.Rent(address); var other=pool.Rent(address);
            void Configure(Unit value,int id,UnitTeam team)
            {
                value.Configure(run.State.Data.Units[1001],1,id,team,run.State.Data,flow,run.Grid,run.Bench,HexCoord.FromOffset(1,3));
                var board=new BoardState(); var placement=new PlacementService(board); var state=new UnitState(id);
                placement.TryRegisterUnit(state,UnitLocation.OnBoard(HexCoord.FromOffset(1,3)));
                value.View.Bind(state,board); value.transform.position=run.Grid.CellToWorld(HexCoord.FromOffset(1,3));
                value.Health.Initialize(100,0); value.gameObject.SetActive(true);
            }
            var source=new ModifierSource(ModifierSourceKind.Skill,99801);
            var secondSource=new ModifierSource(ModifierSourceKind.Skill,99802);
            var shield=new EffectDefinition(99801,EffectKind.Shield,50f,300f);
            var smallShield=new EffectDefinition(99802,EffectKind.Shield,20f,300f);
            try
            {
                Configure(unit,980001,UnitTeam.Ally); Configure(other,980002,UnitTeam.Enemy);
                other.transform.position=new Vector3(-1000f,-1000f,0f);
                var bar=unit.GetComponentInChildren<UnitHealthBar>(true); var info=hud.UnitInfo;
                info.Show(unit,run.State.Data); CheckRatios(bar.Segments,1f,0f);
                Expect(!bar.Segments.ShieldImage.enabled && bar.HealthText.text=="100/100","보호막 없는 기존 표시");
                unit.Effects.Apply(shield,source); await Settle();
                CheckRatios(bar.Segments,2f/3f,1f/3f); CheckRatios(info.HealthSegments,2f/3f,1f/3f);
                Expect(bar.HealthText.text.Contains("+50") && info.HealthText.text.Contains("+50"),"보호막 수치 추가 표시");
                var color=bar.Segments.ShieldImage.color;
                Expect(color.r==color.g && color.g==color.b && color.r>.3f && !bar.Segments.ShieldImage.raycastTarget,"회색 표시와 입력 통과");
                if(Application.isBatchMode) SynergyUIChecks.Capture(1920,1080,"Shield_FullHP_1920.png");
                int healthEvents=0; void Changed(int hp,int maximum)=>healthEvents++;
                unit.Health.HealthChanged+=Changed;
                unit.Health.TakeDamage(20,1f,1f); await Settle();
                Expect(healthEvents==0 && unit.Effects.Shield==30 && unit.Health.CurrentHp==100,"체력 변화 없는 보호막 피해");
                CheckRatios(bar.Segments,100f/130f,30f/130f); CheckRatios(info.HealthSegments,100f/130f,30f/130f);
                unit.Health.TakeDamage(50,1f,1f); await Settle();
                Expect(unit.Health.CurrentHp==80 && unit.Effects.Shield==0,"보호막 초과 피해만 HP 차감");
                CheckRatios(bar.Segments,.8f,0f); CheckRatios(info.HealthSegments,.8f,0f);
                unit.Health.HealthChanged-=Changed;
                unit.Effects.Apply(smallShield,source); await Settle();
                CheckRatios(bar.Segments,.8f,.2f); CheckRatios(info.HealthSegments,.8f,.2f);
                if(Application.isBatchMode) SynergyUIChecks.Capture(1280,720,"Shield_PartialHP_1280.png");
                unit.Health.Heal(20); await Settle();
                CheckRatios(bar.Segments,100f/120f,20f/120f); CheckRatios(info.HealthSegments,100f/120f,20f/120f);
                unit.Effects.Apply(shield,secondSource); await Settle();
                Expect(unit.Effects.Shield==70,"여러 보호막 합산"); CheckRatios(bar.Segments,100f/170f,70f/170f);
                unit.Effects.RemoveSource(source); await Settle(); CheckRatios(bar.Segments,2f/3f,1f/3f);
                unit.Effects.Clear(); await Settle(); CheckRatios(bar.Segments,1f,0f);
                var temporary=new EffectDefinition(99803,EffectKind.Shield,25f,.3f);
                unit.Effects.Apply(temporary,source); Expect(flow.TryStartCombat(),"만료 검사 전투 단계");
                await UniTask.Delay(500,ignoreTimeScale:true);
                Expect(unit.Effects.Shield==0,"보호막 지속 시간 만료");
                CheckRatios(bar.Segments,1f,0f); CheckRatios(info.HealthSegments,1f,0f);
                flow.TryFinishCombat(); flow.TryBeginPreparation();
                bar.Bind(other); info.Show(other,run.State.Data);
                unit.Effects.Apply(shield,source); await Settle();
                CheckRatios(bar.Segments,1f,0f); CheckRatios(info.HealthSegments,1f,0f);
                other.Effects.Apply(smallShield,secondSource); await Settle();
                CheckRatios(bar.Segments,100f/120f,20f/120f); CheckRatios(info.HealthSegments,100f/120f,20f/120f);
                Expect(bar.FillImage.color.r>bar.FillImage.color.g,"적 체력은 붉은색 유지");
                unit.Effects.Clear(); bar.Bind(unit); info.Show(unit,run.State.Data);
                unit.Effects.Apply(shield,source); pool.Return(unit); unit=pool.Rent(address); Configure(unit,980003,UnitTeam.Ally);
                info.Show(unit,run.State.Data); await Settle();
                bar=unit.GetComponentInChildren<UnitHealthBar>(true);
                CheckRatios(bar.Segments,1f,0f); CheckRatios(info.HealthSegments,1f,0f);
                Expect(bar.HealthText.text=="100/100" && !bar.Segments.ShieldImage.enabled,"풀 반환·재사용 후 이전 보호막 제거");
                bar.Segments.SetValues(int.MaxValue,int.MaxValue,int.MaxValue,false); CheckRatios(bar.Segments,.5f,.5f);
                bar.Bind(unit); unit.Effects.Apply(shield,source); unit.Health.TakeDamage(int.MaxValue,1f,1f); await Settle();
                Expect(!bar.HealthCanvas.enabled && !bar.Segments.ShieldImage.enabled,"사망 시 체력바·보호막 숨김");
                unit.Health.RestoreFullHealth(); await Settle(); CheckRatios(bar.Segments,1f,0f);
                Expect(bar.HealthCanvas.enabled,"체력 복원 후 일반 체력바 복원");
                Debug.Log("SHIELD_BAR_PLAY_PASS "+checks+" full-partial-absorb-overflow-heal-stack-expire-rebind-pool-death");
            }
            finally
            {
                hud.UnitInfo.Hide(); pool.Return(unit); pool.Return(other); await pool.DisposeAsync();
                UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(flowRoot); hud.enabled=true;
            }
        }
        private static async UniTask Settle()
        { await UniTask.Delay(250,ignoreTimeScale:true); await UniTask.DelayFrame(2); }
        private static void CheckRatios(HealthBarFill fill,float health,float shield)
        {
            Expect(Mathf.Abs(fill.HealthRatio-health)<.001f && Mathf.Abs(fill.ShieldRatio-shield)<.001f,
                $"체력·보호막 비율 {fill.name}: actual {fill.HealthRatio}/{fill.ShieldRatio}, expected {health}/{shield}");
            var hp=fill.HealthImage.rectTransform; var armor=fill.ShieldImage.rectTransform;
            Expect(Mathf.Abs(armor.anchoredPosition.x-hp.anchoredPosition.x-hp.rect.width*health)<.01f
                && fill.HealthRatio+fill.ShieldRatio<=1.0001f,"체력 오른쪽 연결과 막대 범위 유지");
        }
    }
}
#endif
