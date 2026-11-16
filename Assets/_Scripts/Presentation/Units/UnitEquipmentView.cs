using AutoBattler.Battle.Units;
using AutoBattler.Run;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Units
{
    /// <summary>체력·마나 바 아래의 장착 아이템 3칸 표시.</summary>
    public sealed class UnitEquipmentView : MonoBehaviour
    {
        [SerializeField] private Image[] icons=new Image[3];
        [SerializeField] private TextMeshProUGUI[] stackLabels=new TextMeshProUGUI[ItemInventoryState.EquipmentCapacity];
        private readonly int[] displayedItems=new int[ItemInventoryState.EquipmentCapacity];
        private UnitEquipment equipment;
        public Image[] Icons=>icons;
        private void OnEnable()
        {
            Unit unit=GetComponentInParent<Unit>(); equipment=unit!=null?unit.Equipment:null;
            if(equipment!=null)
            {
                equipment.Changed+=Refresh;
                equipment.EffectStacksChanged+=RefreshStacks;
                equipment.EffectActivated+=Flash;
            }
            Refresh();
        }
        private void Refresh()
        {
            for(int i=0;i<icons.Length;i++)
            {
                int item=equipment!=null?equipment.ItemIdAt(i):0;
                if(displayedItems[i]!=item) EquipmentIconFeedback.Stop(icons[i]);
                displayedItems[i]=item;
                icons[i].sprite=equipment!=null?equipment.Icon(i):null;
                icons[i].enabled=icons[i].sprite!=null;
                RefreshStacks(i);
            }
        }
        private void RefreshStacks(int slot)
            =>EquipmentIconFeedback.SetStacks(stackLabels[slot],icons[slot].enabled && equipment!=null?equipment.EffectStacksAt(slot):0);
        private void Flash(int slot)=>EquipmentIconFeedback.Flash(icons[slot]);
        private void OnDisable()
        {
            if(equipment!=null)
            {
                equipment.Changed-=Refresh;
                equipment.EffectStacksChanged-=RefreshStacks;
                equipment.EffectActivated-=Flash;
            }
            equipment=null;
            Refresh();
        }
    }

    internal static class EquipmentIconFeedback
    {
        public static void SetStacks(TextMeshProUGUI label,int count)
        {
            if(label==null) return;
            if(count>0) label.SetText("<mark=#211916E8>{0:0}</mark>",count);
            if(label.gameObject.activeSelf!=(count>0)) label.gameObject.SetActive(count>0);
        }

        public static void Flash(Image icon)
        {
            if(!icon.isActiveAndEnabled || icon.sprite==null) return;
            Stop(icon);
            Color baseColor=icon.color;
            RectTransform rect=icon.rectTransform;
            Vector3 baseScale=rect.localScale;
            DOTween.Sequence().SetTarget(icon).SetUpdate(true)
                .Append(icon.DOColor(new Color(1f,.83f,.34f,baseColor.a),.07f))
                .Join(rect.DOScale(baseScale*1.16f,.07f))
                .AppendInterval(.08f)
                .Append(icon.DOColor(baseColor,.22f))
                .Join(rect.DOScale(baseScale,.22f))
                .OnKill(()=>
                {
                    if(icon==null) return;
                    icon.color=baseColor;
                    rect.localScale=baseScale;
                });
        }

        public static void Stop(Image icon)=>icon.DOKill();
    }
}
