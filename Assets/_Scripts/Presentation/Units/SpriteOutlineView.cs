using UnityEngine;

namespace AutoBattler.Presentation.Units
{
    /// <summary>원본 스프라이트를 바꾸지 않고 뒤쪽 메시로 한 픽셀 외곽선 표시.</summary>
    [DisallowMultipleComponent]
    public sealed class SpriteOutlineView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer source;
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer outlineRenderer;
        private static readonly int MainTexture=Shader.PropertyToID("_MainTex");
        private static readonly int TexelSize=Shader.PropertyToID("_MainTex_TexelSize");
        private static readonly int UVBounds=Shader.PropertyToID("_UVBounds");
        private static readonly int OutlineColor=Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlinePixels=Shader.PropertyToID("_OutlinePixels");
        private static readonly int[] Triangles={0,1,2,0,2,3};
        private readonly Vector3[] vertices=new Vector3[4];
        private readonly Vector2[] uvs=new Vector2[4];
        private MaterialPropertyBlock properties;
        private Mesh mesh;
        private Sprite displayedSprite;
        private bool flipX,flipY,dirty=true;
        private float pixels=1f,sourceAlpha=-1f;
        private Color color;
        public SpriteRenderer Source=>source;
        public MeshRenderer Renderer=>outlineRenderer;
        public Sprite DisplayedSprite=>displayedSprite;
        private void Awake()=>PrepareMesh();
        private void PrepareMesh()
        {
            if(properties==null) properties=new MaterialPropertyBlock();
            if(mesh!=null) return;
            mesh=new Mesh {name="Pooled Sprite Outline"}; mesh.MarkDynamic(); meshFilter.sharedMesh=mesh;
            outlineRenderer.enabled=false;
        }
        public void SetStyle(Color outlineColor,float width,Material material)
        {
            PrepareMesh(); color=outlineColor; pixels=width; dirty=true;
            outlineRenderer.sharedMaterial=material;
            if(color.a<=0f) outlineRenderer.enabled=false;
        }
        public void Clear() {color=Color.clear; if(outlineRenderer!=null) outlineRenderer.enabled=false;}
        private void LateUpdate()
        {
            bool visible=color.a>0f && source!=null && source.enabled && source.gameObject.activeInHierarchy && source.sprite!=null;
            outlineRenderer.enabled=visible;
            if(!visible) return;
            bool changed=source.sprite!=displayedSprite || source.flipX!=flipX || source.flipY!=flipY;
            if(changed || dirty) UpdateMesh();
            if(changed || dirty || !Mathf.Approximately(sourceAlpha,source.color.a))
            {
                sourceAlpha=source.color.a;
                Color tint=color; tint.a*=sourceAlpha;
                properties.SetColor(OutlineColor,tint); outlineRenderer.SetPropertyBlock(properties);
            }
            dirty=false;
            outlineRenderer.sortingLayerID=source.sortingLayerID; outlineRenderer.sortingOrder=source.sortingOrder-1;
        }
        private void UpdateMesh()
        {
            displayedSprite=source.sprite; flipX=source.flipX; flipY=source.flipY;
            Vector2[] points=displayedSprite.vertices,coordinates=displayedSprite.uv;
            Vector2 minimum=points[0],maximum=points[0],uvMinimum=coordinates[0],uvMaximum=coordinates[0];
            for(int i=1;i<points.Length;i++)
            {
                minimum=Vector2.Min(minimum,points[i]); maximum=Vector2.Max(maximum,points[i]);
                uvMinimum=Vector2.Min(uvMinimum,coordinates[i]); uvMaximum=Vector2.Max(uvMaximum,coordinates[i]);
            }
            Texture texture=displayedSprite.texture;
            Vector2 texel=new Vector2(1f/texture.width,1f/texture.height);
            Vector2 padding=Vector2.one*(pixels/displayedSprite.pixelsPerUnit);
            Vector2 low=minimum-padding,high=maximum+padding;
            vertices[0]=new Vector3(low.x,low.y); vertices[1]=new Vector3(low.x,high.y);
            vertices[2]=new Vector3(high.x,high.y); vertices[3]=new Vector3(high.x,low.y);
            Vector2 uvLow=uvMinimum-texel*pixels,uvHigh=uvMaximum+texel*pixels;
            uvs[0]=new Vector2(uvLow.x,uvLow.y); uvs[1]=new Vector2(uvLow.x,uvHigh.y);
            uvs[2]=new Vector2(uvHigh.x,uvHigh.y); uvs[3]=new Vector2(uvHigh.x,uvLow.y);
            for(int i=0;i<vertices.Length;i++) vertices[i]=new Vector3(vertices[i].x*(flipX?-1f:1f),vertices[i].y*(flipY?-1f:1f),0f);
            mesh.vertices=vertices; mesh.uv=uvs; mesh.triangles=Triangles; mesh.RecalculateBounds();
            properties.SetTexture(MainTexture,texture);
            properties.SetVector(TexelSize,new Vector4(texel.x,texel.y,texture.width,texture.height));
            properties.SetVector(UVBounds,new Vector4(uvMinimum.x,uvMinimum.y,uvMaximum.x,uvMaximum.y));
            properties.SetFloat(OutlinePixels,pixels);
        }
        private void OnDisable() {if(outlineRenderer!=null) outlineRenderer.enabled=false;}
        private void OnDestroy() {if(mesh!=null) Destroy(mesh);}
    }
}
