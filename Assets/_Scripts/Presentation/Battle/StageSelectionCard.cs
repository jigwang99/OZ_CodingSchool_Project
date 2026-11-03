using System;
using AutoBattler.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>Inspector로 연결한 스테이지 카드의 번호·이름·미리보기 표시.</summary>
    public sealed class StageSelectionCard : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image frame;
        [SerializeField] private Image preview;
        [SerializeField] private TextMeshProUGUI numberText;
        [SerializeField] private TextMeshProUGUI locationText;
        public Button Button=>button;
        public StageDefinition Stage { get; private set; }
        public Sprite Preview=>preview.sprite;
        public bool IsSelected { get; private set; }
        public event Action<StageDefinition> Selected;
        private void OnEnable() { if(Application.isPlaying) button.onClick.AddListener(Choose); }
        private void OnDisable() { if(button!=null) button.onClick.RemoveListener(Choose); }
        private void Choose() { if(Stage!=null && button.IsInteractable()) Selected?.Invoke(Stage); }
        public void Bind(StageDefinition stage)
        {
            Stage=stage; numberText.text=stage!=null?stage.DisplayName:"";
            locationText.text=stage!=null?stage.LocationName:"";
            SetPreview(null); SetSelected(false); button.interactable=stage!=null;
            gameObject.SetActive(stage!=null);
        }
        public void SetPreview(Sprite sprite) { preview.sprite=sprite; preview.enabled=sprite!=null; }
        public void SetSelected(bool selected)
        {
            IsSelected=selected;
            frame.color=selected?new Color(.18f,.78f,.74f):new Color(.18f,.28f,.38f);
        }
    }
}
