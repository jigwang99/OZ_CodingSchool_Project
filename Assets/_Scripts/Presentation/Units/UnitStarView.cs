using System;
using UnityEngine;

namespace AutoBattler.Presentation.Units
{
    /// <summary>프리팹에 지정한 기본 크기를 유지하며 성급 배율과 테두리 적용.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitStarView : MonoBehaviour
    {
        [SerializeField] private UnitStarVisualSettings settings;
        [SerializeField] private Transform model;
        [SerializeField] private Transform healthBar;
        [SerializeField] private SpriteOutlineView[] outlines=Array.Empty<SpriteOutlineView>();
        private Vector3 baseScale,baseHealthPosition;
        private bool captured;
        public UnitStarVisualSettings Settings=>settings;
        public Transform Model=>model;
        public Transform HealthBar=>healthBar;
        public int DisplayedStar { get; private set; }=1;
        public float ScaleMultiplier=>settings.Scale(DisplayedStar);
        public Color OutlineColor=>settings.OutlineColor(DisplayedStar);
        public SpriteOutlineView[] Outlines=>outlines;
        private void Awake()=>Capture();
        private void Capture()
        {
            if(captured) return;
            baseScale=model.localScale; baseHealthPosition=healthBar.localPosition; captured=true;
        }
        public void Apply(int star)
        {
            Capture(); DisplayedStar=star;
            float scale=settings.Scale(star);
            model.localScale=new Vector3(baseScale.x*scale,baseScale.y*scale,baseScale.z);
            // 외형 크기만 키우고 HP·MP·아이템 칸의 크기는 유지.
            healthBar.localPosition=baseHealthPosition+Vector3.up*baseHealthPosition.y*(scale-1f);
            foreach(SpriteOutlineView outline in outlines)
                outline.SetStyle(settings.OutlineColor(star),settings.OutlinePixels,settings.OutlineMaterial);
        }
        private void OnDisable()
        {
            if(captured) {model.localScale=baseScale; healthBar.localPosition=baseHealthPosition;}
            DisplayedStar=1;
            foreach(SpriteOutlineView outline in outlines) outline.Clear();
        }
    }
}
