using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Skills;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Skills;
using AutoBattler.Pooling;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Presentation.Units
{
    /// <summary>전투 판정 이벤트를 미리 준비한 표현 풀에 연결. 풀 부족 시 표현만 생략.</summary>
    public sealed class BattleVisualService
    {
        private sealed class Loaded
        { public BattleVisualEntry Entry; public AssetService.Lease<GameObject> Lease; public Pool Pool; }
        private readonly AssetService assets;
        private readonly BattleVisualSettings settings;
        private readonly BattleFlowController flow;
        private readonly HexGridLayout grid;
        private readonly ObjectPoolManager manager;
        private readonly Dictionary<BattleVisualId,Loaded> loaded=new Dictionary<BattleVisualId,Loaded>();
        private readonly HashSet<PooledBattleVisual> active=new HashSet<PooledBattleVisual>();
        private readonly List<Binding> bindings=new List<Binding>();
        private bool inBattle,disposed;
        public int PrefabLeaseCount=>loaded.Count;
        public int ActiveCount=>active.Count;
        public int CreatedCount=>loaded.Values.Sum(item=>item.Pool.InstanceCount);
        public int PlayedCount { get; private set; }
        public int SkippedCount { get; private set; }
        public IReadOnlyCollection<PooledBattleVisual> Active=>active;
        public BattleVisualService(AssetService service,BattleVisualSettings configuration,Transform parent,
            BattleFlowController phases,HexGridLayout layout)
        {
            assets=service; settings=configuration; flow=phases; grid=layout;
            GameObject root=new GameObject("BattleVisualPool"); root.transform.SetParent(parent,false);
            manager=root.AddComponent<ObjectPoolManager>(); flow.PhaseChanged+=OnPhaseChanged;
        }
        public async UniTask PreloadAsync(CancellationToken token)
        {
            foreach(BattleVisualEntry entry in settings.Effects)
            {
                if(loaded.ContainsKey(entry.id)) continue;
                AssetService.Lease<GameObject> lease=await assets.AcquireAsync<GameObject>(entry.prefab,token);
                try
                {
                    token.ThrowIfCancellationRequested();
                    loaded.Add(entry.id,new Loaded {Entry=entry,Lease=lease,Pool=manager.RegisterPool(entry.id,lease.Asset,entry.capacity)});
                }
                catch { lease.Dispose(); throw; }
            }
        }
        public void BeginBattle(IEnumerable<Unit> units)
        {
            EndBattle(); inBattle=true;
            foreach(Unit unit in units) bindings.Add(new Binding(this,unit));
        }
        public void EndBattle()
        {
            inBattle=false;
            foreach(Binding binding in bindings) binding.Dispose(); bindings.Clear();
            foreach(PooledBattleVisual visual in active.ToArray()) if(visual!=null) visual.ReturnToPool();
            active.Clear();
        }
        private void OnPhaseChanged(BattlePhase phase) { if(phase!=BattlePhase.Combat) EndBattle(); }
        private PooledBattleVisual Rent(BattleVisualId id)
        {
            if(disposed || !inBattle || !loaded.TryGetValue(id,out Loaded item)) return null;
            // 기존 Pool의 확장 생성 경로를 사용하지 않는 대여.
            if(!manager.TryGetObject<PooledBattleVisual>(id,out PooledBattleVisual visual)) { SkippedCount++; return null; }
            visual.Bind(manager,OnReturned,item.Entry.lifetime); active.Add(visual); PlayedCount++; return visual;
        }
        private void OnReturned(PooledBattleVisual visual)=>active.Remove(visual);
        public PooledBattleVisual Play(BattleVisualId id,Vector3 position)
        {
            PooledBattleVisual visual=Rent(id);
            if(visual!=null) visual.PlayParticles(position,loaded[id].Entry.scale);
            return visual;
        }
        private PooledBattleVisual Projectile(Unit caster,Unit target,float seconds,bool magic,Color color,bool hold)
        {
            PooledBattleVisual visual=Rent(BattleVisualId.Projectile);
            if(visual!=null) visual.PlayProjectile(caster.transform.position+Vector3.up*.2f,new CombatUnitReference(target),
                target.transform.position+Vector3.up*.2f,magic?settings.MagicOrb:settings.Arrow,color,seconds,hold);
            return visual;
        }
        public async UniTask DisposeAsync()
        {
            if(disposed) return;
            disposed=true; flow.PhaseChanged-=OnPhaseChanged; EndBattle();
            manager.Dispose(); UnityEngine.Object.Destroy(manager.gameObject);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            foreach(Loaded item in loaded.Values) item.Lease.Dispose(); loaded.Clear();
        }
        private sealed class Binding : IDisposable
        {
            private readonly BattleVisualService service;
            private readonly Unit unit;
            private readonly CombatUnitReference reference;
            private int shield;
            private PooledBattleVisual stun,projectile;
            private int stunGeneration,projectileGeneration;
            public Binding(BattleVisualService owner,Unit actor)
            {
                service=owner; unit=actor; reference=new CombatUnitReference(actor); shield=unit.Effects.Shield;
                unit.Health.DamageResolved+=OnDamage; unit.Health.Healed+=OnHealed; unit.Health.Died+=OnDied;
                unit.Effects.Changed+=OnEffects; unit.Combat.ProjectileReleased+=OnProjectile;
                unit.Skill.EffectReleased+=OnSkillReleased; unit.Skill.EffectApplied+=OnSkillApplied; unit.Skill.CastFinished+=OnCastFinished;
                OnEffects();
            }
            private bool Current=>reference.IsCurrent && unit.isActiveAndEnabled;
            private Vector3 Position=>unit.transform.position+Vector3.up*.2f;
            private void OnDamage(DamageResult result)
            {
                if(!Current || result.Target.Unit!=unit) return;
                if(result.Context.Kind==DamageKind.Skill)
                {
                    SkillVisualEntry skill=service.settings.Skill(result.Context.SourceId);
                    if(skill?.areaImpact==true) return;
                    service.Play(skill?.impact ?? BattleVisualId.MagicHit,Position);
                }
                else service.Play(result.Context.Attacker.ClassId==2 ? BattleVisualId.MagicHit : BattleVisualId.Hit,Position);
            }
            private void OnHealed(int amount) { if(Current && amount>0) service.Play(BattleVisualId.Heal,Position); }
            private void OnEffects()
            {
                if(!Current) return;
                int current=unit.Effects.Shield;
                if(current>shield) service.Play(BattleVisualId.Shield,Position);
                shield=current;
                if(!unit.Effects.IsStunned) Return(ref stun,stunGeneration);
                else if(stun==null || !stun.IsRented || stun.Generation!=stunGeneration)
                {
                    stun=service.Rent(BattleVisualId.Stun);
                    if(stun!=null) { stunGeneration=stun.Generation; stun.ShowStatus(reference,service.settings.Stun); }
                }
            }
            private void OnDied()=>Return(ref stun,stunGeneration);
            private void OnProjectile(UnitCombat target,float seconds)
            {
                if(!Current || target==null) return;
                Unit destination=target.GetComponent<Unit>();
                bool magic=unit.Definition.ClassId==2;
                service.Projectile(unit,destination,seconds,magic,magic ? new Color(.6f,.9f,1f) : Color.white,false);
            }
            private void OnSkillReleased(SkillCastContext context)
            {
                if(!Current || context.Definition.Delivery!=SkillDelivery.Projectile || !context.Target.IsValid) return;
                SkillVisualEntry presentation=service.settings.Skill(context.Definition.Id);
                float seconds=unit.CurrentCell.Value.DistanceTo(context.Center)/Mathf.Max(.01f,context.Definition.ProjectileSpeed);
                projectile=service.Projectile(unit,context.Target.Unit,seconds,presentation?.magicProjectile==true,
                    presentation?.projectileColor ?? Color.white,true);
                if(projectile!=null) projectileGeneration=projectile.Generation;
            }
            private void OnSkillApplied(SkillCastContext context)
            {
                Return(ref projectile,projectileGeneration);
                if(!Current) return;
                SkillVisualEntry presentation=service.settings.Skill(context.Definition.Id);
                if(presentation?.areaImpact==true) service.Play(presentation.impact,service.grid.CellToWorld(context.Center));
            }
            private void OnCastFinished()=>Return(ref projectile,projectileGeneration);
            private static void Return(ref PooledBattleVisual visual,int generation)
            { if(visual!=null && visual.IsRented && visual.Generation==generation) visual.ReturnToPool(); visual=null; }
            public void Dispose()
            {
                unit.Health.DamageResolved-=OnDamage; unit.Health.Healed-=OnHealed; unit.Health.Died-=OnDied;
                unit.Effects.Changed-=OnEffects; unit.Combat.ProjectileReleased-=OnProjectile;
                unit.Skill.EffectReleased-=OnSkillReleased; unit.Skill.EffectApplied-=OnSkillApplied; unit.Skill.CastFinished-=OnCastFinished;
                Return(ref stun,stunGeneration); Return(ref projectile,projectileGeneration);
            }
        }
    }
}
