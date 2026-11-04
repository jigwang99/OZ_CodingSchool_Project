using System;
using System.Collections.Generic;
using AutoBattler.Battle.Grid;
using UnityEngine;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>미리 배치한 대기석 바닥 8개의 위치·크기와 배치 강조 표시 관리.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BenchFloorView : MonoBehaviour
    {
        [SerializeField] private BenchLayout bench;
        [SerializeField] private SpriteRenderer[] slots=Array.Empty<SpriteRenderer>();
        [SerializeField] private LineRenderer[] highlights=Array.Empty<LineRenderer>();
        private Sprite currentSprite;
        private bool hasGeometry;
        private Vector3 firstPosition,lastPosition;
        private Vector2 slotSize;
        public BenchLayout Bench=>bench;
        public IReadOnlyList<SpriteRenderer> Slots=>Array.AsReadOnly(slots);
        public Sprite CurrentSprite=>currentSprite;

        public void Show(Sprite sprite)
        {
            currentSprite=sprite;
            foreach(SpriteRenderer slot in slots)
            {
                slot.sprite=sprite;
                slot.enabled=sprite!=null;
            }
            hasGeometry=false;
            Refresh();
        }

        private void OnEnable()=>hasGeometry=false;
        private void LateUpdate()=>Refresh();
        private void Refresh()
        {
            if(bench==null || slots.Length==0) return;
            Vector3 first=bench.SlotToWorld(0),last=bench.SlotToWorld(slots.Length-1);
            if(!hasGeometry || firstPosition!=first || lastPosition!=last || slotSize!=bench.SlotSize)
            {
                for(int i=0;i<slots.Length;i++)
                {
                    Transform target=slots[i].transform;
                    target.position=bench.SlotToWorld(i);
                    if(currentSprite!=null)
                    {
                        Vector2 size=currentSprite.bounds.size;
                        target.localScale=new Vector3(bench.SlotSize.x/size.x,bench.SlotSize.y/size.y,1f);
                    }
                }
                firstPosition=first; lastPosition=last; slotSize=bench.SlotSize; hasGeometry=true;
            }
            for(int i=0;i<highlights.Length;i++)
                highlights[i].enabled=currentSprite==null || bench.HighlightedSlot==i;
        }

        private void OnDisable()
        {
            foreach(LineRenderer highlight in highlights)
                if(highlight!=null) highlight.enabled=true;
        }
    }
}
