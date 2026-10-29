using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>아이템 클릭과 드래그 입력 전달. 실제 장착은 보관함에서 처리.</summary>
    public sealed class ItemIconButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Image background;
        private int itemId;
        public event Action<int> Pressed;
        public event Action<ItemIconButton, PointerEventData> DragBegan, DragMoved, DragEnded;
        public event Action<ItemIconButton> DragCancelled;
        public Button Button => button;
        public Image Icon => icon;
        public int ItemId => itemId;
        private void Awake() => button.onClick.AddListener(Request);
        private void Request() { if (itemId != 0) Pressed?.Invoke(itemId); }
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && itemId != 0 && button.IsInteractable())
                DragBegan?.Invoke(this, eventData);
        }
        public void OnDrag(PointerEventData eventData) => DragMoved?.Invoke(this, eventData);
        public void OnEndDrag(PointerEventData eventData) => DragEnded?.Invoke(this, eventData);
        public void Show(int id, Sprite sprite, string name, bool selected, bool interactable)
        {
            itemId = id; icon.sprite = sprite; icon.enabled = sprite != null;
            label.text = name; button.interactable = interactable && id != 0;
            background.color = selected ? new Color(.15f, .42f, .45f) : new Color(.1f, .16f, .23f);
        }
        private void OnDisable() => DragCancelled?.Invoke(this);
        private void OnDestroy() => button.onClick.RemoveListener(Request);
    }
}
