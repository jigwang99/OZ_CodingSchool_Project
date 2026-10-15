using System;
using AutoBattler.Battle.Units;
using UnityEngine;

namespace AutoBattler.Battle.Combat
{
    public enum DamageKind { Environment, BasicAttack, Skill, Effect }

    /// <summary>발생 당시 유닛 정보. 풀 재사용·새 라운드 이후에는 현재 유닛 참조와 구별.</summary>
    public readonly struct CombatUnitReference
    {
        private readonly Unit unit;
        public int InstanceId { get; }
        public int SpawnVersion { get; }
        public int LifeVersion { get; }
        public int DefinitionId { get; }
        public int OriginId { get; }
        public int ClassId { get; }
        public UnitTeam Team { get; }
        public bool HasIdentity => InstanceId > 0;
        public bool IsCurrent => unit != null && unit.InstanceId == InstanceId
            && unit.SpawnVersion == SpawnVersion && unit.Health != null && unit.Health.LifeVersion == LifeVersion;
        public Unit Unit => IsCurrent ? unit : null;
        public bool IsAlive => IsCurrent && unit.isActiveAndEnabled && unit.Health.IsAlive;

        public CombatUnitReference(Unit value)
        {
            unit=value;
            InstanceId=value!=null?value.InstanceId:0;
            SpawnVersion=value!=null?value.SpawnVersion:0;
            LifeVersion=value!=null && value.Health!=null?value.Health.LifeVersion:0;
            DefinitionId=value!=null && value.Definition!=null?value.Definition.Id:0;
            OriginId=value!=null && value.Definition!=null?value.Definition.OriginId:0;
            ClassId=value!=null && value.Definition!=null?value.Definition.ClassId:0;
            Team=value!=null?value.Team:default;
        }
    }

    /// <summary>피해를 발사할 때 고정한 출처. SourceId는 스킬·효과 정의 ID, 기본 공격·환경은 0.</summary>
    public readonly struct DamageContext
    {
        public CombatUnitReference Attacker { get; }
        public DamageKind Kind { get; }
        public int SourceId { get; }
        public bool IsCritical { get; }
        private DamageContext(Unit attacker,DamageKind kind,int sourceId,bool critical=false)
        { Attacker=new CombatUnitReference(attacker); Kind=kind; SourceId=sourceId; IsCritical=critical; }
        public static DamageContext Environment => default;
        public static DamageContext BasicAttack(Unit attacker,bool critical=false) => new DamageContext(attacker,DamageKind.BasicAttack,0,critical);
        public static DamageContext Skill(Unit attacker,int skillId) => new DamageContext(attacker,DamageKind.Skill,skillId);
        public static DamageContext Effect(Unit attacker,int effectId) => new DamageContext(attacker,DamageKind.Effect,effectId);
    }

    /// <summary>한 번 확정된 피해 결과. 방어 후 피해를 보호막·실제 체력 손실·초과 피해로 구분.</summary>
    public readonly struct DamageResult
    {
        public DamageContext Context { get; }
        public CombatUnitReference Target { get; }
        public int ResolvedDamage { get; }
        public int ShieldDamage { get; }
        public int HealthDamage { get; }
        public int OverkillDamage => ResolvedDamage-ShieldDamage-HealthDamage;
        public bool IsLethal { get; }
        internal DamageResult(DamageContext context,CombatUnitReference target,int resolved,int shield,int health,bool lethal)
        { Context=context; Target=target; ResolvedDamage=resolved; ShieldDamage=shield; HealthDamage=health; IsLethal=lethal; }
    }

    /// <summary>체력을 0으로 만든 피해에만 귀속. 이전 피격 출처로 처치자를 추정하지 않음.</summary>
    public readonly struct KillEvent
    {
        public DamageResult Damage { get; }
        public CombatUnitReference Killer => Damage.Context.Attacker;
        public CombatUnitReference Victim => Damage.Target;
        public bool IsCredited => Damage.IsLethal && Killer.HasIdentity && Victim.HasIdentity && Killer.Team!=Victim.Team;
        internal KillEvent(DamageResult damage) { Damage=damage; }
    }

    /// <summary>표현·확장 기능의 이벤트 예외가 피해 확정과 다른 구독자의 사망 처리를 중단하지 않도록 분리.</summary>
    internal static class CombatEventDispatcher
    {
        public static void Publish<T>(Action<T> listeners,T value)
        {
            if(listeners==null) return;
            foreach(Action<T> listener in listeners.GetInvocationList())
                try { listener(value); } catch(Exception exception) { Debug.LogException(exception); }
        }
        public static void Publish<T1,T2>(Action<T1,T2> listeners,T1 first,T2 second)
        {
            if(listeners==null) return;
            foreach(Action<T1,T2> listener in listeners.GetInvocationList())
                try { listener(first,second); } catch(Exception exception) { Debug.LogException(exception); }
        }
        public static void Publish(Action listeners)
        {
            if(listeners==null) return;
            foreach(Action listener in listeners.GetInvocationList())
                try { listener(); } catch(Exception exception) { Debug.LogException(exception); }
        }
    }
}
