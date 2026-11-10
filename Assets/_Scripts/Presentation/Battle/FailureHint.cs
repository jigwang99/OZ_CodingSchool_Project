using System;
using AutoBattler.Audio;
using AutoBattler.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>비활성 버튼 클릭 시 실패 사유를 토스트로 전달.</summary>
    public sealed class FailureHint : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Button button;
        private Func<string> reason;
        private Func<bool> allowed;
        private Action<string> notify;

        public void Bind(Func<string> failure, Func<bool> canReport, Action<string> publish)
        { reason = failure; allowed = canReport; notify = publish; }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging
                || button.interactable || allowed?.Invoke() != true) return;
            string message = reason?.Invoke();
            if (string.IsNullOrEmpty(message)) return;
            notify?.Invoke(message);
            SoundManager.PlayEffect(SoundId.Denied);
        }
    }
}
