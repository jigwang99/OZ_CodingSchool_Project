#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoBattler.Data.Augments;
using AutoBattler.Presentation.Battle;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace AutoBattler.EditorTools
{
    public static class SelectedAugmentsChecks
    {
        private static int checks;
        private static void Expect(bool value,string message)
        { if(!value) throw new Exception("Selected augments check: "+message); checks++; }
        [MenuItem("PixelTactics/Checks/Run Selected Augments UI Checks")]
        public static void BuildAndPlay()
        {
            SelectedAugmentsSetup.Connect();
            var setup=ProjectSetupSettings.Current;
            string before=File.ReadAllText(setup.HUDPath); SelectedAugmentsSetup.Connect();
            Expect(before==File.ReadAllText(setup.HUDPath),"연결 재실행 시 중복·레이아웃 변경 없음");
            Expect(setup.BattleHUD.SelectedAugments!=null && ((RectTransform)setup.BattleHUD.SynergyPanel.transform).anchoredPosition.y==-200f,"바와 시너지 위치 직렬화");
            AugmentChecks.BuildAndPlay();
        }
        public static async UniTask RunPlay(RunController run,RunHUD hud,Mouse mouse)
        {
            checks=0; var view=hud.SelectedAugments;
            try
            {
                Expect(view!=null && view.SelectedCount==run.State.Augments.SelectedIds.Count && view.SelectedCount==1,"선택 확정 이벤트로 보유 증강 표시");
                Expect(((RectTransform)hud.SynergyPanel.transform).anchoredPosition.y==-200f
                    && ((RectTransform)view.transform).anchoredPosition.y==-136f,"시너지 위 64px 공간 사용");
                Vector2 point=RectTransformUtility.WorldToScreenPoint(null,view.transform.TransformPoint(new Vector3(176f,-28f,0f)));
                InputSystem.QueueStateEvent(mouse,new MouseState {position=point}); await UniTask.DelayFrame(3);
                await UniTask.Delay(180,ignoreTimeScale:true);
                var selected=run.State.Augments.Selected.First();
                Expect(view.IsTooltipVisible && view.TooltipText.text.Contains(selected.Name)
                    && view.TooltipText.text.Contains(selected.Description),"실제 마우스 호버로 이름·효과 표시");
                Expect(view.GetComponentsInChildren<Canvas>(true).Length==0 && !view.Tooltip.blocksRaycasts
                    && view.Tooltip.GetComponentsInChildren<Graphic>(true).All(graphic=>!graphic.raycastTarget),"공유 Canvas와 설명의 입력 통과");
                var pointer=new PointerEventData(EventSystem.current) {position=RectTransformUtility.WorldToScreenPoint(null,view.Tooltip.transform.position)+new Vector2(60f,-30f)};
                var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
                Expect(hits.All(hit=>!hit.gameObject.transform.IsChildOf(view.Tooltip.transform)),"설명 영역에서 보드 입력 가로막지 않음");
                if(Application.isBatchMode) MilestonePlayChecks.CapturePreview("SelectedAugments_One.png");
                InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(Screen.width*.6f,Screen.height*.5f)});
                await UniTask.DelayFrame(3); await UniTask.Delay(150,ignoreTimeScale:true);
                Expect(!view.IsTooltipVisible,"마우스 이탈 시 설명 숨김");
                view.Show(Array.Empty<AugmentDefinition>(),true);
                InputSystem.QueueStateEvent(mouse,new MouseState {position=point}); await UniTask.DelayFrame(3);
                await UniTask.Delay(150,ignoreTimeScale:true);
                Expect(view.SelectedCount==0 && view.IsTooltipVisible && view.TooltipText.text.Contains("No augments selected"),"미선택 상태 안내");
                var preview=new[]{run.State.Data.Augments[5004],run.State.Data.Augments[5005],run.State.Data.Augments[5007]};
                view.Show(preview,true); await UniTask.Delay(180,ignoreTimeScale:true);
                Canvas.ForceUpdateCanvases(); view.TooltipText.ForceMeshUpdate();
                Expect(view.SelectedCount==3 && preview.All(definition=>view.TooltipText.text.Contains(definition.Name))
                    && !view.TooltipText.isTextOverflowing,"선택한 세 증강 전체 목록과 자동 높이");
                if(Application.isBatchMode)
                {
                    SynergyUIChecks.Capture(1920,1080,"SelectedAugments_Three_1920.png");
                    SynergyUIChecks.Capture(1280,720,"SelectedAugments_Three_1280.png");
                }
                view.Show(preview,false); view.OnPointerEnter(new PointerEventData(EventSystem.current));
                Expect(!view.CanHover && !view.IsTooltipVisible,"모달 열림 권한에서 호버 무시·설명 즉시 정리");
                view.Show(preview,true); view.OnPointerEnter(new PointerEventData(EventSystem.current));
                view.gameObject.SetActive(false); view.gameObject.SetActive(true);
                Expect(!view.IsTooltipVisible,"UI 재활성화 시 이전 호버 상태 제거");
                view.Show(run);
                Expect(view.SelectedCount==1 && view.CanHover && view.CountText.text=="AUGMENTS 1/3","실제 도전 선택으로 표시 복원");
                Debug.Log("SELECTED_AUGMENTS_PLAY_PASS "+checks+" hover-exit-empty-three-modal-raycast-reenable-layout");
            }
            finally { view.Show(run); }
        }
    }
}
#endif
