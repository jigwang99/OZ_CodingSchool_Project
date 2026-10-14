using AutoBattler.Battle.Units;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Units
{
    /// <summary>체력바 아래에 현재 마나 비율 표시. 표시 여부는 부모 체력바 Canvas에서 관리.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitManaBar : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField, Min(0f)] private float tweenDuration = 0.15f;

        private UnitMana boundMana;

        public Image FillImage => fillImage;
#if UNITY_EDITOR
        public void ConfigureReferences(Image fill) => fillImage = fill;
#endif

        public void Bind(Unit unit)
        {
            Unbind();
            boundMana = unit != null ? unit.Mana : null;
            if (boundMana != null)
                boundMana.Changed += OnManaChanged;

            float ratio = boundMana != null ? Ratio(boundMana.Current, boundMana.Maximum) : 0f;
            fillImage.rectTransform.localScale = new Vector3(ratio, 1f, 1f);
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                Bind(GetComponentInParent<Unit>());
        }

        private void OnManaChanged(int current, int maximum)
        {
            var fill = fillImage.rectTransform;
            fill.DOKill();
            float ratio = Ratio(current, maximum);
            // 마나 소진·충전 완료·라운드 초기화는 즉시 반영.
            if (current == 0 || current == maximum)
                fill.localScale = new Vector3(ratio, 1f, 1f);
            else
                fill.DOScaleX(ratio, tweenDuration).SetUpdate(true);
        }

        private static float Ratio(int current, int maximum) =>
            Mathf.Clamp01((float)current / Mathf.Max(1, maximum));

        private void Unbind()
        {
            if (boundMana != null)
                boundMana.Changed -= OnManaChanged;
            boundMana = null;
            if (fillImage != null)
                fillImage.rectTransform.DOKill();
        }

        private void OnDisable() => Unbind();
    }
}
