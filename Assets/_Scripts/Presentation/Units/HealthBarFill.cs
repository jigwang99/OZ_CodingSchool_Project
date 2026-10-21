using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Units
{
    /// <summary>고정 길이 막대 안에 체력과 회색 보호막을 연속 표시.</summary>
    [DisallowMultipleComponent]
    public sealed class HealthBarFill : MonoBehaviour
    {
        [SerializeField] private Image healthImage;
        [SerializeField] private Image shieldImage;
        [SerializeField,Min(0f)] private float tweenDuration=.15f;
        private Tween barTween;
        private float displayedHealth,displayedShield,targetHealth,targetShield;
        public Image HealthImage=>healthImage;
        public Image ShieldImage=>shieldImage;
        public float HealthRatio=>displayedHealth;
        public float ShieldRatio=>displayedShield;
#if UNITY_EDITOR
        public void Configure(Image health,Image shield) { healthImage=health; shieldImage=shield; }
#endif
        public void SetValues(int health,int maximum,int shield,bool animate)
        {
            // 보호막이 최대 체력을 넘어도 합산 오버플로 없이 막대 안에 표시.
            double hp=System.Math.Max(0,health),armor=System.Math.Max(0,shield);
            double capacity=System.Math.Max(System.Math.Max(1,maximum),hp+armor);
            float nextHealth=(float)(hp/capacity),nextShield=(float)(armor/capacity);
            if(animate && Mathf.Approximately(targetHealth,nextHealth) && Mathf.Approximately(targetShield,nextShield)) return;
            barTween?.Kill(); barTween=null;
            targetHealth=nextHealth; targetShield=nextShield;
            if(!animate || !isActiveAndEnabled || tweenDuration<=0f)
            { displayedHealth=targetHealth; displayedShield=targetShield; Draw(); return; }
            float fromHealth=displayedHealth,fromShield=displayedShield;
            barTween=DOVirtual.Float(0f,1f,tweenDuration,value=>
            {
                displayedHealth=Mathf.Lerp(fromHealth,targetHealth,value);
                displayedShield=Mathf.Lerp(fromShield,targetShield,value);
                Draw();
            }).SetEase(Ease.OutQuad).SetUpdate(true);
        }
        private void Draw()
        {
            var health=healthImage.rectTransform; var shield=shieldImage.rectTransform;
            health.localScale=new Vector3(displayedHealth,1f,1f);
            shield.anchoredPosition=health.anchoredPosition+new Vector2(health.rect.width*displayedHealth,0f);
            shield.localScale=new Vector3(displayedShield,1f,1f);
            shieldImage.enabled=displayedShield>0f;
        }
        public void Clear() { SetValues(0,1,0,false); }
        private void OnDisable() { barTween?.Kill(); barTween=null; }
    }
}
