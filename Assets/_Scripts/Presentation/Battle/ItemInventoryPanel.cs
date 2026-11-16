using System.Collections.Generic;
using System.Linq;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Items;
using AutoBattler.Managers;
using AutoBattler.Run;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>전장 왼쪽 보관함과 아이템 드래그 장착. 전투 시간과 배경 입력 유지.</summary>
    public sealed class ItemInventoryPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup modal;
        [SerializeField] private Button openButton, closeButton, previousPage, nextPage;
        [SerializeField] private TextMeshProUGUI openLabel, pageLabel;
        [SerializeField] private ItemIconButton[] storedSlots;
        [SerializeField] private ItemRecipePanel recipePanel;
        [Header("드래그 표시")]
        [SerializeField] private RectTransform dragPreview;
        [SerializeField] private Image dragIcon;
        [SerializeField] private ItemFusionPreview fusionPreview;
        [SerializeField, Min(1f)] private float targetScale = 1.18f;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private RunController run;
        private CanvasGroup underlying;
        private Canvas canvas;
        private ItemIconButton dragSource;
        private int selectedItem, draggedItem, pointerId, page;
        private Vector2 pointer;
        private Unit target;
        private ItemIconButton storedTarget;
        private int targetVersion;
        private string dropFailure;
        public bool IsOpen { get; private set; }
        public bool IsDraggingItem => draggedItem != 0;
        public Unit DropUnit => target;
        public int DropStoredItemId=>storedTarget!=null?storedTarget.ItemId:0;
        public Button OpenButton => openButton;
        public Button CloseButton => closeButton;
        public CanvasGroup Modal => modal;
        public ItemIconButton[] StoredSlots => storedSlots;
        public RectTransform DragPreview => dragPreview;
        public ItemFusionPreview FusionPreview => fusionPreview;

        public void Bind(RunController controller, CanvasGroup group)
        {
            Close();
            if (run != null) run.DisplayChanged -= Refresh;
            run = controller; underlying = group;
            if (run != null) run.DisplayChanged += Refresh;
            selectedItem = 0; page = 0; Refresh();
        }
        private void OnEnable()
        {
            canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (run != null) { run.DisplayChanged -= Refresh; run.DisplayChanged += Refresh; }
            openButton.onClick.AddListener(Toggle);
            closeButton.onClick.AddListener(Close);
            previousPage.onClick.AddListener(BackPage); nextPage.onClick.AddListener(ForwardPage);
            foreach (ItemIconButton slot in storedSlots)
            {
                slot.Pressed += SelectItem; slot.DragBegan += BeginItemDrag;
                slot.DragMoved += MoveItemDrag; slot.DragEnded += EndItemDrag;
                slot.DragCancelled += CancelSource;
            }
        }
        private bool CanOpen => run != null && run.IsReady && !run.IsShuttingDown
            && (!run.State.IsOver || run.State.HasPendingRoundReward)
            && underlying != null && underlying.interactable && !GameNavigation.IsTransitioning
            && !run.Flow.IsAugmentWindowOpen
            && (run.Flow.CurrentPhase != BattlePhase.Result || run.State.HasPendingRoundReward)
            && (GameManager.instance.Settings == null || !GameManager.instance.Settings.IsOpen);
        private bool CanDrag => IsOpen && CanOpen && run.Equipment.CanEquip;
        private void LateUpdate()
        {
            openButton.interactable = CanOpen;
            if (IsOpen && !CanOpen) Close();
            if (!IsDraggingItem) return;
            OwnedItem item = run != null && run.IsReady ? run.State.Items.Find(draggedItem) : null;
            if (!CanDrag || item == null || item.OwnerUnitId != 0) { CancelItemDrag(); return; }
            UpdateTarget();
        }
        private void Toggle() { if (IsOpen) Close(); else Open(); }
        public void Open()
        {
            if (IsOpen || !CanOpen) return;
            IsOpen = true; run.Equipment.SetInventoryOpen(true);
            modal.ignoreParentGroups = false; modal.interactable = true; modal.blocksRaycasts = true;
            modal.gameObject.SetActive(true); modal.transform.SetAsLastSibling();
            modal.DOKill(); modal.alpha = 0f; modal.DOFade(1f, .15f).SetUpdate(true);
            Refresh();
        }
        public void Close()
        {
            CancelItemDrag();
            if (!IsOpen) return;
            IsOpen = false;
            if (run != null) run.Equipment.SetInventoryOpen(false);
            selectedItem = 0;
            recipePanel?.Hide();
            modal.DOKill(); modal.gameObject.SetActive(false); ClearIcons();
        }
        private void SelectItem(int id) { selectedItem = selectedItem == id ? 0 : id; Refresh(); }
        private void BackPage() { CancelItemDrag(); selectedItem = 0; page--; Refresh(); }
        private void ForwardPage() { CancelItemDrag(); selectedItem = 0; page++; Refresh(); }
        private void BeginItemDrag(ItemIconButton source, PointerEventData eventData)
        {
            if (!CanDrag || IsDraggingItem) return;
            OwnedItem item = run.State.Items.Find(source.ItemId);
            if (item == null || item.OwnerUnitId != 0) return;
            run.Drag.CancelDrag(); run.Drag.SetItemDragging(true);
            draggedItem = item.InstanceId; dragSource = source; selectedItem = draggedItem;
            pointerId = eventData.pointerId; pointer = eventData.position;
            dragIcon.sprite = source.Icon.sprite;
            dragPreview.gameObject.SetActive(true); dragPreview.SetAsLastSibling();
            Refresh(); UpdateTarget();
        }
        private void MoveItemDrag(ItemIconButton source, PointerEventData eventData)
        {
            if (source != dragSource || eventData.pointerId != pointerId || !IsDraggingItem) return;
            pointer = eventData.position; UpdateTarget();
        }
        private void EndItemDrag(ItemIconButton source, PointerEventData eventData)
        {
            if (source != dragSource || eventData.pointerId != pointerId || !IsDraggingItem) return;
            pointer = eventData.position;
            if (CanDrag) UpdateTarget();
            Unit recipient = CanDrag ? target : null;
            int otherItemId=CanDrag?DropStoredItemId:0;
            int itemId = draggedItem;
            string failure=CanDrag ? dropFailure : null;
            // 장착 콜백에서 배치·풀 상태가 바뀌기 전에 대상 크기와 입력 복원.
            CancelItemDrag();
            if(otherItemId!=0 && run.FuseItems(itemId,otherItemId)) selectedItem=0;
            else if (recipient != null && run.EquipItem(itemId, recipient.InstanceId))
            { selectedItem = 0; run.Drag.Inspect(recipient); }
            else if(recipient==null && otherItemId==0 && failure!=null)
            {run.NotifyFailure(failure); SoundManager.PlayEffect(AutoBattler.Audio.SoundId.Denied);}
            Refresh();
        }
        private void CancelSource(ItemIconButton source) { if (source == dragSource) CancelItemDrag(); }
        public void CancelItemDrag()
        {
            bool wasDragging = IsDraggingItem;
            SetTarget(null);
            dropFailure=null;
            SetStoredTarget(null);
            draggedItem = 0; dragSource = null;
            if (run != null) run.Drag.SetItemDragging(false);
            if (dragPreview != null) dragPreview.gameObject.SetActive(false);
            if (dragIcon != null) dragIcon.sprite = null;
            if (fusionPreview != null) fusionPreview.Hide();
            if (wasDragging && IsOpen && CanOpen) Refresh();
        }
        private Camera UICamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        private void UpdateTarget()
        {
            dropFailure=null;
            uiHits.Clear();
            if(EventSystem.current!=null)
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) {position=pointer,pointerId=pointerId},uiHits);
            // 드래그 아이콘의 Rect 변경과 무관하게 연결된 보관 슬롯의 표시 범위로 판정.
            ItemIconButton hovered=storedSlots.FirstOrDefault(slot=>slot.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)slot.transform,pointer,UICamera));
            ItemDefinition fusion=hovered!=null
                ?run.State.Items.FusionResult(draggedItem,hovered.ItemId):null;
            SetStoredTarget(fusion!=null?hovered:null);
            if(hovered!=null && hovered.ItemId!=0 && hovered.ItemId!=draggedItem && fusion==null)
                dropFailure="두 아이템을 합성할 수 없습니다.";
            bool overInventory=RectTransformUtility.RectangleContainsScreenPoint((RectTransform)modal.transform,pointer,UICamera);
            SetTarget(!overInventory && uiHits.Count==0?FindTarget():null);
            ItemDefinition equippedFusion = null;
            if (target != null && run.State.Items.TryGetEquipFusion(draggedItem, target.InstanceId, out _, out equippedFusion))
                fusion = equippedFusion;
            OwnedItem dragged=run.State.Items.Find(draggedItem);
            if(dragged!=null) dragIcon.sprite=run.ItemIcon(fusion!=null?fusion.IconId:run.State.Data.Items[dragged.DefinitionId].IconId);
            dragIcon.color = target != null || storedTarget!=null ? Color.white : new Color(1f, 1f, 1f, .65f);
            if (fusion != null)
                fusionPreview.Show(fusion, run.ItemIcon(fusion.IconId), pointer, UICamera);
            else
                fusionPreview.Hide();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)dragPreview.parent,
                pointer, UICamera, out Vector2 local)) dragPreview.anchoredPosition = local;
        }
        private Unit FindTarget()
        {
            if (!CanDrag) return null;
            Vector3 world = ScreenPositionUtility.ScreenToWorld(run.Drag.BoardCamera, pointer, run.Grid.transform.position.z);
            foreach (Collider2D hit in Physics2D.OverlapPointAll(world))
            {
                Unit unit = hit.GetComponentInParent<Unit>();
                if(unit==null || !unit.isActiveAndEnabled) continue;
                if(unit.Team!=UnitTeam.Ally) {dropFailure="아군 유닛에 장착해 주세요.";continue;}
                if(!unit.Health.IsAlive || !run.Units.Allies.Contains(unit)) {dropFailure="살아 있는 아군 유닛에 장착해 주세요.";continue;}
                if (!run.State.Items.CanEquip(draggedItem,unit.InstanceId))
                {dropFailure="장착 칸이 가득 찼습니다. 합성 가능한 아이템을 사용해 주세요.";continue;}
                dropFailure=null;
                return unit;
            }
            return null;
        }
        private void SetStoredTarget(ItemIconButton next)
        {
            if(storedTarget==next) return;
            if(storedTarget!=null) storedTarget.SetDropHighlighted(false);
            storedTarget=next;
            if(storedTarget!=null) storedTarget.SetDropHighlighted(true);
        }
        private void SetTarget(Unit next)
        {
            if (target == next && (next == null || next.SpawnVersion == targetVersion)) return;
            if (target != null) target.DropFeedback.SetHighlighted(false);
            target = next;
            if (target == null) return;
            targetVersion = target.SpawnVersion;
            target.DropFeedback.SetHighlighted(true, targetScale);
        }
        private void Refresh()
        {
            bool ready = run != null && run.IsReady;
            openLabel.text = string.Empty;
            openLabel.gameObject.SetActive(false);
            if (!IsOpen) return;
            if (!ready || !CanOpen) { Close(); return; }
            OwnedItem[] stored = run.State.Items.Stored.ToArray();
            int pages = Mathf.Max(1, Mathf.CeilToInt(stored.Length / (float)storedSlots.Length));
            page = Mathf.Clamp(page, 0, pages - 1); pageLabel.text = $"{page + 1} / {pages}";
            previousPage.interactable = page > 0 && !IsDraggingItem;
            nextPage.interactable = page + 1 < pages && !IsDraggingItem;
            for (int i = 0; i < storedSlots.Length; i++)
            {
                int index = page * storedSlots.Length + i;
                OwnedItem item = index < stored.Length ? stored[index] : null;
                ItemDefinition definition = item != null ? run.State.Data.Items[item.DefinitionId] : null;
                storedSlots[i].Show(item?.InstanceId ?? 0, definition != null ? run.ItemIcon(definition.IconId) : null,
                    definition?.Name ?? "빈 슬롯", item != null && item.InstanceId == selectedItem, true);
            }
            OwnedItem selection = run.State.Items.Find(selectedItem);
            if (selection == null || selection.OwnerUnitId != 0) { selectedItem = 0; selection = null; }
            if (recipePanel != null)
            {
                if (selection != null && !IsDraggingItem) recipePanel.Show(run, selection, stored);
                else recipePanel.Hide();
            }
        }
        private void ClearIcons()
        { foreach (ItemIconButton slot in storedSlots) slot.Show(0, null, "빈 슬롯", false, false); }
        private void OnApplicationPause(bool paused) { if (paused) CancelItemDrag(); }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelItemDrag(); }
        private void OnDisable()
        {
            Close();
            openButton.onClick.RemoveListener(Toggle); closeButton.onClick.RemoveListener(Close);
            previousPage.onClick.RemoveListener(BackPage); nextPage.onClick.RemoveListener(ForwardPage);
            foreach (ItemIconButton slot in storedSlots)
            {
                slot.Pressed -= SelectItem; slot.DragBegan -= BeginItemDrag;
                slot.DragMoved -= MoveItemDrag; slot.DragEnded -= EndItemDrag;
                slot.DragCancelled -= CancelSource;
            }
            if (run != null) run.DisplayChanged -= Refresh;
        }
    }
    public static class ItemText
    {
        public static string Stats(ItemDefinition definition)
            =>string.Join("\n",definition.Modifiers.Select(modifier=>
            {
                bool percent=modifier.Operation!=ModifierOperation.Flat || modifier.Stat==StatType.CritChance;
                return $"{StatName(modifier.Stat)} +{(percent?modifier.Value*100f:modifier.Value):0.##}{(percent?"%":"")}";
            }));
        public static string Details(ItemDefinition definition)
        {
            string stats=Stats(definition);
            string effect=DescribeEffect(definition.CombatEffect);
            return string.IsNullOrEmpty(effect)?stats:$"{stats}\n<color=#F5CE86>{effect}</color>";
        }
        private static string Percent(float value)=>$"{value*100f:0.##}%";
        private static string DescribeEffect(ItemCombatEffect effect)=>effect.Kind switch
        {
            ItemEffectKind.BonusArmor=>$"방어력 {Percent(effect.Value)} 추가",
            ItemEffectKind.AdjacentStartShield=>$"전투 시작 시 인접 아군에게 장착자 최대 체력의 {Percent(effect.Value)} 보호막 부여 ({effect.Seconds:0.#}초, 중첩 가능)",
            ItemEffectKind.AttackAndArmorStacks=>$"기본 공격 적중 또는 피격 시 공격력과 방어력 +{effect.Value:0.##} (최대 {effect.MaxStacks}중첩)",
            ItemEffectKind.ArmorShred=>$"기본 공격 적중 시 대상 방어력 {Percent(effect.Value)} 감소 ({effect.Seconds:0.#}초)",
            ItemEffectKind.LowHealthShield=>$"피격 후 생존하고 체력이 {Percent(effect.SecondaryValue)} 이하라면 최대 체력의 {Percent(effect.Value)} 보호막 획득 ({effect.Seconds:0.#}초, 전투당 1회)",
            ItemEffectKind.BonusHealth=>$"최대 체력 {Percent(effect.Value)} 추가",
            ItemEffectKind.RandomStartStat=>$"전투 시작 시 최대 체력 +{effect.Value:0.##} 또는 공격 속도 +{Percent(effect.SecondaryValue)} 중 하나 무작위 적용",
            ItemEffectKind.Omnivamp=>$"모든 피해 흡혈 {Percent(effect.Value)}",
            ItemEffectKind.AdjacentPeriodicHeal=>$"{effect.Seconds:0.#}초마다 자신과 인접 아군 중 부상 유닛 1기를 무작위로 골라 최대 체력의 {Percent(effect.Value)} 회복",
            ItemEffectKind.AttackSpeedStacks=>$"기본 공격 적중 시 공격 속도 +{Percent(effect.Value)} (최대 {effect.MaxStacks}중첩)",
            ItemEffectKind.AttackStacks=>$"기본 공격 적중 시 공격력 +{effect.Value:0.##} (최대 {effect.MaxStacks}중첩)",
            ItemEffectKind.SkillCritical=>$"스킬 피해에 치명타 적용, 치명타 확률 +{Percent(effect.Value)}p",
            ItemEffectKind.ManaOnHit=>$"기본 공격 적중 시 MP {effect.Value:0.##} 회복",
            ItemEffectKind.FirstCastMana=>$"첫 스킬 사용 직후 MP {effect.Value:0.##} 회복 (전투당 1회)",
            _=>string.Empty
        };
        private static string StatName(StatType stat)=>stat switch
        {
            StatType.MaxHp=>"최대 체력",StatType.AttackDamage=>"공격력",StatType.Defense=>"방어력",StatType.AttackSpeed=>"공격 속도",
            StatType.MoveSpeed=>"이동 속도",StatType.SkillPower=>"스킬 위력",StatType.CritChance=>"치명타 확률",
            StatType.CritMultiplier=>"치명타 배율",StatType.BasicAttackPower=>"기본 공격 위력",StatType.Mana=>"MP",StatType.BasicArmorIgnore=>"기본 공격 방어 관통",_=>stat.ToString()
        };
    }
}
