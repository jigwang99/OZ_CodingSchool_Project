using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    public sealed class UnitCodexEntry : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI label;
        private Action select;

        private void Awake()=>button.onClick.AddListener(Select);
        private void OnDestroy()=>button.onClick.RemoveListener(Select);
        private void Select()=>select?.Invoke();

        public void Bind(string title,Sprite sprite,Action callback)
        {
            select=callback;
            label.text=title;
            SetIcon(sprite);
        }
        public void SetIcon(Sprite sprite) { icon.sprite=sprite; icon.enabled=sprite!=null; }
        public void SetSelected(bool selected)=>button.targetGraphic.color=selected?new Color(.55f,.85f,.81f):Color.white;
    }
}
