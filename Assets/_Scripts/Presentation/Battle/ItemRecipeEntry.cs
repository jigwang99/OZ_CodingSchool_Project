using AutoBattler.Data.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    public sealed class ItemRecipeEntry : MonoBehaviour
    {
        [SerializeField] private Image materialIcon, resultIcon;
        [SerializeField] private TextMeshProUGUI materialName, resultName;

        public void Show(ItemDefinition material, Sprite materialSprite, ItemDefinition result, Sprite resultSprite, bool hasMaterial)
        {
            materialName.text = material.Name;
            resultName.text = result.Name;
            materialIcon.sprite = materialSprite;
            materialIcon.enabled = materialSprite != null;
            resultIcon.sprite = resultSprite;
            resultIcon.enabled = resultSprite != null;
            materialIcon.color = hasMaterial ? Color.white : new Color(.65f, .65f, .65f);
            materialName.color = hasMaterial ? new Color(1f, .89f, .58f) : new Color(.8f, .76f, .72f);
            gameObject.SetActive(true);
        }
    }
}
