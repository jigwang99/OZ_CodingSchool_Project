using AutoBattler.Battle.Units;
using TMPro;
using UnityEngine;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>프리팹에 배치한 이름표의 유닛 이름·팀 색상 표시.</summary>
    public sealed class UnitIdentityView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private UnitHealth health;
        public TextMeshProUGUI Label=>label;
        public UnitHealth Health=>health;
        public void Apply(Unit unit)
        {
            label.text=BattleText.UnitName(unit.Definition);
            label.color=unit.Team==UnitTeam.Ally?new Color(.6f,1f,1f):new Color(1f,.65f,.65f);
        }
        private void LateUpdate()=>label.enabled=health.IsAlive;
    }
}
