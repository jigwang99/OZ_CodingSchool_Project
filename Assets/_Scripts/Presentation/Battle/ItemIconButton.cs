using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>보관함·장착칸에 표시할 아이템 버튼.</summary>
    public sealed class ItemIconButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Image background;
        private int itemId;
        public event Action<int> Pressed;
        public Button Button=>button;
        public Image Icon=>icon;
        public int ItemId=>itemId;
        private void Awake()=>button.onClick.AddListener(Request);
        private void Request() { if(itemId!=0) Pressed?.Invoke(itemId); }
        public void Show(int id,Sprite sprite,string name,bool selected,bool interactable)
        {
            itemId=id; icon.sprite=sprite; icon.enabled=sprite!=null;
            label.text=name; button.interactable=interactable && id!=0;
            background.color=selected?new Color(.15f,.42f,.45f):new Color(.1f,.16f,.23f);
        }
        private void OnDestroy()=>button.onClick.RemoveListener(Request);
    }
}
