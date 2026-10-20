using System;
using AutoBattler.Data.Augments;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>강화 한 개 표시와 ID 기반 선택 요청.</summary>
    public sealed class AugmentCard : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI titleText,descriptionText,indexText;
        public int DisplayedId { get; private set; }
        public Button Button=>button;
        public TextMeshProUGUI TitleText=>titleText;
        public TextMeshProUGUI DescriptionText=>descriptionText;
        public event Action<int> Selected;
#if UNITY_EDITOR
        public void Configure(Button select,TextMeshProUGUI title,TextMeshProUGUI description,TextMeshProUGUI index)
        { button=select; titleText=title; descriptionText=description; indexText=index; }
#endif
        public void Show(AugmentDefinition definition,int index)
        { DisplayedId=definition.Id; titleText.text=definition.Name; descriptionText.text=definition.Description; indexText.text=(index+1).ToString("00"); }
        private void OnEnable() { if(Application.isPlaying && button!=null) button.onClick.AddListener(Request); }
        private void OnDisable() { if(button!=null) button.onClick.RemoveListener(Request); }
        private void Request()=>Selected?.Invoke(DisplayedId);
    }
}
