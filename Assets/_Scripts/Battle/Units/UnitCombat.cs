using System;
using System.Threading;
using AutoBattler.Battle.Combat;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    /// <summary>공격 선딜, 투사체 도착, 쿨다운과 기본 공격 피해 담당.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Unit),typeof(UnitHealth))]
    public sealed class UnitCombat : MonoBehaviour
    {
        [SerializeField,Min(0)] private int manaOnDamage=10;
        private Unit unit;
        private UnitHealth health;
        private float nextAttackTime;
        private int roundGeneration;
        private CancellationTokenSource windupCancellation;
        public bool IsAttacking { get; private set; }
        public int Mana=>unit.Mana.Current;
        public bool CanFight=>isActiveAndEnabled && unit!=null && unit.IsInCombat && health!=null && health.IsAlive;
        public bool CanAttack=>CanFight && !unit.Effects.IsStunned && !IsAttacking && !unit.Skill.IsCasting && Time.time>=nextAttackTime && !unit.Move.IsMoving;
        public event Action<UnitCombat> AttackStarted;
        public event Action<UnitCombat,float> ProjectileReleased;
        public event Action<DamageResult> DamageDealt;
        public event Action<KillEvent> Killed;
        internal void NotifyDamageDealt(DamageResult result)
        {
            if(!isActiveAndEnabled || result.Context.Attacker.Unit!=unit) return;
            CombatEventDispatcher.Publish(DamageDealt,result);
            KillEvent kill=new KillEvent(result);
            if(isActiveAndEnabled && result.Context.Attacker.Unit==unit && kill.IsCredited)
                CombatEventDispatcher.Publish(Killed,kill);
        }
        private void Awake() { unit=GetComponent<Unit>(); health=GetComponent<UnitHealth>(); }
        private void OnEnable()
        {
            unit.Effects.Changed+=OnEffectsChanged;
            health.DamageTaken+=OnDamageTaken;
        }
        private void OnDamageTaken(int lostHp)
        {
            // 전투 중 생존한 유닛에 피격 1회당 마나 지급.
            if(lostHp>0 && CanFight) unit.Mana.Add(manaOnDamage);
        }
        private void OnEffectsChanged() { if(unit.Effects.IsStunned) windupCancellation?.Cancel(); }
        private void OnDisable()
        {
            unit.Effects.Changed-=OnEffectsChanged;
            health.DamageTaken-=OnDamageTaken;
            ResetForRound();
        }
        public void ResetForRound() { roundGeneration++; windupCancellation?.Cancel(); nextAttackTime=0f; IsAttacking=false; }
        public bool IsInAttackRange(UnitCombat target)=>target!=null && target!=this && target.unit.Team!=unit.Team
            && unit.CurrentCell.HasValue && target.unit.CurrentCell.HasValue
            && unit.CurrentCell.Value.DistanceTo(target.unit.CurrentCell.Value)<=unit.Stats.AttackRange;
        public async UniTask AttackAsync(UnitCombat target,CancellationToken token)
        {
            if(!CanAttack || target==null || !target.CanFight || !IsInAttackRange(target)) return;
            int generation=roundGeneration;
            CombatUnitReference targetReference=new CombatUnitReference(target.unit);
            IsAttacking=true; nextAttackTime=Time.time+unit.Stats.AttackCooldown; unit.Mana.Add(10);
            using CancellationTokenSource windup=CancellationTokenSource.CreateLinkedTokenSource(token,this.GetCancellationTokenOnDestroy());
            windupCancellation=windup;
            try
            {
                AttackStarted?.Invoke(target);
                await UniTask.Delay(TimeSpan.FromSeconds(unit.WindupSeconds),cancellationToken:windup.Token);
                if(ReferenceEquals(windupCancellation,windup)) windupCancellation=null;
                if(generation!=roundGeneration || !CanFight || unit.Effects.IsStunned || !targetReference.IsAlive || target==null || !target.CanFight || !IsInAttackRange(target)) return;
                int attack=unit.Stats.AttackDamage;
                float multiplier=unit.StatModifiers.BasicAttackPower;
                bool critical=UnityEngine.Random.value<unit.StatModifiers.CritChance;
                if(critical)
                    multiplier*=unit.StatModifiers.CritMultiplier;
                DamageContext context=DamageContext.BasicAttack(unit,critical);
                // 원거리도 선딜 뒤 논리 투사체의 도착 시간에 피해 적용.
                if(unit.Definition!=null && unit.Definition.IsRanged)
                {
                    float seconds=unit.CurrentCell.Value.DistanceTo(target.unit.CurrentCell.Value)/unit.ProjectileSpeed;
                    CombatEventDispatcher.Publish(ProjectileReleased,target,seconds);
                    await UniTask.Delay(TimeSpan.FromSeconds(seconds),cancellationToken:token);
                }
                if(generation==roundGeneration && CanFight && targetReference.IsAlive && target!=null && target.CanFight)
                    target.ReceiveAttack(context,attack,multiplier);
            }
            catch(OperationCanceledException) { }
            finally
            {
                if(ReferenceEquals(windupCancellation,windup)) windupCancellation=null;
                if(generation==roundGeneration) IsAttacking=false;
            }
        }
        public int ReceiveAttack(int attackDamage,float multiplier=1f)=>CanFight?health.TakeDamage(attackDamage,multiplier):0;
        public int ReceiveAttack(DamageContext context,int attackDamage,float multiplier=1f)=>CanFight?health.TakeDamage(context,attackDamage,multiplier):0;
    }
}
