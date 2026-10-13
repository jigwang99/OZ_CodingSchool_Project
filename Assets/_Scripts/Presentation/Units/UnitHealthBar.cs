using AutoBattler.Battle.Units;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Units
{
    /// <summary>부모 유닛의 체력과 생존 상태를 표시하는 공통 월드 UI.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitHealthBar : MonoBehaviour
    {
        [SerializeField] private Canvas healthCanvas;
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private Color allyColor = new Color(0.2f, 0.9f, 0.35f);
        [SerializeField] private Color enemyColor = new Color(1f, 0.35f, 0.35f);
        [SerializeField, Min(0f)] private float tweenDuration = 0.15f;

        private Unit owner;
        private UnitHealth boundHealth;

        public Canvas HealthCanvas => healthCanvas;
        public Image FillImage => fillImage;
        public TextMeshProUGUI HealthText => healthText;
#if UNITY_EDITOR
        public void ConfigureReferences(Canvas canvas, Image fill, TextMeshProUGUI label)
        { healthCanvas=canvas; fillImage=fill; healthText=label; }
#endif

        public void Bind(Unit unit)
        {
            Unbind();
            owner = unit;
            boundHealth = owner != null ? owner.GetComponent<UnitHealth>() : null;
            if (boundHealth != null)
                boundHealth.HealthChanged += OnHealthChanged;
            RefreshImmediate();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                Bind(GetComponentInParent<Unit>());
        }

        private void RefreshImmediate()
        {
            fillImage.rectTransform.DOKill();
            bool initialized = boundHealth != null && boundHealth.IsInitialized;
            float ratio = initialized ? (float)boundHealth.CurrentHp / boundHealth.MaxHp : 0f;
            fillImage.rectTransform.localScale = new Vector3(Mathf.Clamp01(ratio), 1f, 1f);
            healthText.text = initialized ? $"{boundHealth.CurrentHp}/{boundHealth.MaxHp}" : "--";
            RefreshColor();
            RefreshVisibility();
        }

        private void OnHealthChanged(int currentHp, int maxHp)
        {
            fillImage.rectTransform.DOKill();
            healthText.text = $"{currentHp}/{maxHp}";
            RefreshColor();
            RefreshVisibility();
            float ratio = Mathf.Clamp01((float)currentHp / maxHp);
            if (currentHp == 0 || currentHp == maxHp)
                fillImage.rectTransform.localScale = new Vector3(ratio, 1f, 1f);
            else
                fillImage.rectTransform.DOScaleX(ratio, tweenDuration).SetUpdate(true);
        }

        private void RefreshColor()
        {
            fillImage.color = owner != null && owner.Team == UnitTeam.Enemy ? enemyColor : allyColor;
        }

        private void RefreshVisibility()
        {
            healthCanvas.enabled = owner != null && boundHealth != null && boundHealth.IsInitialized
                && owner.IsOnBoard && boundHealth.IsAlive;
        }

        private void LateUpdate() => RefreshVisibility();

        private void Unbind()
        {
            if (boundHealth != null)
                boundHealth.HealthChanged -= OnHealthChanged;
            boundHealth = null;
            owner = null;
            if (fillImage != null)
                fillImage.rectTransform.DOKill();
        }

        private void OnDisable()
        {
            Unbind();
            if (healthCanvas != null)
                healthCanvas.enabled = false;
        }
    }
}
