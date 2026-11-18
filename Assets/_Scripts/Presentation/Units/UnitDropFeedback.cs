using UnityEngine;

namespace AutoBattler.Presentation.Units
{
    /// <summary>아이템 드롭 대상 확대. 드래그 종료와 풀 반환 시 원래 크기 복원.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitDropFeedback : MonoBehaviour
    {
        private Vector3 originalScale;
        public bool IsHighlighted { get; private set; }
        public void SetHighlighted(bool highlighted, float multiplier = 1.18f)
        {
            if (IsHighlighted == highlighted) return;
            IsHighlighted = highlighted;
            if (highlighted)
            {
                originalScale = transform.localScale;
                transform.localScale = originalScale * multiplier;
            }
            else transform.localScale = originalScale;
        }
        private void OnDisable() => SetHighlighted(false);
    }
}
