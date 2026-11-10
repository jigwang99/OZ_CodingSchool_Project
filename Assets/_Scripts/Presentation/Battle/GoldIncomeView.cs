using AutoBattler.Run;
using AutoBattler.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>정산 예상 수입을 기본·이자·연승·증강으로 나눠 표시.</summary>
    public sealed class GoldIncomeView : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private TextMeshProUGUI basicText, interestText, streakText, augmentText;
        [SerializeField] private GameObject augmentRow;
        [SerializeField] private Vector2 pointerOffset = new Vector2(24f, -24f);
        [SerializeField, Min(0f)] private float edgePadding = 12f;
        private RoundIncome displayedWin;
        private bool displayedKnown;
        public bool IsVisible => panel.gameObject.activeSelf;
        public RectTransform Panel => panel;
        public TextMeshProUGUI BasicText => basicText;
        public TextMeshProUGUI InterestText => interestText;
        public TextMeshProUGUI StreakText => streakText;
        public TextMeshProUGUI AugmentText => augmentText;

        public void Show(RoundIncome win, bool known, Vector2 pointer, Camera eventCamera)
        {
            bool changed = !IsVisible || !win.Equals(displayedWin) || displayedKnown != known;
            if (!IsVisible) { panel.gameObject.SetActive(true); panel.SetAsLastSibling(); }
            if (changed)
            {
                displayedWin = win; displayedKnown = known;
                basicText.text = CurrencyText.Amount(win.BaseGold,"+");
                interestText.text = CurrencyText.Amount(win.InterestGold,"+");
                streakText.text = !known && win.StreakGold > 0
                    ? $"승리 시 {CurrencyText.Amount(win.StreakGold,"+")}" : CurrencyText.Amount(win.StreakGold,"+");
                augmentRow.SetActive(win.AugmentGold > 0);
                augmentText.text = known ? CurrencyText.Amount(win.AugmentGold,"+") : $"승리 시 {CurrencyText.Amount(win.AugmentGold,"+")}";
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            }
            TooltipPositionUtility.Place(panel, pointer, eventCamera, pointerOffset, edgePadding);
        }
        public void Hide() => panel.gameObject.SetActive(false);
    }
}
