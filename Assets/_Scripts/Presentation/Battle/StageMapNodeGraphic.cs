using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>지도 지점의 원형 테두리와 이동 표식 표시. 텍스처 생성 없이 UI 메시 재사용.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StageMapNodeGraphic : MaskableGraphic
    {
        [SerializeField,Range(4,64)] private int segments=48;
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear(); Rect area=rectTransform.rect; float radius=Mathf.Min(area.width,area.height)*.5f;
            Vector2 center=area.center;
            Disc(vertices,center+Vector2.down*4f,radius,new Color(.02f,.07f,.09f,.65f));
            Disc(vertices,center,radius,new Color(.16f,.19f,.17f));
            Disc(vertices,center,radius*.94f,new Color(.89f,.69f,.29f));
            Disc(vertices,center,radius*.84f,new Color(1f,.94f,.73f));
            Disc(vertices,center,radius*.75f,new Color(.06f,.20f,.23f));
            Disc(vertices,center,radius*.67f,color);
        }
        private void Disc(VertexHelper vertices,Vector2 center,float radius,Color shade)
        {
            int start=vertices.currentVertCount; vertices.AddVert(center,shade,Vector2.zero);
            for(int i=0;i<segments;i++)
            {
                float angle=2f*Mathf.PI*i/segments;
                vertices.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,shade,Vector2.zero);
            }
            for(int i=0;i<segments;i++) vertices.AddTriangle(start,start+1+i,start+1+(i+1)%segments);
        }
    }
}
