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
        public Image Fill => fill;
        public Image Portrait => portrait;
        public TextMeshProUGUI ValueText => valueText;
        public TextMeshProUGUI NameText => nameText;
        public int InstanceId => instanceId;

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
        public void Hide() { fill.rectTransform.DOKill(); gameObject.SetActive(false); }
        private void OnDisable() { if (fill != null) fill.rectTransform.DOKill(); }
    }
}
