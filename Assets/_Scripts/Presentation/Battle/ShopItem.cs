using System;
using AutoBattler.Data.Snapshot;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>상점 한 칸의 표시와 구매 요청. 재화와 기물 변경은 진행 시스템에서 처리.</summary>
    [DisallowMultipleComponent]
    public sealed class ShopItem : MonoBehaviour
    {
        [Header("구매 버튼")]
        [SerializeField] private Button buyButton;

        [Header("기물 표시")]
        [SerializeField] private GameObject content;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI iconPlaceholderText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI poolText;
        [SerializeField] private TextMeshProUGUI emptyText;

        [Header("표시 색상")]
        [SerializeField] private Color placeholderColor = new Color(0.25f, 0.4f, 0.55f);
        [SerializeField] private Color priceColor = Color.white;
        [SerializeField] private Color unaffordableColor = new Color(1f, 0.5f, 0.5f);

        public int SlotIndex { get; private set; } = -1;
        public int DisplayedUnitId { get; private set; }
        public bool IsEmpty => DisplayedUnitId == 0;
        public event Action<int> PurchaseRequested;

        public Button BuyButton => buyButton;
        public GameObject Content => content;
        public Image IconImage => iconImage;
        public TextMeshProUGUI IconPlaceholderText => iconPlaceholderText;
        public TextMeshProUGUI NameText => nameText;
        public TextMeshProUGUI PriceText => priceText;
        public TextMeshProUGUI PoolText => poolText;
        public TextMeshProUGUI EmptyText => emptyText;
#if UNITY_EDITOR
        public void ConfigureReferences(Button button, GameObject contentRoot, Image icon,
            TextMeshProUGUI placeholder, TextMeshProUGUI nameLabel, TextMeshProUGUI price,
            TextMeshProUGUI pool, TextMeshProUGUI empty)
        {
            buyButton=button; content=contentRoot; iconImage=icon; iconPlaceholderText=placeholder;
            nameText=nameLabel; priceText=price; poolText=pool; emptyText=empty;
        }
#endif

        public void BindSlot(int slotIndex) => SlotIndex = slotIndex;

        public void Show(UnitDefinition unit, Sprite icon, int remainingCopies, int gold, bool canPrepare)
        {
            bool hasUnit = unit != null;
            DisplayedUnitId = hasUnit ? unit.Id : 0;
            content.SetActive(hasUnit);
            emptyText.gameObject.SetActive(!hasUnit);
            buyButton.interactable = hasUnit && canPrepare && gold >= unit.Cost;
            iconImage.sprite = hasUnit ? icon : null;
            iconImage.color = icon != null ? Color.white : placeholderColor;
            iconPlaceholderText.gameObject.SetActive(icon == null);
            nameText.text = hasUnit ? unit.Name : "";
            priceText.text = hasUnit ? $"{unit.Cost} gold" : "";
            poolText.text = hasUnit ? $"Pool {remainingCopies}" : "";
            priceText.color = hasUnit && gold < unit.Cost ? unaffordableColor : priceColor;
            iconPlaceholderText.text = hasUnit && !string.IsNullOrEmpty(unit.Name) ? unit.Name.Substring(0, 1) : "?";
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                buyButton.onClick.AddListener(RequestPurchase);
        }

        private void RequestPurchase()
        {
            if (SlotIndex >= 0 && buyButton.IsInteractable())
                PurchaseRequested?.Invoke(SlotIndex);
        }

        private void OnDisable()
        {
            if (buyButton != null)
                buyButton.onClick.RemoveListener(RequestPurchase);
        }
    }
}
