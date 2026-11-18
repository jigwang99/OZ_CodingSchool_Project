using System.Globalization;
using AutoBattler.Battle.Combat;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>기물 한 기의 별·수치·최대값 대비 가로 막대 표시.</summary>
    public sealed class BattleStatisticsRow : MonoBehaviour
    {
        [SerializeField] private Image portrait, fill;
        [SerializeField] private TextMeshProUGUI nameText, starText, valueText;
        [SerializeField] private Sprite fallbackPortrait;
        private int instanceId;
        private BattleStatisticKind displayedKind;
        private int rank=-1;
        public Image Fill => fill;
        public Image Portrait => portrait;
        public TextMeshProUGUI ValueText => valueText;
        public TextMeshProUGUI NameText => nameText;
        public int InstanceId => instanceId;
        public RectTransform Rect => (RectTransform)transform;

        public void SetRank(int index, bool animate)
        {
            if (rank == index) return;
            Rect.DOKill();
            float y = -index * BattleStatisticsPanel.RowSpacing;
            if (animate && rank >= 0) Rect.DOAnchorPosY(y, .24f).SetEase(Ease.OutCubic).SetUpdate(true);
            else Rect.anchoredPosition = new Vector2(Rect.anchoredPosition.x, y);
            rank = index;
        }

        public void Show(UnitBattleRecord record, BattleStatisticKind kind, long maximum, Sprite icon, Color color)
        {
            bool animate = instanceId != record.InstanceId || displayedKind != kind || !gameObject.activeSelf;
            instanceId = record.InstanceId; displayedKind = kind;
            gameObject.SetActive(true);
            portrait.sprite = icon != null ? icon : fallbackPortrait;
            nameText.text = record.UnitName;
            starText.text = new string('★', Mathf.Clamp(record.Star, 1, 3));
            long amount = record.Value(kind);
            valueText.text = amount.ToString("N0", CultureInfo.InvariantCulture);
            float ratio = maximum > 0 ? Mathf.Clamp01((float)((double)amount / maximum)) : 0f;
            fill.rectTransform.DOKill(); fill.color = color;
            if (animate) { fill.rectTransform.localScale = new Vector3(0f, 1f, 1f); fill.rectTransform.DOScaleX(ratio, .18f).SetUpdate(true); }
            else fill.rectTransform.localScale = new Vector3(ratio, 1f, 1f);
        }
        public void Hide()
        {
            rank=-1; instanceId=0;
            Rect.DOKill(); fill.rectTransform.DOKill(); gameObject.SetActive(false);
        }
        private void OnDisable()
        {
            Rect.DOKill();
            if(rank>=0) Rect.anchoredPosition=new Vector2(Rect.anchoredPosition.x,-rank*BattleStatisticsPanel.RowSpacing);
            if(fill!=null) fill.rectTransform.DOKill();
        }
    }
}
