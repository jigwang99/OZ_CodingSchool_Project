using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Presentation.Units;
using UnityEngine;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>유닛 배치 드래그 중 육각 발판 표시와 전장·대기석 강조 색상 갱신.</summary>
    [ExecuteAlways]
    public sealed class BoardOutlineView : MonoBehaviour
    {
        [Header("보드 참조")]
        [SerializeField] private HexGridLayout grid;
        [SerializeField] private BenchLayout bench;
        [SerializeField] private UnitDragController dragController;
        [Header("전장: 열 순서로 56개, 대기석: 슬롯 순서로 8개")]
        [SerializeField] private LineRenderer[] tiles=System.Array.Empty<LineRenderer>();
        [SerializeField] private LineRenderer[] seats=System.Array.Empty<LineRenderer>();
        [Header("육각 발판")]
        [SerializeField] private MeshRenderer[] tileFills=System.Array.Empty<MeshRenderer>();
        [SerializeField] private Material allyFill;
        [SerializeField] private Material enemyFill;
        [SerializeField] private Material highlightFill;
        [SerializeField,Range(.8f,1f)] private float tileScale=.94f;
        [Header("영역 색상")]
        [SerializeField] private Color allyColor=new Color(.15f,.7f,.8f);
        [SerializeField] private Color enemyColor=new Color(.9f,.3f,.4f);
        [SerializeField] private Color seatColor=Color.gray;
        [SerializeField] private Color highlightColor=Color.yellow;
        private bool hasGeometry;
        private Vector3 lastGridPosition,lastFirstSeat,lastLastSeat;
        private float lastCellSize;
        private float lastTileScale;
        private Vector2 lastSeatSize;
        public HexGridLayout Grid=>grid;
        public BenchLayout Bench=>bench;
        public bool TilesVisible { get; private set; }
        public System.Collections.Generic.IReadOnlyList<LineRenderer> Tiles=>System.Array.AsReadOnly(tiles??System.Array.Empty<LineRenderer>());
        public System.Collections.Generic.IReadOnlyList<LineRenderer> Seats=>System.Array.AsReadOnly(seats??System.Array.Empty<LineRenderer>());
        public System.Collections.Generic.IReadOnlyList<MeshRenderer> TileFills=>System.Array.AsReadOnly(tileFills);
        private void OnEnable()
        {
            hasGeometry=false;
            if(Application.isPlaying && dragController!=null)
            {
                dragController.DragStarted+=OnDragStarted;
                dragController.DragEnded+=OnDragEnded;
            }
            SetTilesVisible(Application.isPlaying && dragController!=null && dragController.CanPlaceUnits && dragController.DraggedUnit!=null);
        }
        private void OnDragStarted(UnitView unit)
            =>SetTilesVisible(dragController.CanPlaceUnits && dragController.DraggedUnit!=null);
        private void OnDragEnded()=>SetTilesVisible(false);
        private void SetTilesVisible(bool visible)
        {
            TilesVisible=visible;
            foreach(LineRenderer tile in tiles) if(tile!=null) tile.enabled=visible;
            foreach(MeshRenderer fill in tileFills) if(fill!=null) fill.enabled=visible;
        }
        private void OnDisable()
        {
            if(dragController!=null)
            {
                dragController.DragStarted-=OnDragStarted;
                dragController.DragEnded-=OnDragEnded;
            }
            SetTilesVisible(false);
        }
        private void LateUpdate()
        {
            if(grid==null || bench==null || tiles==null || seats==null
                || tiles.Length!=HexGridLayout.Columns*HexGridLayout.Rows || seats.Length!=PlacementService.BenchCapacity) return;
            Vector3 first=bench.SlotToWorld(0),last=bench.SlotToWorld(PlacementService.BenchCapacity-1);
            if(!hasGeometry || lastGridPosition!=grid.transform.position || lastCellSize!=grid.CellSize || lastTileScale!=tileScale
                || lastFirstSeat!=first || lastLastSeat!=last || lastSeatSize!=bench.SlotSize)
            {
                UpdateGeometry(); lastGridPosition=grid.transform.position; lastCellSize=grid.CellSize;
                lastFirstSeat=first; lastLastSeat=last; lastSeatSize=bench.SlotSize; lastTileScale=tileScale; hasGeometry=true;
            }
            if(TilesVisible) for(int col=0;col<HexGridLayout.Columns;col++) for(int row=0;row<HexGridLayout.Rows;row++)
            {
                bool highlighted=grid.HighlightTile==HexCoord.FromOffset(col,row);
                bool ally=col<HexGridLayout.AllyColumns;
                Color color=highlighted?highlightColor:ally?allyColor:enemyColor;
                int index=col*HexGridLayout.Rows+row;
                LineRenderer line=tiles[index]; line.startColor=line.endColor=color;
                if(index<tileFills.Length)
                {
                    Material material=highlighted?highlightFill:ally?allyFill:enemyFill;
                    if(tileFills[index].sharedMaterial!=material) tileFills[index].sharedMaterial=material;
                }
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
                    line.SetPosition(i,line.transform.InverseTransformPoint(center+grid.CellSize*tileScale*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0f)));
                }
                int index=col*HexGridLayout.Rows+row;
                if(index<tileFills.Length)
                {
                    Transform fill=tileFills[index].transform;
                    fill.position=center;
                    fill.localScale=new Vector3(grid.CellSize*tileScale,grid.CellSize*tileScale,1f);
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
