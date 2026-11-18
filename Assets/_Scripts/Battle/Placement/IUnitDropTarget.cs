using AutoBattler.Presentation.Units;
using UnityEngine;

namespace AutoBattler.Battle.Placement
{
    /// <summary>화면 영역으로 드래그한 기물의 별도 처리 계약.</summary>
    public interface IUnitDropTarget
    {
        bool Contains(Vector2 screenPosition);
        bool TryDrop(UnitView unit);
    }
}
