using AutoBattler.Run;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>골드 아이콘·숫자 호버 중 실시간 예상 정산 패널 표시.</summary>
    public sealed class GoldIncomeHover : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
    {
        [SerializeField] private GoldIncomeView view;
        private RunController run;
        private CanvasGroup inputRoot;
        private ItemInventoryPanel inventory;
        private bool hovered;
        private Vector2 pointer;
        private Camera eventCamera;
        public GoldIncomeView View => view;
        public void Bind(RunController controller, CanvasGroup group, ItemInventoryPanel bag)
        { Clear(); run = controller; inputRoot = group; inventory = bag; }
        private bool CanShow => isActiveAndEnabled && run != null && run.IsReady && !run.IsShuttingDown
            && (!run.State.IsOver || run.State.HasPendingRoundReward)
            && inputRoot != null && inputRoot.interactable && inputRoot.blocksRaycasts
            && !GameNavigation.IsTransitioning && !run.Flow.IsAugmentWindowOpen
            && !inventory.IsDraggingItem && run.Drag.DraggedUnit == null;
        public void OnPointerEnter(PointerEventData eventData) => OnPointerMove(eventData);
        public void OnPointerMove(PointerEventData eventData)
        {
            if (!CanShow || eventData.dragging) { Clear(); return; }
            hovered = true; pointer = eventData.position; eventCamera = eventData.enterEventCamera;
            Refresh();
        }
        private void Refresh()
        {
            if (!CanShow) { Clear(); return; }
            view.Show(run.State.PreviewRoundIncome(true),
                run.State.HasPendingRoundReward, pointer, eventCamera);
        }
        private void LateUpdate() { if (hovered) Refresh(); }
        public void OnPointerExit(PointerEventData eventData) => Clear();
        private void Clear() { hovered = false; if (view != null) view.Hide(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Clear(); }
        private void OnApplicationPause(bool paused) { if (paused) Clear(); }
        private void OnDisable() => Clear();
    }
}
