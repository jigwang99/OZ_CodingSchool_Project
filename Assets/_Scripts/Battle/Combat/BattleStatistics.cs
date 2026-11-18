using System;
using System.Collections.Generic;
using System.Linq;
using AutoBattler.Battle.Units;

namespace AutoBattler.Battle.Combat
{
    /// <summary>전투 이벤트로 실제 피해·회복 집계. 전투 외 복원과 풀 재사용의 이전 사건은 제외.</summary>
    public sealed class BattleStatistics : IDisposable
    {
        private readonly List<UnitHealth> subscriptions = new List<UnitHealth>();
        private readonly Dictionary<(UnitTeam, int, int, int), UnitBattleRecord> records = new Dictionary<(UnitTeam, int, int, int), UnitBattleRecord>();
        public BattleReport Current { get; private set; }
        public bool IsRecording => Current != null;
        public event Action Changed;
        private static (UnitTeam, int, int, int) Key(CombatUnitReference reference)
            => (reference.Team, reference.InstanceId, reference.SpawnVersion, reference.LifeVersion);

        public void Begin(int round, IEnumerable<Unit> participants)
        {
            Dispose();
            foreach (Unit unit in participants.Where(unit => unit != null && unit.isActiveAndEnabled).Distinct())
            {
                if (unit.Team == UnitTeam.Ally) records.Add(Key(new CombatUnitReference(unit)), new UnitBattleRecord(unit));
                subscriptions.Add(unit.Health);
                unit.Health.DamageResolved += OnDamage;
                unit.Health.HealingResolved += OnHealing;
            }
            Current = new BattleReport(round, records.Values.OrderBy(record => record.InstanceId).ToArray());
            Changed?.Invoke();
        }
        private void OnDamage(DamageResult result)
        {
            if (!IsRecording) return;
            long amount = (long)result.HealthDamage + result.ShieldDamage;
            if (amount <= 0) return;
            if (records.TryGetValue(Key(result.Target), out UnitBattleRecord victim)) victim.AddDamageTaken(amount);
            if (result.Context.Attacker.HasIdentity && result.Context.Attacker.Team != result.Target.Team
                && records.TryGetValue(Key(result.Context.Attacker), out UnitBattleRecord attacker)) attacker.AddDamageDealt(amount);
            Changed?.Invoke();
        }
        private void OnHealing(HealingResult result)
        {
            if (!IsRecording || result.Amount <= 0 || result.Healer.Team != result.Target.Team) return;
            if (!records.TryGetValue(Key(result.Healer), out UnitBattleRecord healer)) return;
            healer.AddHealing(result.Amount);
            Changed?.Invoke();
        }
        public BattleReport Complete(bool won)
        {
            BattleReport result = Current;
            result?.Complete(won);
            Dispose();
            return result;
        }
        public void Dispose()
        {
            foreach (UnitHealth health in subscriptions)
                if (health != null) { health.DamageResolved -= OnDamage; health.HealingResolved -= OnHealing; }
            subscriptions.Clear(); records.Clear(); Current = null;
            Changed?.Invoke();
        }
    }
}
