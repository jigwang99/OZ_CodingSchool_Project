using AutoBattler.Battle.Units;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Units
{
    /// <summary>부모 유닛의 성급·체력·보호막과 생존 상태를 표시하는 공통 월드 UI.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitHealthBar : MonoBehaviour
    {
        [SerializeField] private Canvas healthCanvas;
        [SerializeField] private Image fillImage;
        [FormerlySerializedAs("healthText")]
        [SerializeField] private TextMeshProUGUI starText;
        [SerializeField] private HealthBarFill segments;
        [SerializeField] private Color allyColor = new Color(0.2f, 0.9f, 0.35f);
        [SerializeField] private Color enemyColor = new Color(1f, 0.35f, 0.35f);

        private Unit owner;
        private UnitHealth boundHealth;
        private UnitEffects boundEffects;

        public Canvas HealthCanvas => healthCanvas;
        public Image FillImage => fillImage;
        public TextMeshProUGUI StarText => starText;
        public HealthBarFill Segments=>segments;
#if UNITY_EDITOR
        public void ConfigureReferences(Canvas canvas, Image fill, TextMeshProUGUI label)
        { healthCanvas=canvas; fillImage=fill; starText=label; }
        public void ConfigureSegments(HealthBarFill view)=>segments=view;
#endif

        public void Bind(Unit unit)
        {
            Unbind();
            owner = unit;
            boundHealth = owner != null ? owner.GetComponent<UnitHealth>() : null;
            if (boundHealth != null)
                boundHealth.HealthChanged += OnHealthChanged;
            boundEffects=owner!=null ? owner.Effects : null;
            if(boundEffects!=null) boundEffects.Changed+=OnEffectsChanged;
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
            RefreshValues(false);
            RefreshColor();
            RefreshVisibility();
        }

        private void OnHealthChanged(int currentHp, int maxHp)
        {
            RefreshColor();
            RefreshVisibility();
            RefreshValues(currentHp!=0 && currentHp!=maxHp);
        }
        private void OnEffectsChanged()=>RefreshValues(boundHealth!=null && boundHealth.IsAlive);
        private void RefreshValues(bool animate)
        {
            bool initialized=boundHealth!=null && boundHealth.IsInitialized;
            int shield=boundEffects?.Shield ?? 0;
            starText.text=initialized && owner!=null ? new string('★',owner.Star) : string.Empty;
            segments.SetValues(initialized ? boundHealth.CurrentHp : 0,initialized ? boundHealth.MaxHp : 1,shield,animate);
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
            if(boundEffects!=null) boundEffects.Changed-=OnEffectsChanged;
            boundEffects=null;
            owner = null;
            if(segments!=null) segments.Clear();
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
