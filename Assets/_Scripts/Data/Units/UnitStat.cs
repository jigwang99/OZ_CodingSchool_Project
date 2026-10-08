using System;
using UnityEngine;

namespace AutoBattler.Data.Units
{
    [Serializable]
    public sealed class UnitStat
    {
        [Header("기물 정보")]
        [SerializeField] private UnitType unitType;
        [SerializeField] private Sprite icon;

        [Header("전투 스탯")]
        [SerializeField, Min(1)] private int maxHp = 100;
        [SerializeField, Min(0)] private int attackDamage = 20;
        [SerializeField, Min(0)] private int defense = 5;
        [SerializeField, Min(0f)] private float moveSpeed = 2f;
        [Tooltip("공격 가능한 육각 셀 거리")]
        [SerializeField, Min(1)] private int attackRange = 1;
        [SerializeField, Min(0.01f)] private float attackCooldown = 1f;

        public UnitType UnitType => unitType;
        public Sprite Icon => icon;
        public int MaxHp => maxHp;
        public int AttackDamage => attackDamage;
        public int Defense => defense;
        public float MoveSpeed => moveSpeed;
        public int AttackRange => attackRange;
        public float AttackCooldown => attackCooldown;

        public UnitStat(UnitType unitType, int maxHp, int attackDamage, int defense,
            float moveSpeed, int attackRange, float attackCooldown, Sprite icon = null)
        {
            this.unitType = unitType;
            this.maxHp = maxHp;
            this.attackDamage = attackDamage;
            this.defense = defense;
            this.moveSpeed = moveSpeed;
            this.attackRange = attackRange;
            this.attackCooldown = attackCooldown;
            this.icon = icon;
        }

        /// <summary>원본 에셋과 기물별 스탯을 분리하는 복사본 반환.</summary>
        public UnitStat Clone() => new UnitStat(unitType, maxHp, attackDamage, defense,
            moveSpeed, attackRange, attackCooldown, icon);
    }
}
