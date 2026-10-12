using AutoBattler.Battle.Grid;
using UnityEngine;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>Game 뷰에서도 배치 영역과 강조를 표시.</summary>
    public sealed class BoardOutlineView : MonoBehaviour
    {
        private HexGridLayout grid; private BenchLayout bench;
        private LineRenderer[,] tiles;
        private LineRenderer[] seats;
        private Material material;
        private void Start()
        {
            grid=GetComponent<HexGridLayout>(); bench=GetComponentInChildren<BenchLayout>();
            material=new Material(Shader.Find("Sprites/Default"));
            tiles=new LineRenderer[8,7];
            for(int col=0;col<8;col++) for(int row=0;row<7;row++)
            {
                Vector3 center=grid.CellToWorld(HexCoord.FromOffset(col,row));
                var points=new Vector3[6];
                for(int i=0;i<6;i++) { float angle=i*60f*Mathf.Deg2Rad; points[i]=center+grid.CellSize*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0f); }
                tiles[col,row]=CreateLine($"Tile_{col}_{row}",points);
            }
            seats=new LineRenderer[8];
            for(int i=0;i<8;i++)
            {
                Vector3 c=bench.SlotToWorld(i);
                seats[i]=CreateLine($"Bench_{i}",new[]{c+new Vector3(-.6f,-.6f,0),c+new Vector3(-.6f,.6f,0),c+new Vector3(.6f,.6f,0),c+new Vector3(.6f,-.6f,0)});
            }
        }
        private LineRenderer CreateLine(string name,Vector3[] points)
        {
            var obj=new GameObject(name); obj.transform.SetParent(transform,false);
            var line=obj.AddComponent<LineRenderer>(); line.sharedMaterial=material; line.useWorldSpace=true;
            line.positionCount=points.Length; line.SetPositions(points); line.loop=true; line.widthMultiplier=.025f; line.sortingOrder=-10;
            return line;
        }
        private void LateUpdate()
        {
            if(tiles==null) return;
            for(int col=0;col<8;col++) for(int row=0;row<7;row++)
            {
                Color color=grid.HighlightTile==HexCoord.FromOffset(col,row)?Color.yellow:
                    col<4?new Color(.15f,.7f,.8f):new Color(.9f,.3f,.4f);
                tiles[col,row].startColor=tiles[col,row].endColor=color;
            }
            for(int i=0;i<8;i++) seats[i].startColor=seats[i].endColor=bench.HighlightedSlot==i?Color.yellow:Color.gray;
        }
        private void OnDestroy() { if(material!=null) Destroy(material); }
    }
}
