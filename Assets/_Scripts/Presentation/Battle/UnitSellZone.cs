using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>기물을 드래그하는 동안 상점을 대신하는 판매 영역 표시.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitSellZone : MonoBehaviour
    {
        [SerializeField] private RectTransform area;
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI hintText;
        private bool canSell;
        public bool IsVisible=>gameObject.activeSelf;
        public bool IsHovered { get; private set; }
        public RectTransform Area=>area;
        public TextMeshProUGUI PriceText=>priceText;
        public TextMeshProUGUI HintText=>hintText;
        public void Show(string name,int star,int price,bool allowed)
        {
            canSell=allowed; IsHovered=false;
            titleText.text=allowed?"판매":"판매 불가";
            priceText.text=$"{name}  {new string('★',star)}  +{price}골드";
            gameObject.SetActive(true); panel.DOKill(); panel.alpha=0f;
            panel.DOFade(1f,.08f).SetUpdate(true); UpdateAppearance();
        }
        public bool Contains(Vector2 screenPosition)=>IsVisible && gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(area,screenPosition,
                canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera);
        public void SetPointer(Vector2 screenPosition)
        { IsHovered=Contains(screenPosition); UpdateAppearance(); }
        private void UpdateAppearance()
        {
            background.color=!canSell?new Color(.15f,.16f,.2f)
                :IsHovered?new Color(.58f,.17f,.2f):new Color(.3f,.11f,.15f);
            hintText.text=!canSell?"증강 선택 후 판매할 수 있습니다."
                :IsHovered?"놓아서 판매":"여기에 유닛을 놓으면 판매";
        }
        public void Hide()
        { panel.DOKill(); panel.alpha=0f; IsHovered=false; gameObject.SetActive(false); }
        private void OnDisable() { if(panel!=null) panel.DOKill(); }
    }
}
