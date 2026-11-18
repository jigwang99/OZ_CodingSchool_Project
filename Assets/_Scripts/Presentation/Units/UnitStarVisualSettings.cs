using UnityEngine;

namespace AutoBattler.Presentation.Units
{
    /// <summary>성급별 외형 배율과 얇은 테두리의 공통 설정.</summary>
    [CreateAssetMenu(menuName="PixelTactics/Unit Star Visual Settings")]
    public sealed class UnitStarVisualSettings : ScriptableObject
    {
        [SerializeField] private Material outlineMaterial;
        [SerializeField,Min(1f)] private float twoStarScale=1.08f;
        [SerializeField,Min(1f)] private float threeStarScale=1.16f;
        [SerializeField,Range(.5f,2f)] private float outlinePixels=1f;
        [SerializeField] private Color twoStarColor=new Color(.45f,.82f,1f);
        [SerializeField] private Color threeStarColor=new Color(1f,.76f,.18f);
        public Material OutlineMaterial=>outlineMaterial;
        public float OutlinePixels=>outlinePixels;
        public float Scale(int star)=>star==3?threeStarScale:star==2?twoStarScale:1f;
        public Color OutlineColor(int star)=>star==3?threeStarColor:star==2?twoStarColor:Color.clear;
    }
}
