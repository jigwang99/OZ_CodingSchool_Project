#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Traits;
using AutoBattler.Data.Snapshot;
using AutoBattler.Data.Traits;
using AutoBattler.Presentation.Battle;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace AutoBattler.EditorTools
{
    public static class SynergyUIChecks
    {
        private static int checks;
        private static void Expect(bool value,string message)
        { if(!value) throw new Exception("Synergy UI check: "+message); checks++; }
        [MenuItem("PixelTactics/Checks/Run Synergy UI Checks")]
        public static void BuildAndPlay()
        {
            SynergyUISetup.Connect(); var setup=ProjectSetupSettings.Current;
            string before=File.ReadAllText(setup.HUDPath),panelBefore=File.ReadAllText(setup.SynergyPanelPath);
            SynergyUISetup.Connect();
            Expect(before==File.ReadAllText(setup.HUDPath) && panelBefore==File.ReadAllText(setup.SynergyPanelPath),"재연결 시 프리팹 중복·변경 없음");
            var panel=setup.BattleHUD.SynergyPanel;
            Expect(panel!=null && panel.Rows.Count==8 && panel.GetComponentsInChildren<Canvas>(true).Length==0,"공유 HUD Canvas와 8개 행 직렬화 연결");
            Expect(panel.Rows.Select(row=>(row.Category,row.GroupId)).Distinct().Count()==8,"계열·직업 그룹 중복 없음");
            Expect(panel.GetComponentsInChildren<Graphic>(true).All(graphic=>!graphic.raycastTarget),"패널이 기물 드래그 입력을 가리지 않음");
            AssetDatabase.SaveAssets(); Debug.Log("SYNERGY_UI_PREFAB_PASS");
            TraitChecks.BuildAndPlay();
        }
        public static async UniTask RunPlay(RunController run,RunHUD hud)
        {
            var panel=hud.SynergyPanel; var previousCulture=CultureInfo.CurrentCulture;
            try
            {
                checks=0;
                Expect(panel!=null && panel.IsDataAvailable && ReferenceEquals(panel.DisplayedSnapshot,run.AllySynergies),"진행 이벤트에 연결된 실제 패널 표시");
                Expect(panel.Rows.All(row=>row.Count==0 && !row.IsActive && !row.gameObject.activeSelf),"대기석만 있는 시작 상태 0종 숨김");
                Expect(panel.EmptyText.gameObject.activeSelf && ((RectTransform)panel.transform).rect.height==208f,"빈 배치 안내와 패널 축소");
                var human=panel.Rows.Single(row=>row.Category==TraitCategory.Origin && row.GroupId==1);
                var mage=panel.Rows.Single(row=>row.Category==TraitCategory.Class && row.GroupId==2);
                var first=TraitCounter.Count(new[]{run.State.Data.Units[1001],run.State.Data.Units[1001]},run.State.Data.Traits);
                panel.Show(first,true,BattlePhase.Preparation);
                Expect(human.gameObject.activeSelf && human.CountText.text=="1/2" && !human.IsActive && panel.Rows.Count(row=>row.gameObject.activeSelf)==2,"같은 종류 중복은 UI에서도 1종, 해당 그룹만 표시");
                Expect(human.CountText.color.r==human.CountText.color.g && human.CountText.color.g==human.CountText.color.b,"1/2는 중립 회색 표시");
                var combined=TraitCounter.Count(new[]{run.State.Data.Units[1001],run.State.Data.Units[1007],run.State.Data.Units[1003]},run.State.Data.Traits);
                panel.Show(combined,true,BattlePhase.Preparation);
                Expect(human.IsActive && mage.IsActive && human.CountText.text=="2/2 ACTIVE" && panel.Rows.Count(row=>row.IsActive)==2,"계열·직업 동시 활성과 수치·문구 표시");
                Expect(human.EffectText.text.Contains("15%") && human.EffectText.text.Contains("5s") && mage.EffectText.text.Contains("20%"),"데이터 수치로 효과 설명 생성");
                Expect(panel.Rows.Count(row=>row.gameObject.activeSelf)==4 && !panel.EmptyText.gameObject.activeSelf,"0종은 숨기고 보유 중인 네 그룹만 표시");
                var elf=panel.Rows.Single(row=>row.Category==TraitCategory.Origin && row.GroupId==2);
                Expect(Mathf.Approximately(((RectTransform)elf.transform).anchoredPosition.y,((RectTransform)human.transform).anchoredPosition.y-70f)
                    && ((RectTransform)mage.transform).anchoredPosition.y>-506f,"숨긴 행의 빈자리 제거와 그룹 간격 조정");
                await UniTask.Delay(250,ignoreTimeScale:true);
                Expect(human.CountText.color.g>human.CountText.color.r && human.Background.color.g>human.Background.color.r,"활성 시 녹색 표시");
                if(Application.isBatchMode) Capture(1920,1080,"SynergyUI_Partial_1920.png");
                var all=TraitCounter.Count(run.State.Data.Units.Values,run.State.Data.Traits);
                CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("fr-FR");
                panel.Show(all,true,BattlePhase.Combat);
                Expect(panel.Rows.All(row=>row.IsActive) && panel.StatusText.text=="Locked for this battle","전투 중 스냅샷과 잠금 안내");
                Expect(panel.Rows.Single(row=>row.Category==TraitCategory.Class && row.GroupId==4).EffectText.text.Contains("+0.3"),"소수점 설명은 로케일과 무관");
                await UniTask.Delay(250,ignoreTimeScale:true);
                var activeColor=human.Background.color;
                panel.Show(first,true,BattlePhase.Preparation);
                await UniTask.Delay(250,ignoreTimeScale:true);
                Expect(!human.IsActive && human.Background.color!=activeColor,"활성 해제 시 표시·DOTween 색상 갱신");
                Expect(human.Background.color.r==human.Background.color.g && human.Background.color.g==human.Background.color.b
                    && !mage.gameObject.activeSelf && mage.Count==0 && !mage.IsActive,"활성 해제는 회색, 0종으로 돌아간 행은 숨김과 상태 초기화");
                var none=TraitCounter.Count(Array.Empty<UnitDefinition>(),run.State.Data.Traits);
                panel.Show(none,true,BattlePhase.Preparation);
                Expect(panel.Rows.All(row=>!row.gameObject.activeSelf && row.Count==0 && !row.IsActive)
                    && panel.EmptyText.gameObject.activeSelf && panel.IsDataAvailable,"모든 기물을 제거하면 안내만 표시하고 데이터는 유지");
                if(Application.isBatchMode) Capture(1280,720,"SynergyUI_Empty_1280.png");
                panel.Show(all,true,BattlePhase.Combat);
                panel.gameObject.SetActive(false); panel.gameObject.SetActive(true); panel.Show(all,true,BattlePhase.Combat);
                Expect(human.IsActive && human.Background.color==activeColor,"재활성화 후 현재 상태 즉시 복원");
                panel.Show(TraitSnapshot.Empty,true,BattlePhase.Preparation);
                Expect(!panel.IsDataAvailable && panel.EmptyText.gameObject.activeSelf && panel.EmptyText.text=="Synergy data is unavailable.","이전 스키마 데이터 없음 표시");
                panel.Show(TraitSnapshot.Empty,false,BattlePhase.Preparation);
                Expect(panel.EmptyText.text=="Loading synergies...","로드 전 표시");

                var nextStage=new TraitDefinition(new TraitRow {traitId=4901,name="Human III",category=TraitCategory.Origin,groupId=1,requiredCount=3,effect=TraitEffect.ShieldAtStart,value=.3f,duration=6});
                var additional=new UnitDefinition(new UnitRow {unitId=1999,originId=1,classId=1});
                var highest=TraitCounter.Count(new[]{run.State.Data.Units[1001],run.State.Data.Units[1007],additional},run.State.Data.Traits.Concat(new[]{nextStage}));
                panel.Show(highest,true,BattlePhase.Result);
                Expect(human.DefinitionId==4901 && human.CountText.text=="3/3 ACTIVE" && human.EffectText.text.Contains("30%")
                    && panel.StatusText.text=="Last battle's synergies","최고 단계와 결과 스냅샷 표시");
                panel.Show(all,true,BattlePhase.Combat);
                await UniTask.Delay(250,ignoreTimeScale:true);
                Canvas.ForceUpdateCanvases();
                foreach(var label in panel.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    label.ForceMeshUpdate();
                    Expect(!label.isTextOverflowing,"라벨 잘림 없음: "+label.text);
                }
                if(Application.isBatchMode)
                {
                    Capture(1920,1080,"SynergyUI_1920.png");
                    Capture(1280,720,"SynergyUI_1280.png");
                }
                Debug.Log("SYNERGY_UI_PLAY_PASS "+checks+" count-active-effects-stage-phase-empty-reenable-layout");
            }
            finally { CultureInfo.CurrentCulture=previousCulture; panel.Show(run.AllySynergies,run.IsReady,run.Flow.CurrentPhase); }
        }
        internal static void Capture(int width,int height,string name)
        {
            var camera=Camera.main; var target=new RenderTexture(width,height,24); target.Create();
            var previous=camera.targetTexture; camera.targetTexture=target;
            var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(canvas=>canvas.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            foreach(var canvas in canvases) { canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1f; }
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
            var pixels=new Texture2D(width,height,TextureFormat.RGB24,false); var active=RenderTexture.active; RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
            Directory.CreateDirectory("Diagnostics"); File.WriteAllBytes("Diagnostics/"+name,pixels.EncodeToPNG());
            RenderTexture.active=active; camera.targetTexture=previous;
            foreach(var canvas in canvases) { canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null; }
            UnityEngine.Object.Destroy(pixels); target.Release(); UnityEngine.Object.Destroy(target);
        }
    }
}
#endif
