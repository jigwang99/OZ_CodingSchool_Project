using System;
using AutoBattler.Run;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>길 위 스테이지 지점의 번호·장소와 선택 상태 표시.</summary>
    public sealed class StageSelectionCard : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Graphic frame;
        [SerializeField] private TextMeshProUGUI numberText;
        [SerializeField] private TextMeshProUGUI locationText;
        public Button Button=>button;
        public StageDefinition Stage { get; private set; }
        public RectTransform RectTransform=>(RectTransform)transform;
        public bool IsSelected { get; private set; }
        public event Action<StageDefinition> Selected;
        private void OnEnable() { if(Application.isPlaying) button.onClick.AddListener(Choose); }
        private void OnDisable() { if(button!=null) button.onClick.RemoveListener(Choose); transform.DOKill(); }
        private void Choose() { if(Stage!=null && button.IsInteractable()) Selected?.Invoke(Stage); }
        public void Bind(StageDefinition stage)
        {
            Stage=stage; numberText.text=stage!=null?stage.DisplayName:"";
            locationText.text=stage!=null?stage.LocationName:"";
            SetSelected(false,false); button.interactable=stage!=null;
            gameObject.SetActive(stage!=null);
        }
        public void SetSelected(bool selected,bool animate=true)
        {
            IsSelected=selected;
            frame.color=selected?new Color(.96f,.68f,.18f):new Color(.08f,.48f,.54f);
            numberText.color=selected?new Color(.10f,.16f,.19f):Color.white;
            transform.DOKill();
            Vector3 scale=Vector3.one*(selected?1.12f:1f);
            if(animate && Application.isPlaying) transform.DOScale(scale,.14f).SetEase(Ease.OutCubic).SetUpdate(true);
            else transform.localScale=scale;
        }
    }
}
