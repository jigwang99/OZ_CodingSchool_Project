using System;
using AutoBattler.Battle.Combat;
using AutoBattler.Pooling;
using UnityEngine;

namespace AutoBattler.Presentation.Units
{
    /// <summary>애니메이션·파티클·투사체·지속 상태를 같은 풀 수명으로 재사용.</summary>
    [DisallowMultipleComponent]
    public sealed class PooledBattleVisual : MonoBehaviour,IPoolable
    {
        private enum Playback { None, Effect, Projectile, Status }
        [SerializeField] private BattleVisualId key;
        [SerializeField] private ParticleSystem[] particles=Array.Empty<ParticleSystem>();
        [SerializeField] private Animator[] animators=Array.Empty<Animator>();
        [SerializeField] private TrailRenderer[] trails=Array.Empty<TrailRenderer>();
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Vector3 effectRotation=new Vector3(-90f,0f,0f);
        [SerializeField] private bool directional;
        [SerializeField] private float angleOffset=-45f;
        private ObjectPoolManager owner;
        private Action<PooledBattleVisual> returned;
        private CombatUnitReference target;
        private Vector3 start,lastDestination,statusOffset;
        private float elapsed,duration,lifetime;
        private bool holdUntilImpact;
        private Playback playback;
        public Enum PoolKey=>key;
        public BattleVisualId Id=>key;
        public bool IsRented { get; private set; }
        public int Generation { get; private set; }
        public SpriteRenderer SpriteRenderer=>spriteRenderer;
        public int AnimatorCount=>animators.Length;
        public int ParticleCount=>particles.Length;
        public void Init(){Generation++;ResetState();}
        internal void Bind(ObjectPoolManager manager,Action<PooledBattleVisual> onReturned,float maximumLifetime)
        {owner=manager;returned=onReturned;lifetime=maximumLifetime;IsRented=true;}
        public void PlayEffect(Vector3 position,float scale,Vector3 direction)
        {
            playback=Playback.Effect;transform.SetPositionAndRotation(position,Quaternion.Euler(effectRotation));transform.localScale=Vector3.one*scale;
            if(directional && direction.sqrMagnitude>.0001f)Aim(direction);
            RestartContent();
        }
        public void PlayProjectile(Vector3 position,CombatUnitReference destination,Vector3 fallback,Sprite sprite,
            Color color,float seconds,bool hold,float scale)
        {
            playback=Playback.Projectile;target=destination;start=position;lastDestination=fallback;
            duration=Mathf.Max(.01f,seconds);holdUntilImpact=hold;transform.position=position;transform.localScale=Vector3.one*scale;
            if(spriteRenderer!=null)SetSprite(sprite,color,.4f);else RestartContent();
            Aim(lastDestination-start);
        }
        public void FollowEffect(CombatUnitReference unit,float scale,Vector3 offset)
        {
            playback=Playback.Status;target=unit;statusOffset=offset;
            transform.rotation=Quaternion.Euler(effectRotation);transform.localScale=Vector3.one*scale;
            RestartContent();FollowStatus();
        }
        private void RestartContent()
        {
            foreach(Animator animator in animators)
            {
                if(animator==null || !animator.gameObject.activeInHierarchy)continue;
                animator.enabled=true;animator.fireEvents=false;animator.speed=1f;animator.Rebind();animator.Update(0f);
            }
            foreach(ParticleSystem system in particles)
            {
                if(system==null || !system.gameObject.activeInHierarchy)continue;
                system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);system.Play(false);
            }
            foreach(TrailRenderer trail in trails)if(trail!=null)trail.Clear();
        }
        private void SetSprite(Sprite sprite,Color color,float width)
        {
            spriteRenderer.sprite=sprite;spriteRenderer.color=color;spriteRenderer.enabled=sprite!=null;
            transform.rotation=Quaternion.identity;
            transform.localScale=Vector3.one*(sprite!=null?width/Mathf.Max(.001f,sprite.bounds.size.x):1f);
        }
        private void Aim(Vector3 direction)
        {transform.rotation=Quaternion.Euler(0f,0f,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg+angleOffset);}
        private void LateUpdate()
        {
            if(!IsRented)return;
            elapsed+=Time.deltaTime;
            if(playback==Playback.Status)
            {
                if(!target.IsAlive || !target.Unit.IsInCombat){ReturnToPool();return;}
                FollowStatus();return;
            }
            if(playback==Playback.Projectile)
            {
                if(target.IsAlive)lastDestination=target.Unit.transform.position+Vector3.up*.2f;
                transform.position=Vector3.Lerp(start,lastDestination,Mathf.Min(holdUntilImpact ? .94f : 1f,elapsed/duration));
                Aim(lastDestination-start);
                if(!holdUntilImpact && elapsed>=duration){ReturnToPool();return;}
            }
            else if(playback==Playback.Effect && animators.Length==0 && elapsed>.03f)
            {
                bool alive=false;foreach(ParticleSystem system in particles)if(system.IsAlive(false)){alive=true;break;}
                if(!alive){ReturnToPool();return;}
            }
            if(elapsed>=lifetime)ReturnToPool();
        }
        private void FollowStatus()
        {if(target.IsCurrent)transform.position=target.Unit.transform.position+statusOffset;}
        public void ReturnToPool()
        {
            if(!IsRented)return;
            IsRented=false;Action<PooledBattleVisual> callback=returned;returned=null;ResetState();
            if(owner!=null)owner.ReturnObject(key,gameObject);callback?.Invoke(this);
        }
        private void ResetState()
        {
            foreach(ParticleSystem system in particles)if(system!=null)system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach(Animator animator in animators)if(animator!=null)animator.enabled=false;
            foreach(TrailRenderer trail in trails)if(trail!=null)trail.Clear();
            if(spriteRenderer!=null){spriteRenderer.sprite=null;spriteRenderer.color=Color.white;spriteRenderer.enabled=false;}
            target=default;start=lastDestination=statusOffset=Vector3.zero;elapsed=duration=0;holdUntilImpact=false;playback=Playback.None;
        }
        private void OnDisable(){if(IsRented)ReturnToPool();else ResetState();}
    }
}
