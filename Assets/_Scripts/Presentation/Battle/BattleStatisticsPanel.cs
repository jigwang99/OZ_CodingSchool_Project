using System;
using System.Linq;
using System.Collections.Generic;
using AutoBattler.Battle.Combat;
using AutoBattler.Run;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>증강 반대편의 비모달 전투 통계 패널. 전투·준비 시간은 계속 진행.</summary>
    public sealed class BattleStatisticsPanel : MonoBehaviour
    {
        [SerializeField] private Button toggleButton, closeButton;
        [SerializeField] private Button damageButton, takenButton, healingButton;
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private BattleStatisticsRow[] rows;
        [SerializeField] private Color damageColor = new Color(1f, .64f, .25f);
        [SerializeField] private Color takenColor = new Color(.95f, .37f, .4f);
        [SerializeField] private Color healingColor = new Color(.35f, .85f, .55f);
        [SerializeField] private Color selectedTab = new Color(.18f, .38f, .4f);
        [SerializeField] private Color normalTab = Color.white;
        private RunController run;
        private CanvasGroup inputRoot;
        private bool dirty, resetScroll;
        private BattleReport displayedReport;
        public bool IsOpen { get; private set; }
        public BattleStatisticKind SelectedKind { get; private set; }
        public Button ToggleButton => toggleButton;
        public Button CloseButton => closeButton;
        public Button DamageButton => damageButton;
        public Button TakenButton => takenButton;
        public Button HealingButton => healingButton;
        public CanvasGroup Panel => panel;
        public TextMeshProUGUI TitleText => titleText;
        public IReadOnlyList<BattleStatisticsRow> Rows => Array.AsReadOnly(rows);

        public void Bind(RunController controller, CanvasGroup group)
        {
            Close();
            if (run != null)
            {
                run.DisplayChanged -= MarkDirty;
                if (run.Battle != null) run.Battle.Statistics.Changed -= MarkDirty;
            }
            run = controller; inputRoot = group;
            if (run != null)
            { run.DisplayChanged += MarkDirty; run.Battle.Statistics.Changed += MarkDirty; }
            SelectedKind = BattleStatisticKind.DamageDealt; MarkDirty();
        }
        private bool CanUse => run != null && run.IsReady && !run.IsShuttingDown
            && !GameNavigation.IsTransitioning && !run.Flow.IsAugmentWindowOpen
            && inputRoot != null && inputRoot.interactable && inputRoot.blocksRaycasts;
        private void OnEnable()
        {
            toggleButton.onClick.AddListener(Toggle); closeButton.onClick.AddListener(Close);
            damageButton.onClick.AddListener(SelectDamage); takenButton.onClick.AddListener(SelectTaken); healingButton.onClick.AddListener(SelectHealing);
        }
        private void Toggle() { if (IsOpen) Close(); else Open(); }
        public void Open()
        {
            if (IsOpen || !CanUse) return;
            run.Drag.CancelDrag();
            IsOpen = true; resetScroll = true; panel.gameObject.SetActive(true); transform.SetAsLastSibling();
            panel.DOKill(); panel.alpha = 0f; panel.DOFade(1f, .15f).SetUpdate(true); Refresh();
        }
        public void Close()
        {
            IsOpen = false;
            if (panel != null) { panel.DOKill(); panel.gameObject.SetActive(false); }
        }
        private void SelectDamage() => Select(BattleStatisticKind.DamageDealt);
        private void SelectTaken() => Select(BattleStatisticKind.DamageTaken);
        private void SelectHealing() => Select(BattleStatisticKind.HealingDone);
        public void Select(BattleStatisticKind kind)
        { SelectedKind = kind; resetScroll = true; MarkDirty(); if (IsOpen) Refresh(); }
        private void MarkDirty() => dirty = true;
        private void LateUpdate()
        {
            toggleButton.interactable = CanUse;
            if (IsOpen && !CanUse) Close();
            if (!IsOpen) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (dirty) Refresh();
        }
        private void Refresh()
        {
            dirty = false;
            BattleReport report = run.Battle.Statistics.Current ?? run.State.LastBattleReport;
            if (!ReferenceEquals(displayedReport, report)) { displayedReport = report; resetScroll = true; }
            UnitBattleRecord[] ordered = report == null ? Array.Empty<UnitBattleRecord>() : report.Units.Where(record => record != null)
                .OrderByDescending(record => record.Value(SelectedKind)).ThenBy(record => record.InstanceId).Take(rows.Length).ToArray();
            int length = run.State.Data.RoundsPerSegment;
            titleText.text = report == null ? "전투 기록" : $"{(report.Round - 1) / length + 1}-{(report.Round - 1) % length + 1}  {(report.IsComplete ? report.Won ? "승리" : "패배" : "전투 중")}";
            long maximum = ordered.Length > 0 ? ordered[0].Value(SelectedKind) : 0;
            Color color = SelectedKind == BattleStatisticKind.DamageTaken ? takenColor : SelectedKind == BattleStatisticKind.HealingDone ? healingColor : damageColor;
            for (int index = 0; index < rows.Length; index++)
            {
                if (index >= ordered.Length) { rows[index].Hide(); continue; }
                UnitBattleRecord record = ordered[index];
                Sprite icon = run.Assets.UnitPortrait(record.VisualId) ?? run.UnitIcon(record.IconId);
                rows[index].Show(record, SelectedKind, maximum, icon, color);
            }
            damageButton.targetGraphic.color = SelectedKind == BattleStatisticKind.DamageDealt ? selectedTab : normalTab;
            takenButton.targetGraphic.color = SelectedKind == BattleStatisticKind.DamageTaken ? selectedTab : normalTab;
            healingButton.targetGraphic.color = SelectedKind == BattleStatisticKind.HealingDone ? selectedTab : normalTab;
            scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, ordered.Length * 60f - 4f));
            scroll.vertical = scroll.content.rect.height > scroll.viewport.rect.height;
            if (resetScroll) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1f; resetScroll = false; }
        }
        private void OnDisable()
        {
            Close();
            toggleButton.onClick.RemoveListener(Toggle); closeButton.onClick.RemoveListener(Close);
            damageButton.onClick.RemoveListener(SelectDamage); takenButton.onClick.RemoveListener(SelectTaken); healingButton.onClick.RemoveListener(SelectHealing);
        }
        private void OnDestroy() => Bind(null, null);
    }
}
