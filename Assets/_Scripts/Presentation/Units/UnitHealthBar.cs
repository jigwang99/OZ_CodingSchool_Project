using AutoBattler.Battle.Units;
using AutoBattler.Battle.Placement;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Units
{
    /// <summary>부모 유닛의 체력·마나·보호막과 장비 표시. 장비가 있으면 표시 묶음을 위로 이동.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitHealthBar : MonoBehaviour
    {
        [SerializeField] private Canvas healthCanvas;
        [SerializeField] private Image fillImage;
        [FormerlySerializedAs("healthText")]
        [SerializeField] private TextMeshProUGUI starText;
        [SerializeField] private HealthBarFill segments;
        [SerializeField] private RectTransform healthRoot;
        [SerializeField] private RectTransform manaRoot;
        [SerializeField] private RectTransform equipmentRoot;
        [SerializeField, Min(0f)] private float equipmentLift = 52f;
        public RectTransform EquipmentRoot=>equipmentRoot;
        [SerializeField] private Color allyColor = new Color(0.2f, 0.9f, 0.35f);
        [SerializeField] private Color enemyColor = new Color(1f, 0.35f, 0.35f);

        private Unit owner;
        private UnitHealth boundHealth;
        private UnitEffects boundEffects;
        private Vector2 healthPosition;
        private Vector2 manaPosition;
        private Vector2 equipmentPosition;
        private bool layoutCaptured;
        private bool equipmentLayout;

        public Canvas HealthCanvas => healthCanvas;
        public Image FillImage => fillImage;
        public TextMeshProUGUI StarText => starText;
        public HealthBarFill Segments=>segments;
        public RectTransform HealthRoot => healthRoot;
        public RectTransform ManaRoot => manaRoot;

        public void Bind(Unit unit)
        {
            CaptureLayout();
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
            starText.text=string.Empty;
            starText.gameObject.SetActive(false);
            segments.SetValues(initialized ? boundHealth.CurrentHp : 0,initialized ? boundHealth.MaxHp : 1,shield,animate);
        }

        private void RefreshColor()
        {
            fillImage.color = owner != null && owner.Team == UnitTeam.Enemy ? enemyColor : allyColor;
        }

        private void RefreshVisibility()
        {
            bool alive = owner != null && boundHealth != null && boundHealth.IsInitialized && boundHealth.IsAlive;
            bool onBoard = alive && owner.IsOnBoard;
            bool onBench = alive && owner.View != null && owner.View.State != null
                && owner.View.State.Location.Kind == UnitLocationKind.Bench;
            bool equipped = alive && owner.Equipment != null && owner.Equipment.Count > 0;
            healthCanvas.enabled = onBoard || onBench && equipped;
            SetEquipmentLayout(equipped);
            // 대기석에서는 장비만 표시하고 전장에 배치되면 HP·MP 표시 복원.
            SetVisible(healthRoot, onBoard);
            SetVisible(manaRoot, onBoard && !owner.IsSummon);
            SetVisible(equipmentRoot,(onBoard || onBench) && equipped);
        }

        private void CaptureLayout()
        {
            if (layoutCaptured) return;
            healthPosition = healthRoot.anchoredPosition;
            manaPosition = manaRoot.anchoredPosition;
            equipmentPosition = equipmentRoot.anchoredPosition;
            layoutCaptured = true;
        }

        private void SetEquipmentLayout(bool equipped)
        {
            if (!layoutCaptured || equipmentLayout == equipped) return;
            equipmentLayout = equipped;
            Vector2 offset = equipped ? Vector2.up * equipmentLift : Vector2.zero;
            healthRoot.anchoredPosition = healthPosition + offset;
            manaRoot.anchoredPosition = manaPosition + offset;
            equipmentRoot.anchoredPosition = equipmentPosition + offset;
        }

        private static void SetVisible(RectTransform root, bool visible)
        {
            if (root != null && root.gameObject.activeSelf != visible)
                root.gameObject.SetActive(visible);
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
            SetEquipmentLayout(false);
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
