using AutoBattler.Data.Items;
using AutoBattler.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>아이템 이름·능력치를 포인터 옆에 표시하고 화면 안으로 위치를 제한하는 공용 패널.</summary>
    public class ItemTooltip : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private Vector2 pointerOffset = new Vector2(28f, -28f);
        [SerializeField, Min(0f)] private float edgePadding = 12f;

        public bool IsVisible => panel.gameObject.activeSelf;
        public int DefinitionId { get; private set; }
        public RectTransform Panel => panel;
        public Image Icon => icon;
        public TextMeshProUGUI NameText => nameText;
        public TextMeshProUGUI StatsText => statsText;

        public void Show(ItemDefinition result, Sprite sprite, Vector2 pointer, Camera eventCamera)
        {
            if (DefinitionId != result.Id || icon.sprite != sprite)
            {
                DefinitionId = result.Id;
                icon.sprite = sprite;
                nameText.text = result.Name;
                statsText.text = ItemText.Stats(result);
            }
            if (!IsVisible)
            {
                panel.gameObject.SetActive(true);
                panel.SetAsLastSibling();
            }
            MoveToPointer(pointer, eventCamera);
        }

        private void MoveToPointer(Vector2 pointer, Camera eventCamera)
        {
            TooltipPositionUtility.Place(panel, pointer, eventCamera, pointerOffset, edgePadding);
        }

        public void Hide()
        {
            panel.gameObject.SetActive(false);
            DefinitionId = 0;
            icon.sprite = null;
            nameText.text = string.Empty;
            statsText.text = string.Empty;
        }
    }
}
