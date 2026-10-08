using AutoBattler.Battle.Units;
using AutoBattler.Battle.Placement;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Units
{
    public sealed class UnitHealthBar : MonoBehaviour
    {
        [SerializeField] private UnitHealth unitHealth;
        [SerializeField] private UnitView unitView;
        [SerializeField] private Canvas healthCanvas;
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField, Min(0f)] private float tweenDuration = 0.15f;

        private UnitHealth boundHealth;

        private void Start() => TryBind();
        private void OnEnable() => TryBind();

        private void TryBind()
        {
            if (unitHealth == null || unitView == null || healthCanvas == null
                || fillImage == null || healthText == null || !unitHealth.IsInitialized || boundHealth != null)
                return;
            boundHealth = unitHealth;
            boundHealth.HealthChanged += OnHealthChanged;
            fillImage.rectTransform.localScale = new Vector3(
                (float)boundHealth.CurrentHp / boundHealth.MaxHp, 1f, 1f);
            healthText.text = $"{boundHealth.CurrentHp}/{boundHealth.MaxHp}";
        }

        private void LateUpdate()
        {
            if (healthCanvas != null)
                healthCanvas.enabled = boundHealth != null && unitView != null && unitView.State != null
                    && unitView.State.Location.Kind == UnitLocationKind.Board;
        }

        private void OnHealthChanged(int currentHp, int maxHp)
        {
            fillImage.rectTransform.DOKill();
            healthText.text = $"{currentHp}/{maxHp}";
            fillImage.rectTransform.DOScaleX((float)currentHp / maxHp, tweenDuration).SetUpdate(true);
        }

        private void OnDisable()
        {
            if (boundHealth != null) boundHealth.HealthChanged -= OnHealthChanged;
            boundHealth = null;
            if (fillImage != null) fillImage.rectTransform.DOKill();
        }
    }
}
