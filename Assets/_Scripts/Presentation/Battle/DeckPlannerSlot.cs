using AutoBattler.Battle.Grid;
using AutoBattler.Data.Snapshot;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>유닛 목록 카드와 배치 칸의 클릭·드래그 입력 전달.</summary>
    public sealed class DeckPlannerSlot : MonoBehaviour,IPointerClickHandler,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        [SerializeField] private DeckPlannerPanel owner;
        [SerializeField] private bool boardCell;
        [SerializeField] private int column,row;
        [SerializeField] private Image portrait,background;
        [SerializeField] private PlannerHexGraphic hex;
        [SerializeField] private TextMeshProUGUI nameText,costText;
        [SerializeField] private Color normalColor=Color.white;
        [SerializeField] private Color selectedColor=new Color(.6f,1f,.85f,1f);
        public bool IsBoardCell=>boardCell;
        public HexCoord Cell=>HexCoord.FromOffset(column,row);
        public int UnitId { get; private set; }
        public Image Portrait=>portrait;
        public bool Contains(Vector2 point,Camera camera)=>hex!=null && hex.IsRaycastLocationValid(point,camera);
        public void Show(UnitDefinition unit,Sprite sprite,bool selected)
        {
            UnitId=unit?.Id ?? 0;
            portrait.sprite=sprite; portrait.enabled=sprite!=null;
            nameText.text=boardCell?string.Empty:unit?.Name ?? string.Empty;
            costText.text=boardCell || unit==null?string.Empty:CurrencyText.Amount(unit.Cost);
            if(background!=null) background.color=selected?selectedColor:normalColor;
            if(hex!=null) hex.SetState(unit!=null,selected);
        }
        public void OnPointerClick(PointerEventData eventData)
        { if(!eventData.dragging) owner.Press(this,eventData); }
        public void OnBeginDrag(PointerEventData eventData)=>owner.BeginDrag(this,eventData);
        public void OnDrag(PointerEventData eventData)=>owner.MoveDrag(this,eventData);
        public void OnEndDrag(PointerEventData eventData)=>owner.EndDrag(this,eventData);
    }
}
