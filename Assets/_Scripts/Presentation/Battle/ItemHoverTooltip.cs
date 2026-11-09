using System.Collections.Generic;
using AutoBattler.Data.Items;
using AutoBattler.Run;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>보관함·유닛 정보창의 슬롯을 연결해 하나의 호버 패널로 표시.</summary>
    public sealed class ItemHoverTooltip : MonoBehaviour
    {
        [SerializeField] private ItemTooltip view;
        private readonly List<ItemIconButton> slots = new List<ItemIconButton>();
        private RunController run;
        private CanvasGroup inputRoot;
        private ItemInventoryPanel inventory;
        private ItemIconButton hovered;
        private Vector2 pointer;
        private Camera eventCamera;
        public ItemTooltip View => view;

        public void Bind(RunController controller, CanvasGroup group, ItemInventoryPanel bag, IEnumerable<ItemIconButton> sources)
        {
            Clear();
            foreach (ItemIconButton slot in slots)
            {
                if (slot == null) continue;
                slot.Hovered -= Show;
                slot.HoverEnded -= Exit;
            }
            slots.Clear();
            run = controller;
            inputRoot = group;
            inventory = bag;
            if (sources == null) return;
            foreach (ItemIconButton slot in sources)
            {
                slots.Add(slot);
                slot.Hovered += Show;
                slot.HoverEnded += Exit;
            }
        }

        private bool CanShow => isActiveAndEnabled && run != null && run.IsReady && !run.IsShuttingDown
            && inputRoot != null && inputRoot.interactable && inputRoot.blocksRaycasts
            && !GameNavigation.IsTransitioning && !run.Flow.IsAugmentWindowOpen
            && !inventory.IsDraggingItem && run.Drag.DraggedUnit == null;

        private void Show(ItemIconButton slot, PointerEventData eventData)
        {
            if (!CanShow) { Clear(); return; }
            hovered = slot;
            pointer = eventData.position;
            eventCamera = eventData.enterEventCamera;
            Refresh();
        }

        private void Refresh()
        {
            if (!CanShow || hovered == null || !hovered.isActiveAndEnabled || hovered.ItemId == 0)
            { Clear(); return; }
            OwnedItem item = run.State.Items.Find(hovered.ItemId);
            if (item == null) { Clear(); return; }
            ItemDefinition definition = run.State.Data.Items[item.DefinitionId];
            view.Show(definition, run.ItemIcon(definition.IconId), pointer, eventCamera);
        }

        private void LateUpdate()
        {
            if (hovered != null) Refresh();
        }
        private void Exit(ItemIconButton slot) { if (hovered == slot) Clear(); }
        private void Clear()
        {
            hovered = null;
            if (view != null) view.Hide();
        }
        private void OnApplicationFocus(bool focused) { if (!focused) Clear(); }
        private void OnApplicationPause(bool paused) { if (paused) Clear(); }
        private void OnDisable() => Clear();
        private void OnDestroy() => Bind(null, null, null, null);
    }
}
