using System;
using AutoBattler.Battle.Combat;
using AutoBattler.Pooling;
using UnityEngine;

namespace AutoBattler.Presentation.Units
{
    /// <summary>타격·투사체·상태 표시 공통 풀 멤버. 반환 시 이전 재생 상태 초기화.</summary>
    [DisallowMultipleComponent]
    public sealed class PooledBattleVisual : MonoBehaviour,IPoolable
    {
        [SerializeField] private BattleVisualId key;
        [SerializeField] private ParticleSystem[] particles=Array.Empty<ParticleSystem>();
        [SerializeField] private SpriteRenderer spriteRenderer;
        private ObjectPoolManager owner;
        private Action<PooledBattleVisual> returned;
        private CombatUnitReference target;
        private Vector3 start,lastDestination;
        private float elapsed,duration,lifetime;
        private bool followStatus,holdUntilImpact;
        public Enum PoolKey=>key;
        public BattleVisualId Id=>key;
        public bool IsRented { get; private set; }
        public int Generation { get; private set; }
        public SpriteRenderer SpriteRenderer=>spriteRenderer;
        public void Init() { Generation++; ResetState(); }
        internal void Bind(ObjectPoolManager manager,Action<PooledBattleVisual> onReturned,float maximumLifetime)
        { owner=manager; returned=onReturned; lifetime=maximumLifetime; IsRented=true; }
        public void PlayParticles(Vector3 position,float scale)
        {
            transform.SetPositionAndRotation(position,Quaternion.Euler(-90f,0f,0f)); transform.localScale=Vector3.one*scale;
            foreach(var system in particles) system.Play(false);
        }
        public void PlayProjectile(Vector3 position,CombatUnitReference destination,Vector3 fallback,Sprite sprite,
            Color color,float seconds,bool hold)
        {
            target=destination; start=position; lastDestination=fallback; duration=Mathf.Max(.01f,seconds); holdUntilImpact=hold;
            SetSprite(sprite,color,.4f); transform.position=position; Aim(lastDestination-start);
        }
        public void ShowStatus(CombatUnitReference unit,Sprite sprite)
        { target=unit; followStatus=true; SetSprite(sprite,Color.white,.32f); FollowStatus(); }
        private void SetSprite(Sprite sprite,Color color,float width)
        {
            spriteRenderer.sprite=sprite; spriteRenderer.color=color; spriteRenderer.enabled=sprite!=null;
            transform.rotation=Quaternion.identity;
            transform.localScale=Vector3.one*(sprite!=null ? width/Mathf.Max(.001f,sprite.bounds.size.x) : 1f);
        }
        private void Aim(Vector3 direction)
        { transform.rotation=Quaternion.Euler(0f,0f,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg-45f); }
        private void LateUpdate()
        {
            if(!IsRented) return;
            elapsed+=Time.deltaTime;
            if(followStatus)
            {
                if(!target.IsAlive || !target.Unit.IsInCombat || !target.Unit.Effects.IsStunned) { ReturnToPool(); return; }
                FollowStatus(); return;
            }
            if(spriteRenderer!=null)
            {
                if(target.IsAlive) lastDestination=target.Unit.transform.position+Vector3.up*.2f;
                transform.position=Vector3.Lerp(start,lastDestination,Mathf.Min(holdUntilImpact ? .94f : 1f,elapsed/duration));
                Aim(lastDestination-start);
                if(!holdUntilImpact && elapsed>=duration) { ReturnToPool(); return; }
            }
            else if(elapsed>.03f)
            {
                bool alive=false;
                foreach(var system in particles) if(system.IsAlive(false)) { alive=true; break; }
                if(!alive) { ReturnToPool(); return; }
            }
            if(elapsed>=lifetime) ReturnToPool();
        }
        private void FollowStatus()
        { if(target.IsCurrent) transform.position=target.Unit.transform.position+Vector3.up*.85f; }
        public void ReturnToPool()
        {
            if(!IsRented) return;
            IsRented=false;
            var callback=returned; returned=null; ResetState();
            if(owner!=null) owner.ReturnObject(key,gameObject);
            callback?.Invoke(this);
        }
        private void ResetState()
        {
            foreach(var system in particles) if(system!=null) system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(spriteRenderer!=null) { spriteRenderer.sprite=null; spriteRenderer.color=Color.white; spriteRenderer.enabled=false; }
            target=default; start=lastDestination=Vector3.zero; elapsed=duration=0f; followStatus=holdUntilImpact=false;
        }
        private void OnDisable() { if(IsRented) ReturnToPool(); else ResetState(); }
#if UNITY_EDITOR
        public void Configure(BattleVisualId id,ParticleSystem[] systems,SpriteRenderer renderer)
        { key=id; particles=systems; spriteRenderer=renderer; }
#endif
    }
}
