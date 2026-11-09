using UnityEngine;

namespace AutoBattler.Utility
{
    /// <summary>좌상단 피벗의 툴팁을 포인터 옆에 배치하고 부모 화면 안으로 제한.</summary>
    public static class TooltipPositionUtility
    {
        public static void Place(RectTransform panel, Vector2 pointer, Camera eventCamera, Vector2 offset, float padding)
        {
            RectTransform viewport = (RectTransform)panel.parent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, pointer, eventCamera, out Vector2 local)) return;
            Rect bounds = viewport.rect;
            Vector2 size = panel.rect.size;
            float x = local.x + offset.x;
            float y = local.y + offset.y;
            if (x + size.x > bounds.xMax - padding) x = local.x - size.x - offset.x;
            if (y - size.y < bounds.yMin + padding) y = local.y + size.y - offset.y;
            x = Mathf.Clamp(x, bounds.xMin + padding, Mathf.Max(bounds.xMin + padding, bounds.xMax - padding - size.x));
            y = Mathf.Clamp(y, Mathf.Min(bounds.yMax - padding, bounds.yMin + padding + size.y), bounds.yMax - padding);
            panel.localPosition = new Vector3(x, y, 0f);
        }
    }
}
