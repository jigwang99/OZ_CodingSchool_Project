using System;
using System.Collections.Generic;
using System.Linq;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Data.Snapshot;
using AutoBattler.Data.Traits;
using AutoBattler.Managers;
using AutoBattler.Run;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>전투와 독립된 편성 미리보기. 프리팹의 목록·배치 칸·시너지 표시만 갱신.</summary>
    public sealed class DeckPlannerPanel : MonoBehaviour
    {
        [SerializeField] private Button openButton,closeButton,clearButton,importButton,previousButton,nextButton;
        [SerializeField] private Button[] costFilters;
        [SerializeField] private CanvasGroup modal;
        [SerializeField] private DeckPlannerSlot[] catalogSlots,boardSlots;
        [SerializeField] private SynergyPanel synergies;
        [SerializeField] private TextMeshProUGUI countText,pageText,selectionText;
        [SerializeField] private RectTransform dragPreview;
        [SerializeField] private Image dragIcon;
        private readonly List<Action> filterActions=new List<Action>();
        private RunController run;
        private CanvasGroup underlying;
        private ItemInventoryPanel inventory;
        private Canvas canvas;
        private DataRepository data;
        private DeckPlannerSlot dragSource;
        private HexCoord? hoveredCell;
        private int selectedUnit,page,costFilter,draggedUnit,pointerId;
        private bool previousInteraction,previousRaycasts;
        private GameObject previousSelection;
        public bool IsOpen { get; private set; }
        public bool IsDragging=>dragSource!=null;
        public bool IsLoadingPortraits=>run!=null && run.Assets.IsLoadingUnitPortraits;
        public DeckPlanState Plan { get; private set; }
        public Button OpenButton=>openButton;
        public Button CloseButton=>closeButton;
        public Button ClearButton=>clearButton;
        public Button ImportButton=>importButton;
        public Button PreviousButton=>previousButton;
        public Button NextButton=>nextButton;
        public IReadOnlyList<Button> CostFilters=>Array.AsReadOnly(costFilters);
        public IReadOnlyList<DeckPlannerSlot> CatalogSlots=>Array.AsReadOnly(catalogSlots);
        public IReadOnlyList<DeckPlannerSlot> BoardSlots=>Array.AsReadOnly(boardSlots);
        public CanvasGroup Modal=>modal;
        public SynergyPanel Synergies=>synergies;
        public TextMeshProUGUI CountText=>countText;
        public TextMeshProUGUI PageText=>pageText;
        public TextMeshProUGUI SelectionText=>selectionText;
        public int SelectedUnitId=>selectedUnit;
        public int PortraitCount=>run!=null?run.Assets.UnitPortraitHandleCount:0;

        public void Bind(RunController controller,CanvasGroup group,ItemInventoryPanel bag)
        {
            Close();
            if(run!=controller)
            {
                if(Plan!=null) Plan.Changed-=Refresh;
                Plan=null; data=null; selectedUnit=0; page=0; costFilter=0;
            }
            run=controller; underlying=group; inventory=bag;
        }
        private void OnEnable()
        {
            if(!Application.isPlaying) return;
            canvas=GetComponentInParent<Canvas>()?.rootCanvas;
            openButton.onClick.AddListener(Open); closeButton.onClick.AddListener(Close);
            clearButton.onClick.AddListener(Clear); importButton.onClick.AddListener(ImportCurrent);
            previousButton.onClick.AddListener(Previous); nextButton.onClick.AddListener(Next);
            for(int index=0;index<costFilters.Length;index++)
            {
                int cost=index; Action action=()=>Filter(cost); filterActions.Add(action);
                costFilters[index].onClick.AddListener(action.Invoke);
            }
        }
        private bool CanRemain=>run!=null && run.IsReady && !run.IsShuttingDown && !run.State.IsOver
            && !GameNavigation.IsTransitioning && !run.Flow.IsAugmentWindowOpen;
        private bool CanOpen=>CanRemain && underlying!=null && underlying.interactable
            && (GameManager.instance.Settings==null || !GameManager.instance.Settings.IsOpen);
        public void Open()
        {
            if(IsOpen || !CanOpen) return;
            inventory?.Close(); run.Drag.CancelDrag(); run.Drag.SetPlanning(true);
            if(Plan==null)
            {
                data=run.State.Data; Plan=new DeckPlanState(data); Plan.Changed+=Refresh;
            }
            previousInteraction=underlying.interactable; previousRaycasts=underlying.blocksRaycasts;
            previousSelection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            underlying.interactable=false; underlying.blocksRaycasts=false;
            IsOpen=true; modal.ignoreParentGroups=true; modal.interactable=true; modal.blocksRaycasts=true;
            transform.SetAsLastSibling();
            modal.gameObject.SetActive(true); modal.transform.SetAsLastSibling();
            modal.DOKill(); modal.alpha=0f; modal.DOFade(1f,.15f).SetUpdate(true);
            if(EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
            Refresh();
        }
        public void Close()
        {
            CancelDrag();
            if(!IsOpen) return;
            IsOpen=false; modal.DOKill(); modal.gameObject.SetActive(false);
            if(underlying!=null) {underlying.interactable=previousInteraction; underlying.blocksRaycasts=previousRaycasts;}
            if(run!=null) run.Drag.SetPlanning(false);
            if(EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(previousSelection!=null && previousSelection.activeInHierarchy?previousSelection:null);
            previousSelection=null;
        }
        private void LateUpdate()
        {
            openButton.interactable=!IsOpen && CanOpen;
            if(IsOpen && !CanRemain) Close();
            if(IsOpen && Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
            { if(IsDragging) CancelDrag(); else Close(); }
        }
        private void Clear() {CancelDrag(); selectedUnit=0; Plan?.Clear(); Refresh();}
        private void ImportCurrent()
        {
            if(!IsOpen || !CanRemain) return;
            CancelDrag(); selectedUnit=0;
            Plan.Load(run.State.Units.Where(unit=>unit.Placement.Location.Kind==UnitLocationKind.Board)
                .Select(unit=>new KeyValuePair<HexCoord,int>(unit.Placement.Cell.Value,unit.DefinitionId)));
        }
        private void Previous() {CancelDrag(); page--; Refresh();}
        private void Next() {CancelDrag(); page++; Refresh();}
        private void Filter(int cost) {CancelDrag(); costFilter=cost; page=0; Refresh();}
        private Sprite Portrait(UnitDefinition unit)=>run.Assets.UnitPortrait(unit.VisualId);
        private void Refresh()
        {
            if(!IsOpen || Plan==null) return;
            UnitDefinition[] candidates=data.Units.Values.Where(unit=>costFilter==0 || unit.Cost==costFilter)
                .OrderBy(unit=>unit.Cost).ThenBy(unit=>unit.Id).ToArray();
            int pages=Mathf.Max(1,Mathf.CeilToInt(candidates.Length/(float)catalogSlots.Length));
            page=Mathf.Clamp(page,0,pages-1); pageText.text=$"{page+1} / {pages}";
            previousButton.interactable=page>0; nextButton.interactable=page+1<pages;
            for(int index=0;index<catalogSlots.Length;index++)
            {
                int offset=page*catalogSlots.Length+index;
                UnitDefinition unit=offset<candidates.Length?candidates[offset]:null;
                catalogSlots[index].gameObject.SetActive(unit!=null);
                catalogSlots[index].Show(unit,unit!=null?Portrait(unit):null,unit!=null && unit.Id==selectedUnit);
            }
            foreach(DeckPlannerSlot cell in boardSlots)
            {
                int id=Plan.UnitAt(cell.Cell); UnitDefinition unit=id!=0?data.Units[id]:null;
                cell.Show(unit,unit!=null?Portrait(unit):null,hoveredCell==cell.Cell);
            }
            for(int index=0;index<costFilters.Length;index++) costFilters[index].interactable=index!=costFilter;
            countText.text=$"편성 {Plan.Count}/{DeckPlanState.Capacity}";
            selectionText.text=selectedUnit!=0 && data.Units.TryGetValue(selectedUnit,out UnitDefinition selection)
                ?$"{selection.Name} · {BattleText.TraitName(TraitCategory.Origin,selection.OriginId,"")} / {BattleText.TraitName(TraitCategory.Class,selection.ClassId,"")}":string.Empty;
            selectionText.gameObject.SetActive(selectionText.text.Length>0);
            synergies.Show(Plan.Synergies,true,BattlePhase.Preparation);
            synergies.EmptyText.text=string.Empty;
            synergies.EmptyText.gameObject.SetActive(false);
        }
        public void Press(DeckPlannerSlot slot,PointerEventData eventData)
        {
            if(!IsOpen || !CanRemain || IsDragging) return;
            if(eventData.button==PointerEventData.InputButton.Right)
            { if(slot.IsBoardCell) Plan.Remove(slot.Cell); return; }
            if(eventData.button!=PointerEventData.InputButton.Left) return;
            if(!slot.IsBoardCell) {selectedUnit=slot.UnitId; Refresh();}
            else if(selectedUnit!=0) Plan.TryPlace(selectedUnit,slot.Cell);
            else if(slot.UnitId!=0) {selectedUnit=slot.UnitId; Refresh();}
        }
        public void BeginDrag(DeckPlannerSlot source,PointerEventData eventData)
        {
            if(!IsOpen || !CanRemain || IsDragging || source.UnitId==0 || eventData.button!=PointerEventData.InputButton.Left) return;
            dragSource=source; draggedUnit=source.UnitId; pointerId=eventData.pointerId; selectedUnit=draggedUnit;
            dragIcon.sprite=Portrait(data.Units[draggedUnit]); dragPreview.gameObject.SetActive(true); dragPreview.SetAsLastSibling();
            MoveDrag(source,eventData);
        }
        private Camera UICamera=>canvas!=null && canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
        private DeckPlannerSlot CellAt(Vector2 pointer)=>boardSlots.FirstOrDefault(slot=>slot.isActiveAndEnabled
            && slot.Contains(pointer,UICamera));
        public void MoveDrag(DeckPlannerSlot source,PointerEventData eventData)
        {
            if(source!=dragSource || eventData.pointerId!=pointerId || !IsDragging) return;
            DeckPlannerSlot target=CellAt(eventData.position); hoveredCell=target!=null?target.Cell:(HexCoord?)null;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)dragPreview.parent,eventData.position,UICamera,out Vector2 position))
                dragPreview.anchoredPosition=position;
            Refresh();
        }
        public void EndDrag(DeckPlannerSlot source,PointerEventData eventData)
        {
            if(source!=dragSource || eventData.pointerId!=pointerId || !IsDragging) return;
            DeckPlannerSlot target=IsOpen && CanRemain?CellAt(eventData.position):null;
            int id=draggedUnit; bool move=source.IsBoardCell; HexCoord from=source.Cell;
            CancelDrag();
            if(target==null) return;
            if(move) Plan.TryMove(from,target.Cell);
            else Plan.TryPlace(id,target.Cell);
        }
        public void CancelDrag()
        {
            dragSource=null; draggedUnit=0; hoveredCell=null;
            if(dragPreview!=null) dragPreview.gameObject.SetActive(false);
            if(dragIcon!=null) dragIcon.sprite=null;
            Refresh();
        }
        private void OnApplicationFocus(bool focused) {if(!focused) CancelDrag();}
        private void OnApplicationPause(bool paused) {if(paused) CancelDrag();}
        private void OnDisable()
        {
            Close();
            if(openButton==null) return;
            openButton.onClick.RemoveListener(Open); closeButton.onClick.RemoveListener(Close);
            clearButton.onClick.RemoveListener(Clear); importButton.onClick.RemoveListener(ImportCurrent);
            previousButton.onClick.RemoveListener(Previous); nextButton.onClick.RemoveListener(Next);
            for(int index=0;index<filterActions.Count;index++) costFilters[index].onClick.RemoveListener(filterActions[index].Invoke);
            filterActions.Clear();
        }
    }
}
