using System.Collections.Generic;
using AutoBattler.Data.Augments;
using AutoBattler.Data.Units;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    /// <summary>선택한 강화의 능력치·시작 효과 적용. 출처별 교체로 중복 누적 방지.</summary>
    public static class AugmentApplicator
    {
        public static void ApplyPersistent(Unit unit,IEnumerable<AugmentDefinition> augments)
        {
            if(unit.Team!=UnitTeam.Ally) return;
            foreach(var augment in augments)
            {
                if(augment.Effect==AugmentEffect.StatModifier)
                    unit.StatModifiers.ReplaceSource(augment.Source,new StatModifierSpec(augment.Stat,augment.Operation,augment.Value));
                else if(augment.Effect==AugmentEffect.StartingMana)
                    unit.Mana.SetStartingBonus(augment.Source,Mathf.FloorToInt(augment.Value));
            }
            // 새 유닛·합성 결과는 최종 최대 체력을 기준으로 준비 상태 복원.
            if(!unit.IsInCombat) { unit.Health.RestoreFullHealth(); unit.Mana.ResetForRound(); }
        }
        public static void ApplyBattleStart(IEnumerable<Unit> units,IEnumerable<AugmentDefinition> augments)
        {
            foreach(var unit in units)
                if(unit.Team==UnitTeam.Ally && unit.Health.IsAlive && unit.IsInCombat)
                    foreach(var augment in augments)
                        if(augment.Effect==AugmentEffect.StartingShield)
                            unit.Effects.Apply(new EffectDefinition(augment.Id,EffectKind.Shield,
                                Mathf.FloorToInt(unit.Health.MaxHp*augment.Value),augment.Duration),augment.Source);
        }
    }
}
