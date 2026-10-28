using UnityEngine;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>전투 카메라 뒤에 배경 표시. 화면 비율을 유지하며 빈 가장자리 없이 채움.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleBackgroundView : MonoBehaviour
    {
        [SerializeField] private Camera boardCamera;
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField, Min(1f)] private float cameraDistance = 20f;
        public SpriteRenderer BackgroundRenderer=>backgroundRenderer;
        public Camera BoardCamera=>boardCamera;
        public void Show(Sprite sprite)
        {
            backgroundRenderer.sprite=sprite;
            backgroundRenderer.enabled=sprite!=null;
            FitToCamera();
        }
        private void LateUpdate()=>FitToCamera();
        private void FitToCamera()
        {
            if(boardCamera==null || backgroundRenderer==null || backgroundRenderer.sprite==null) return;
            float height=boardCamera.orthographicSize*2f;
            float width=height*boardCamera.aspect;
            Vector2 size=backgroundRenderer.sprite.bounds.size;
            float scale=Mathf.Max(width/size.x,height/size.y);
            Transform target=backgroundRenderer.transform;
            target.SetPositionAndRotation(boardCamera.transform.position+boardCamera.transform.forward*cameraDistance,
                boardCamera.transform.rotation);
            target.localScale=Vector3.one*scale;
        }
    }
}
