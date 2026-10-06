using UnityEngine;

namespace AutoBattler.Battle.Grid
{
    public class HexGridLayout : MonoBehaviour
    {
        private const int Columns = 8;
        private const int Rows = 7;
        private const int AllyColumns = 4;

        [Header("육각 타일 크기")]
        [Tooltip("타일 중심에서 꼭짓점까지의 거리")]
        [SerializeField, Min(0.01f)] private float cellSize = 1f;

        [Header("배치 영역 표시")]
        [SerializeField] private Color allyColor = Color.cyan;

        [SerializeField] private Color enemyColor = Color.red;

        private HexCoord? highlightTile;

        /// <summary>
        /// Axial 좌표를 월드 위치로 변환.
        /// 이 오브젝트의 위치를 보드 원점으로 사용.
        /// </summary>
        public Vector3 CellToWorld(HexCoord cell)
        {
            float x = cellSize * 1.5f * cell.Q;
            float y = cellSize * Mathf.Sqrt(3f)
                * (cell.R + cell.Q * 0.5f);

            return transform.position + new Vector3(x, y, 0f);
        }
        public HexCoord WorldToCell(Vector3 worldPosition)
        {
            Vector3 localPosition = worldPosition - transform.position;

            float q = (2f / 3f * localPosition.x) / cellSize;
            float r = (-1f / 3f * localPosition.x + Mathf.Sqrt(3f) / 3f * localPosition.y) / cellSize;
            float s = -q - r;

            int roundedQ = Mathf.RoundToInt(q);
            int roundedR = Mathf.RoundToInt(r);
            int roundedS = Mathf.RoundToInt(s);

            float qDiff = Mathf.Abs(roundedQ - q);
            float rDiff = Mathf.Abs(roundedR - r);
            float sDiff = Mathf.Abs(roundedS - s);

            // 육각 좌표의 합이 0이 되도록 조정.
            if (qDiff > rDiff && qDiff > sDiff)
            {
                roundedQ = -roundedR - roundedS;
            }
            else if (rDiff > sDiff)
            {
                roundedR = -roundedQ - roundedS;
            }
            else
            {
                roundedS = -roundedQ - roundedR;
            }

            return new HexCoord(roundedQ, roundedR);
        }
        public bool IsInsideBoard(HexCoord cell)
        {
            cell.ToOffset(out int col, out int row);
            return col >= 0 && col < Columns && row >= 0 && row < Rows;
        }
        public void SetHighlightTile(HexCoord? cell)
        {
            highlightTile = cell;
        }
        private void DrawHexOutLine(Vector3 center)
        {
            for(int i = 0; i < 6; i++)
            {
                float currentAngle = Mathf.Deg2Rad * (60f * i);
                float nextAngle = Mathf.Deg2Rad * (60f * ((i + 1) % 6));

                Vector3 currentPoint = center + new Vector3(
                    cellSize * Mathf.Cos(currentAngle),
                    cellSize * Mathf.Sin(currentAngle),
                    0f);

                Vector3 nextPoint = center + new Vector3(
                    cellSize * Mathf.Cos(nextAngle),
                    cellSize * Mathf.Sin(nextAngle),
                    0f);

                Gizmos.DrawLine(currentPoint, nextPoint);
            }
        }
        /// <summary>
        /// 실제 타일을 생성하기 전에 중심점과 배치 영역을 확인.
        /// </summary>
        private void OnDrawGizmos()
        {
            for (int col = 0; col < Columns; col++)
            {
                Gizmos.color = col < AllyColumns
                    ? allyColor
                    : enemyColor;

                for (int row = 0; row < Rows; row++)
                {
                    HexCoord cell = HexCoord.FromOffset(col, row);
                    Vector3 center = CellToWorld(cell);

                    DrawHexOutLine(center);
                }
            }
            if (highlightTile.HasValue)
            {
                Gizmos.color = Color.yellow;
                DrawHexOutLine(CellToWorld(highlightTile.Value));
            }
        }
    }
}