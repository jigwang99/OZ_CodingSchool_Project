using System;
using AutoBattler.Data.Snapshot;
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
        private UnitDefinition unit;
        private Action<UnitDefinition> select;

        private void Awake()=>button.onClick.AddListener(Select);
        private void OnDestroy()=>button.onClick.RemoveListener(Select);
        private void Select()=>select?.Invoke(unit);

        public void Bind(UnitDefinition definition,Sprite sprite,Action<UnitDefinition> callback)
        {
            unit=definition;select=callback;
            icon.sprite=sprite;icon.enabled=sprite!=null;
            label.text=$"{definition.Name}  <color=#FFD159>{definition.Cost}G</color>";
        }
        public void SetSelected(bool selected)=>button.targetGraphic.color=selected?new Color(.55f,.85f,.81f):Color.white;
    }
}
