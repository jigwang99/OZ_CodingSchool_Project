using System;
using System.Linq;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Items;
using AutoBattler.Run;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>아이템 보관함 표시, 대상 선택, 장착·해제 입력 연결.</summary>
    public sealed class ItemInventoryPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup modal;
        [SerializeField] private Button openButton,closeButton,equipButton;
        [SerializeField] private Button previousPage,nextPage,previousUnit,nextUnit;
        [SerializeField] private TextMeshProUGUI openLabel,pageLabel,detailLabel,unitLabel,hintLabel;
        [SerializeField] private ItemIconButton[] storedSlots;
        [SerializeField] private ItemIconButton[] equippedSlots;
        private RunController run;
        private CanvasGroup underlying;
        private bool previousInteraction,previousRaycasts,previousDrag;
        private int selectedItem,targetUnit,page;
        public bool IsOpen { get; private set; }
        public Button OpenButton=>openButton;
        public Button CloseButton=>closeButton;
        public Button EquipButton=>equipButton;
        public CanvasGroup Modal=>modal;
        public ItemIconButton[] StoredSlots=>storedSlots;
        public ItemIconButton[] EquippedSlots=>equippedSlots;
        public int TargetUnitId=>targetUnit;
        public void Bind(RunController controller,CanvasGroup group)
        {
            Close();
            if(run!=null) run.DisplayChanged-=Refresh;
            run=controller; underlying=group;
            if(run!=null) run.DisplayChanged+=Refresh;
            selectedItem=0; targetUnit=0; page=0; Refresh();
        }
        private void OnEnable()
        {
            openButton.onClick.AddListener(Open); closeButton.onClick.AddListener(Close);
            equipButton.onClick.AddListener(Equip);
            previousPage.onClick.AddListener(BackPage); nextPage.onClick.AddListener(ForwardPage);
            previousUnit.onClick.AddListener(BackUnit); nextUnit.onClick.AddListener(ForwardUnit);
            foreach(ItemIconButton slot in storedSlots) slot.Pressed+=SelectItem;
            foreach(ItemIconButton slot in equippedSlots) slot.Pressed+=Unequip;
        }
        private bool CanOpen=>run!=null && run.IsReady && !run.IsShuttingDown && !run.State.IsOver
            && underlying!=null && !GameNavigation.IsTransitioning && !run.Flow.IsAugmentWindowOpen
            && run.Flow.CurrentPhase!=AutoBattler.Battle.Flow.BattlePhase.Result
            && (AutoBattler.Managers.GameManager.instance.Settings==null
                || !AutoBattler.Managers.GameManager.instance.Settings.IsOpen);
        private void LateUpdate()=>openButton.interactable=!IsOpen && CanOpen;
        public void Open()
        {
            if(IsOpen || !CanOpen) return;
            targetUnit=run.Drag.SelectedUnit!=null && run.Drag.SelectedUnit.State!=null
                ?run.Drag.SelectedUnit.State.InstanceId:0;
            run.Drag.CancelDrag(); previousDrag=run.Drag.enabled; run.Drag.enabled=false;
            previousInteraction=underlying.interactable; previousRaycasts=underlying.blocksRaycasts;
            underlying.interactable=false; underlying.blocksRaycasts=false;
            IsOpen=true; run.Equipment.SetInventoryOpen(true);
            modal.ignoreParentGroups=true; modal.gameObject.SetActive(true); modal.transform.SetAsLastSibling();
            modal.DOKill(); modal.alpha=0f; modal.DOFade(1f,.15f).SetUpdate(true);
            Refresh();
            if(EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }
        public void Close()
        {
            if(!IsOpen) return;
            IsOpen=false; modal.DOKill(); modal.gameObject.SetActive(false);
            if(underlying!=null) { underlying.interactable=previousInteraction; underlying.blocksRaycasts=previousRaycasts; }
            if(run!=null)
            {
                run.Equipment.SetInventoryOpen(false);
                if(!run.IsShuttingDown && run.IsReady) run.Drag.enabled=previousDrag;
            }
            ClearIcons();
            if(EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(null);
        }
        private void SelectItem(int id) { selectedItem=id; Refresh(); }
        private void Equip()
        { if(IsOpen && run.EquipItem(selectedItem,targetUnit)) { selectedItem=0; Refresh(); } }
        private void Unequip(int id) { if(IsOpen && run.UnequipItem(id)) Refresh(); }
        private void BackPage() { page--; Refresh(); }
        private void ForwardPage() { page++; Refresh(); }
        private void BackUnit()=>ChangeUnit(-1);
        private void ForwardUnit()=>ChangeUnit(1);
        private void ChangeUnit(int delta)
        {
            int[] ids=run.State.Units.Select(unit=>unit.Placement.InstanceId).ToArray();
            if(ids.Length==0) return;
            int index=Array.IndexOf(ids,targetUnit); targetUnit=ids[(index+delta+ids.Length)%ids.Length]; Refresh();
        }
        private void Refresh()
        {
            bool ready=run!=null && run.IsReady;
            openLabel.text=ready?$"인벤토리 ({run.State.Items.Stored.Count()})":"인벤토리";
            if(!IsOpen) return;
            if(!ready || !CanOpen) { Close(); return; }
            RunState state=run.State;
            OwnedItem[] stored=state.Items.Stored.ToArray();
            int pages=Mathf.Max(1,Mathf.CeilToInt(stored.Length/(float)storedSlots.Length));
            page=Mathf.Clamp(page,0,pages-1); pageLabel.text=$"{page+1} / {pages}";
            previousPage.interactable=page>0; nextPage.interactable=page+1<pages;
            for(int i=0;i<storedSlots.Length;i++)
            {
                int index=page*storedSlots.Length+i;
                OwnedItem item=index<stored.Length?stored[index]:null;
                ShowItem(storedSlots[i],item,item!=null && item.InstanceId==selectedItem,true);
            }
            OwnedUnit target=state.Units.FirstOrDefault(unit=>unit.Placement.InstanceId==targetUnit)
                ??state.Units.FirstOrDefault();
            targetUnit=target!=null?target.Placement.InstanceId:0;
            unitLabel.text=target==null?"보유한 유닛 없음":$"{BattleText.UnitName(state.Data.Units[target.DefinitionId])}  {target.Star}성";
            previousUnit.interactable=nextUnit.interactable=state.Units.Count>1;
            OwnedItem[] worn=state.Items.Equipped(targetUnit).ToArray();
            for(int i=0;i<equippedSlots.Length;i++)
                ShowItem(equippedSlots[i],worn.FirstOrDefault(item=>item.Slot==i),false,run.Equipment.CanEquip);
            OwnedItem selection=state.Items.Find(selectedItem);
            if(selection==null || selection.OwnerUnitId!=0) { selectedItem=0; selection=null; }
            detailLabel.text=selection==null?(stored.Length==0?"전투 승리 시 아이템을 획득합니다.":"보관 중인 아이템을 선택하세요.")
                :$"{state.Data.Items[selection.DefinitionId].Name}\n\n{state.Data.Items[selection.DefinitionId].Description}\n\n{ItemText.Stats(state.Data.Items[selection.DefinitionId])}";
            equipButton.interactable=selection!=null && target!=null && worn.Length<ItemInventoryState.EquipmentCapacity && run.Equipment.CanEquip;
            hintLabel.text=run.Equipment.CanEquip?"아이템 선택 후 장착 · 장착한 아이콘 클릭 시 해제":"준비 단계에서 장착·해제 가능";
            hintLabel.gameObject.SetActive(true);
        }
        private void ShowItem(ItemIconButton slot,OwnedItem item,bool selected,bool interactable)
        {
            ItemDefinition definition=item!=null?run.State.Data.Items[item.DefinitionId]:null;
            slot.Show(item?.InstanceId??0,definition!=null?run.ItemIcon(definition.IconId):null,
                definition?.Name??"빈 슬롯",selected,interactable);
        }
        private void ClearIcons()
        {
            foreach(ItemIconButton slot in storedSlots) slot.Show(0,null,"빈 슬롯",false,false);
            foreach(ItemIconButton slot in equippedSlots) slot.Show(0,null,"빈 슬롯",false,false);
        }
        private void OnDisable()
        {
            Close();
            openButton.onClick.RemoveListener(Open); closeButton.onClick.RemoveListener(Close); equipButton.onClick.RemoveListener(Equip);
            previousPage.onClick.RemoveListener(BackPage); nextPage.onClick.RemoveListener(ForwardPage);
            previousUnit.onClick.RemoveListener(BackUnit); nextUnit.onClick.RemoveListener(ForwardUnit);
            foreach(ItemIconButton slot in storedSlots) slot.Pressed-=SelectItem;
            foreach(ItemIconButton slot in equippedSlots) slot.Pressed-=Unequip;
            if(run!=null) run.DisplayChanged-=Refresh;
        }
    }
    public static class ItemText
    {
        public static string Stats(ItemDefinition definition)=>string.Join("\n",definition.Modifiers.Select(modifier=>
            $"{StatName(modifier.Stat)} +{(modifier.Operation==ModifierOperation.Percent || modifier.Stat==StatType.CritChance?modifier.Value*100f:modifier.Value):0.##}{(modifier.Operation==ModifierOperation.Percent || modifier.Stat==StatType.CritChance?"%":"")}"));
        private static string StatName(StatType stat)=>stat switch
        {
            StatType.MaxHp=>"최대 체력",StatType.AttackDamage=>"공격력",StatType.Defense=>"방어력",StatType.AttackSpeed=>"공격 속도",
            StatType.MoveSpeed=>"이동 속도",StatType.SkillPower=>"스킬 위력",StatType.CritChance=>"치명타 확률",
            StatType.CritMultiplier=>"치명타 배율",StatType.BasicAttackPower=>"기본 공격 위력",_=>stat.ToString()
        };
    }
}
