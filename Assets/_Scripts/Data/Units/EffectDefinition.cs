using System;
using AutoBattler.Battle.Units;
using UnityEngine;

namespace AutoBattler.Data.Units
{
    public enum EffectKind { Shield, Heal, Stun, Slow, StatModifier }
    public enum EffectStacking { Stack, Refresh }

    /// <summary>효과 정의 데이터. 양수 정의 ID를 사용하고 실행 중 상태는 별도로 보관.</summary>
    [Serializable]
    public sealed class EffectDefinition
    {
        [SerializeField] private int id;
        [SerializeField] private EffectKind kind;
        [SerializeField] private float value;
        [Tooltip("초 단위. 음수는 만료 없음, 0은 즉시 효과.")]
        [SerializeField] private float duration;
        [SerializeField] private EffectStacking stacking;
        [SerializeField] private StatType stat;
        [SerializeField] private ModifierOperation operation;
        public int Id=>id;
        public EffectKind Kind=>kind;
        public float Value=>value;
        public float Duration=>duration;
        public EffectStacking Stacking=>stacking;
        public StatType Stat=>stat;
        public ModifierOperation Operation=>operation;
        public EffectDefinition(int id,EffectKind kind,float value,float duration=0f,
            EffectStacking stacking=EffectStacking.Refresh,StatType stat=StatType.AttackDamage,ModifierOperation operation=ModifierOperation.Flat)
        {
            this.id=id; this.kind=kind; this.value=value; this.duration=duration;
            this.stacking=stacking; this.stat=stat; this.operation=operation;
        }
    }
}
