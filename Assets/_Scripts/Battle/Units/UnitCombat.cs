using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    /// <summary>공격 선딜, 투사체 도착, 쿨다운과 기본 공격 피해 담당.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Unit),typeof(UnitHealth))]
    public sealed class UnitCombat : MonoBehaviour
    {
        private Unit unit;
        private UnitHealth health;
        private float nextAttackTime;
        private int roundGeneration;
        public bool IsAttacking { get; private set; }
        public int Mana { get; private set; }
        public bool CanFight=>isActiveAndEnabled && unit!=null && unit.IsInCombat && health!=null && health.IsAlive;
        public bool CanAttack=>CanFight && !IsAttacking && Time.time>=nextAttackTime && !unit.Move.IsMoving;
        public event Action<UnitCombat> AttackStarted;
        private void Awake() { unit=GetComponent<Unit>(); health=GetComponent<UnitHealth>(); }
        public void ResetForRound() { roundGeneration++; nextAttackTime=0f; Mana=0; IsAttacking=false; }
        public bool IsInAttackRange(UnitCombat target)=>target!=null && target!=this && target.unit.Team!=unit.Team
            && unit.CurrentCell.HasValue && target.unit.CurrentCell.HasValue
            && unit.CurrentCell.Value.DistanceTo(target.unit.CurrentCell.Value)<=unit.Stats.AttackRange;
        public bool TryAttack(UnitCombat target)
        {
            if(!CanAttack || target==null || !target.CanFight || !IsInAttackRange(target)) return false;
            AttackAsync(target,unit.BattleFlow.GetCancellationTokenOnDestroy()).Forget(); return true;
        }
        public async UniTask AttackAsync(UnitCombat target,CancellationToken token)
        {
            if(!CanAttack || target==null || !target.CanFight || !IsInAttackRange(target)) return;
            int generation=roundGeneration;
            IsAttacking=true; nextAttackTime=Time.time+unit.Stats.AttackCooldown; Mana=Math.Min(100,Mana+10);
            AttackStarted?.Invoke(target);
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(unit.WindupSeconds),cancellationToken:token);
                if(generation!=roundGeneration || !CanFight || target==null || !target.CanFight || !IsInAttackRange(target)) return;
                int attack=unit.Stats.AttackDamage;
                float multiplier=1f;
                if(unit.Definition!=null && UnityEngine.Random.value<unit.Definition.CritChance)
                    multiplier=unit.Definition.CritMultiplier;
                // 원거리도 선딜 뒤 논리 투사체의 도착 시간에 피해 적용.
                if(unit.Definition!=null && unit.Definition.IsRanged)
                {
                    float seconds=unit.CurrentCell.Value.DistanceTo(target.unit.CurrentCell.Value)/unit.ProjectileSpeed;
                    await UniTask.Delay(TimeSpan.FromSeconds(seconds),cancellationToken:token);
                }
                if(generation==roundGeneration && CanFight && target!=null && target.CanFight) target.ReceiveAttack(attack,multiplier);
            }
            catch(OperationCanceledException) { }
            finally { if(generation==roundGeneration) IsAttacking=false; }
        }
        public int ReceiveAttack(int attackDamage,float multiplier=1f)=>CanFight?health.TakeDamage(attackDamage,multiplier):0;
    }
}
