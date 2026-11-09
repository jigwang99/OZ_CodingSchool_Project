using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>배치툴 전용 육각 타일 채움·테두리와 정확한 포인터 영역.</summary>
    [RequireComponent(typeof(CanvasRenderer),typeof(RectTransform))]
    public sealed class PlannerHexGraphic : MaskableGraphic,ICanvasRaycastFilter
    {
        [SerializeField] private Color emptyColor=new Color(.08f,.12f,.15f,1f);
        [SerializeField] private Color occupiedColor=new Color(.12f,.24f,.27f,1f);
        [SerializeField] private Color borderColor=new Color(.38f,.55f,.58f,1f);
        [SerializeField] private Color highlightColor=new Color(.45f,.95f,.83f,1f);
        [SerializeField,Min(1f)] private float borderWidth=2f;
        private bool occupied,highlighted;
        public void SetState(bool hasUnit,bool selected)
        { if(occupied==hasUnit && highlighted==selected) return; occupied=hasUnit; highlighted=selected; SetVerticesDirty(); }
        private float Radius=>Mathf.Min(rectTransform.rect.width*.5f,rectTransform.rect.height/Mathf.Sqrt(3f));
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Vector2 center=rectTransform.rect.center;
            float radius=Radius,inner=Mathf.Max(0f,radius-borderWidth);
            Color fill=occupied?occupiedColor:emptyColor,border=highlighted?highlightColor:borderColor;
            mesh.AddVert(center,fill,Vector2.zero);
            for(int i=0;i<6;i++)
            {
                float angle=Mathf.Deg2Rad*60f*i; Vector2 direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                mesh.AddVert(center+direction*inner,fill,Vector2.zero);
                mesh.AddVert(center+direction*inner,border,Vector2.zero);
                mesh.AddVert(center+direction*radius,border,Vector2.zero);
            }
            for(int i=0;i<6;i++)
            {
                int current=1+i*3,next=1+(i+1)%6*3;
                mesh.AddTriangle(0,current,next);
                mesh.AddTriangle(current+1,current+2,next+2);
                mesh.AddTriangle(current+1,next+2,next+1);
            }
        }
        public bool IsRaycastLocationValid(Vector2 screenPoint,Camera eventCamera)
        {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,screenPoint,eventCamera,out Vector2 point)) return false;
            point-=rectTransform.rect.center; float x=Mathf.Abs(point.x),y=Mathf.Abs(point.y),radius=Radius;
            return x<=radius && y<=Mathf.Sqrt(3f)*radius*.5f && Mathf.Sqrt(3f)*x+y<=Mathf.Sqrt(3f)*radius;
        }
    }
}
