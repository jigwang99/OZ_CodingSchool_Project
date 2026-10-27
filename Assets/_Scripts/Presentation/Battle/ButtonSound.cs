using AutoBattler.Audio;
using AutoBattler.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>활성 버튼의 마우스·키보드 확정 효과음.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class ButtonSound : MonoBehaviour
    {
        [SerializeField] private Button button;
        // 프리팹의 영속 onClick 리스너로 등록하여 화면 전환 명령보다 먼저 재생.
        public void PlayClick()
        { if (button != null && button.IsActive() && button.IsInteractable()) SoundManager.PlayEffect(SoundId.Click); }
#if UNITY_EDITOR
        public void Configure(Button target) => button = target;
#endif
    }
}
