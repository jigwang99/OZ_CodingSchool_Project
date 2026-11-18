using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AutoBattler.Battle.Units;
using UnityEngine;

namespace AutoBattler.Battle.Combat
{
    public enum BattleStatisticKind { DamageDealt, DamageTaken, HealingDone }

    /// <summary>전투에 참가했던 기물의 표시 정보와 실제 실적. 풀 오브젝트 참조는 저장하지 않음.</summary>
    [Serializable]
    public sealed class UnitBattleRecord
    {
        [SerializeField] private int instanceId, definitionId, visualId, iconId, star;
        [SerializeField] private string unitName;
        [SerializeField] private long damageDealt, damageTaken, healingDone;
        public int InstanceId => instanceId;
        public int DefinitionId => definitionId;
        public int VisualId => visualId;
        public int IconId => iconId;
        public int Star => star;
        public string UnitName => unitName;
        public long DamageDealt => damageDealt;
        public long DamageTaken => damageTaken;
        public long HealingDone => healingDone;
        internal UnitBattleRecord(Unit unit)
        {
            instanceId = unit.InstanceId; definitionId = unit.Definition.Id;
            visualId = unit.Definition.VisualId; iconId = unit.Definition.IconId;
            star = unit.Star; unitName = unit.Definition.Name;
        }
        public long Value(BattleStatisticKind kind) => kind switch
        {
            BattleStatisticKind.DamageTaken => damageTaken,
            BattleStatisticKind.HealingDone => healingDone,
            _ => damageDealt
        };
        internal void AddDamageDealt(long amount) => damageDealt += amount;
        internal void AddDamageTaken(long amount) => damageTaken += amount;
        internal void AddHealing(long amount) => healingDone += amount;
    }

    /// <summary>현재 또는 직전 전투의 아군 기록. 완료된 기록은 다음 전투와 독립적으로 보관.</summary>
    [Serializable]
    public sealed class BattleReport
    {
        [SerializeField] private int round;
        [SerializeField] private bool isComplete, won;
        [SerializeField] private UnitBattleRecord[] units;
        private ReadOnlyCollection<UnitBattleRecord> view;
        public int Round => round;
        public bool IsComplete => isComplete;
        public bool Won => won;
        public IReadOnlyList<UnitBattleRecord> Units => view ?? (view = Array.AsReadOnly(units ?? Array.Empty<UnitBattleRecord>()));
        internal BattleReport(int round, UnitBattleRecord[] units)
        { this.round = round; this.units = units; }
        internal void Complete(bool victory) { won = victory; isComplete = true; }
    }
}
