using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using UnityEngine;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>미리 배치한 전장·대기석 테두리의 위치와 강조 색상 갱신.</summary>
    [ExecuteAlways]
    public sealed class BoardOutlineView : MonoBehaviour
    {
        [Header("보드 참조")]
        [SerializeField] private HexGridLayout grid;
        [SerializeField] private BenchLayout bench;
        [Header("전장: 열 순서로 56개, 대기석: 슬롯 순서로 8개")]
        [SerializeField] private LineRenderer[] tiles;
        [SerializeField] private LineRenderer[] seats;
        [Header("영역 색상")]
        [SerializeField] private Color allyColor=new Color(.15f,.7f,.8f);
        [SerializeField] private Color enemyColor=new Color(.9f,.3f,.4f);
        [SerializeField] private Color seatColor=Color.gray;
        [SerializeField] private Color highlightColor=Color.yellow;
        private bool hasGeometry;
        private Vector3 lastGridPosition,lastFirstSeat,lastLastSeat;
        private float lastCellSize;
        private Vector2 lastSeatSize;
        public HexGridLayout Grid=>grid;
        public BenchLayout Bench=>bench;
        public System.Collections.Generic.IReadOnlyList<LineRenderer> Tiles=>System.Array.AsReadOnly(tiles??System.Array.Empty<LineRenderer>());
        public System.Collections.Generic.IReadOnlyList<LineRenderer> Seats=>System.Array.AsReadOnly(seats??System.Array.Empty<LineRenderer>());
        private void OnEnable()=>hasGeometry=false;
        private void LateUpdate()
        {
            if(grid==null || bench==null || tiles==null || seats==null
                || tiles.Length!=HexGridLayout.Columns*HexGridLayout.Rows || seats.Length!=PlacementService.BenchCapacity) return;
            Vector3 first=bench.SlotToWorld(0),last=bench.SlotToWorld(PlacementService.BenchCapacity-1);
            if(!hasGeometry || lastGridPosition!=grid.transform.position || lastCellSize!=grid.CellSize
                || lastFirstSeat!=first || lastLastSeat!=last || lastSeatSize!=bench.SlotSize)
            {
                UpdateGeometry(); lastGridPosition=grid.transform.position; lastCellSize=grid.CellSize;
                lastFirstSeat=first; lastLastSeat=last; lastSeatSize=bench.SlotSize; hasGeometry=true;
            }
            for(int col=0;col<HexGridLayout.Columns;col++) for(int row=0;row<HexGridLayout.Rows;row++)
            {
                Color color=grid.HighlightTile==HexCoord.FromOffset(col,row)?highlightColor:col<HexGridLayout.AllyColumns?allyColor:enemyColor;
                LineRenderer line=tiles[col*HexGridLayout.Rows+row]; line.startColor=line.endColor=color;
            }
            for(int i=0;i<seats.Length;i++) seats[i].startColor=seats[i].endColor=bench.HighlightedSlot==i?highlightColor:seatColor;
        }
        private void UpdateGeometry()
        {
            for(int col=0;col<HexGridLayout.Columns;col++) for(int row=0;row<HexGridLayout.Rows;row++)
            {
                LineRenderer line=tiles[col*HexGridLayout.Rows+row];
                Vector3 center=grid.CellToWorld(HexCoord.FromOffset(col,row));
                for(int i=0;i<6;i++)
                {
                    float angle=i*60f*Mathf.Deg2Rad;
                    line.SetPosition(i,line.transform.InverseTransformPoint(center+grid.CellSize*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0f)));
                }
            }
            Vector2 half=bench.SlotSize*.5f;
            for(int i=0;i<seats.Length;i++)
            {
                LineRenderer line=seats[i]; Vector3 center=bench.SlotToWorld(i);
                line.SetPosition(0,line.transform.InverseTransformPoint(center+new Vector3(-half.x,-half.y,0f)));
                line.SetPosition(1,line.transform.InverseTransformPoint(center+new Vector3(-half.x,half.y,0f)));
                line.SetPosition(2,line.transform.InverseTransformPoint(center+new Vector3(half.x,half.y,0f)));
                line.SetPosition(3,line.transform.InverseTransformPoint(center+new Vector3(half.x,-half.y,0f)));
            }
        }
    }
}
