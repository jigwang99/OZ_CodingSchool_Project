using System;
using System.Collections.Generic;
using AutoBattler.Battle.Grid;

namespace AutoBattler.Battle.Combat
{
    /// <summary>동일 비용의 6방향 A*. f, h, Offset 좌표 순서로 동점 처리.</summary>
    public static class HexPathfinder
    {
        private readonly struct Node
        {
            public readonly HexCoord Cell; public readonly int G,H;
            public Node(HexCoord cell,int g,int h) { Cell=cell; G=g; H=h; }
        }
        private sealed class MinHeap
        {
            private readonly List<Node> items=new List<Node>();
            public int Count=>items.Count;
            private static int Compare(Node a,Node b)
            {
                int compare=(a.G+a.H).CompareTo(b.G+b.H); if(compare!=0) return compare;
                compare=a.H.CompareTo(b.H); if(compare!=0) return compare;
                a.Cell.ToOffset(out int ac,out int ar); b.Cell.ToOffset(out int bc,out int br);
                compare=ac.CompareTo(bc); return compare!=0?compare:ar.CompareTo(br);
            }
            public void Push(Node node)
            {
                items.Add(node); int i=items.Count-1;
                while(i>0) { int p=(i-1)/2; if(Compare(items[p],node)<=0) break; items[i]=items[p]; i=p; }
                items[i]=node;
            }
            public Node Pop()
            {
                Node result=items[0],last=items[items.Count-1]; items.RemoveAt(items.Count-1);
                if(items.Count==0) return result;
                int i=0;
                while(i*2+1<items.Count)
                {
                    int child=i*2+1;
                    if(child+1<items.Count && Compare(items[child+1],items[child])<0) child++;
                    if(Compare(last,items[child])<=0) break;
                    items[i]=items[child]; i=child;
                }
                items[i]=last; return result;
            }
        }
        public static List<HexCoord> Find(CombatBoard board,int id,HexCoord start,HexCoord target,int range)
        {
            HashSet<HexCoord> goals=new HashSet<HexCoord>();
            for(int col=0;col<8;col++) for(int row=0;row<7;row++)
            {
                HexCoord cell=HexCoord.FromOffset(col,row);
                if(cell!=target && cell.DistanceTo(target)<=range && board.IsFree(cell,id)) goals.Add(cell);
            }
            if(goals.Count==0) return null;
            MinHeap open=new MinHeap(); Dictionary<HexCoord, int> scores=new Dictionary<HexCoord,int>();
            Dictionary<HexCoord, HexCoord> previous=new Dictionary<HexCoord,HexCoord>(); HashSet<HexCoord> closed=new HashSet<HexCoord>();
            scores[start]=0; open.Push(new Node(start,0,Heuristic(start,target,range)));
            while(open.Count>0)
            {
                Node node=open.Pop(); if(!closed.Add(node.Cell)) continue;
                if(goals.Contains(node.Cell))
                {
                    List<HexCoord> path=new List<HexCoord>(); HexCoord current=node.Cell;
                    while(current!=start) { path.Add(current); current=previous[current]; }
                    path.Reverse(); return path;
                }
                for(int direction=0;direction<6;direction++)
                {
                    HexCoord next=node.Cell.GetNeighbor(direction);
                    if(closed.Contains(next) || !board.IsFree(next,id)) continue;
                    int score=node.G+1;
                    if(scores.TryGetValue(next,out int old) && old<=score) continue;
                    scores[next]=score; previous[next]=node.Cell;
                    open.Push(new Node(next,score,Heuristic(next,target,range)));
                }
            }
            return null;
        }
        private static int Heuristic(HexCoord cell,HexCoord target,int range)=>Math.Max(0,cell.DistanceTo(target)-range);
    }
}
