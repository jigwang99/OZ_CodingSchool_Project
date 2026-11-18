using System.Collections.Generic;
using System.Linq;
using AutoBattler.Data.Items;
using AutoBattler.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>선택한 재료와 조합 가능한 상대 재료 및 결과 표시.</summary>
    public sealed class ItemRecipePanel : MonoBehaviour
    {
        [SerializeField] private Image selectedIcon;
        [SerializeField] private TextMeshProUGUI selectedName;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private ItemRecipeEntry entryPrefab;
        private readonly List<ItemRecipeEntry> entries = new List<ItemRecipeEntry>();
        private int shownItemId;

        public void Show(RunController run, OwnedItem selected, IReadOnlyList<OwnedItem> stored)
        {
            ItemDefinition ingredient = run.State.Data.Items[selected.DefinitionId];
            int count = 0;
            foreach (ItemRecipeDefinition recipe in run.State.Data.ItemRecipes)
            {
                int partnerId;
                if (recipe.FirstItemId == ingredient.Id) partnerId = recipe.SecondItemId;
                else if (recipe.SecondItemId == ingredient.Id) partnerId = recipe.FirstItemId;
                else continue;

                if (count == entries.Count) entries.Add(Instantiate(entryPrefab, scroll.content));
                ItemDefinition partner = run.State.Data.Items[partnerId];
                ItemDefinition result = run.State.Data.Items[recipe.ResultItemId];
                bool hasPartner = stored.Any(item => item.DefinitionId == partnerId && item.InstanceId != selected.InstanceId);
                entries[count++].Show(partner, run.ItemIcon(partner.IconId), result, run.ItemIcon(result.IconId), hasPartner);
            }
            for (int i = count; i < entries.Count; i++) entries[i].gameObject.SetActive(false);
            if (count == 0) { Hide(); return; }

            selectedName.text = ingredient.Name;
            selectedIcon.sprite = run.ItemIcon(ingredient.IconId);
            selectedIcon.enabled = selectedIcon.sprite != null;
            gameObject.SetActive(true);
            if (shownItemId == selected.InstanceId) return;
            shownItemId = selected.InstanceId;
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.verticalNormalizedPosition = 1f;
        }

        public void Hide()
        {
            shownItemId = 0;
            gameObject.SetActive(false);
        }
    }
}
