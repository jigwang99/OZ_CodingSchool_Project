using AutoBattler.Battle.Placement;
using UnityEngine;

namespace AutoBattler.Battle.Grid
{
    /// <summary>대기석 8칸의 월드 위치, 드롭 영역과 개발용 표시를 관리.</summary>
    public sealed class BenchLayout : MonoBehaviour
    {
        [SerializeField] private Vector2 originOffset = new Vector2(0f, -2f);
        [SerializeField, Min(0.1f)] private float slotSpacing = 1.5f;
        [SerializeField] private Vector2 slotSize = new Vector2(1.2f, 1.2f);
        private int? highlightedSlot;

        public Vector3 SlotToWorld(int slot) => transform.position
            + new Vector3(originOffset.x + slot * slotSpacing, originOffset.y, 0f);

        public bool TryWorldToSlot(Vector3 world, out int slot)
        {
            float x = world.x - transform.position.x - originOffset.x;
            slot = Mathf.RoundToInt(x / slotSpacing);
            if (slot < 0 || slot >= PlacementService.BenchCapacity) return false;
            Vector3 center = SlotToWorld(slot);
            return Mathf.Abs(world.x - center.x) <= slotSize.x * 0.5f
                && Mathf.Abs(world.y - center.y) <= slotSize.y * 0.5f;
        }

        public void SetHighlightedSlot(int? slot) => highlightedSlot = slot;

        private void OnDrawGizmos()
        {
            for (int slot = 0; slot < PlacementService.BenchCapacity; slot++)
            {
                Gizmos.color = highlightedSlot == slot ? Color.yellow : Color.white;
                Gizmos.DrawWireCube(SlotToWorld(slot), new Vector3(slotSize.x, slotSize.y, 0f));
            }
        }
    }
}
