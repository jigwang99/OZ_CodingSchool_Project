using AutoBattler.Battle.Units;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Units
{
    /// <summary>체력·마나 바 아래의 장착 아이템 3칸 표시.</summary>
    public sealed class UnitEquipmentView : MonoBehaviour
    {
        [SerializeField] private Image[] icons=new Image[3];
        private UnitEquipment equipment;
        public Image[] Icons=>icons;
        private void OnEnable()
        {
            Unit unit=GetComponentInParent<Unit>(); equipment=unit!=null?unit.Equipment:null;
            if(equipment!=null) equipment.Changed+=Refresh;
            Refresh();
        }
        private void Refresh()
        {
            for(int i=0;i<icons.Length;i++)
            { icons[i].sprite=equipment!=null?equipment.Icon(i):null; icons[i].enabled=icons[i].sprite!=null; }
        }
        private void OnDisable()
        {
            if(equipment!=null) equipment.Changed-=Refresh;
            equipment=null; foreach(Image icon in icons) { icon.sprite=null; icon.enabled=false; }
        }
    }
}
