# 능력치 수정과 효과

`Unit.StatModifiers`는 기본 능력치와 수정자에서 최종 `Unit.Stats`를 계산한다. `Unit.Effects`는 적용 인스턴스, 시간, 보호막과 상태 효과를 관리한다. 숫자·효과 정의를 담는 추가 SO는 만들지 않는다.

## 능력치 수정

```csharp
var source = new ModifierSource(ModifierSourceKind.ClassTrait, 4001);

unit.StatModifiers.ReplaceSource(source,
    new StatModifierSpec(StatType.AttackDamage, ModifierOperation.Percent, 0.25f),
    new StatModifierSpec(StatType.AttackSpeed, ModifierOperation.Percent, 0.20f));

unit.StatModifiers.RemoveSource(source);
```

`(기본값 + 고정 증가 합계) × (1 + 비율 증가 합계)`로 계산한다. `AttackSpeed`의 기본 계수는 1이며 최종 공격 간격은 기본 간격을 해당 계수로 나눈 값이다. 치명타 확률에 `Flat, 0.2f`를 추가하면 20%p 증가한다. `SkillPower`와 `BasicAttackPower`도 기본 계수 1이다. 기본 공격은 `BasicAttackPower`와 현재 치명타 능력치를 사용한다. `SkillPower`는 이후 스킬 실행기가 사용할 수치다.

`ReplaceSource`는 기존 출처의 수정자를 한 번에 교체한다. 최대 체력 수정자를 교체하는 중간 단계에 체력이 줄어들지 않는다. 개별 등록은 `AddModifier`의 반환 Handle로 해제한다. Handle은 풀 재사용 시 재발급하지 않는다.

## 효과 적용

```csharp
var source = new ModifierSource(ModifierSourceKind.Skill, 2001, caster.InstanceId);
var shield = new EffectDefinition(3001, EffectKind.Shield, 50f, 5f);
EffectHandle handle = target.Effects.Apply(shield, source);

var buff = new EffectDefinition(3002, EffectKind.StatModifier, 0.25f, 3f,
    stat: StatType.AttackDamage, operation: ModifierOperation.Percent);
target.Effects.Apply(buff, source);

target.Effects.Apply(new EffectDefinition(3003, EffectKind.Heal, 30f), source);
target.Effects.Apply(new EffectDefinition(3004, EffectKind.Stun, 1f, 1f), source);
target.Effects.Apply(new EffectDefinition(3005, EffectKind.Slow, 0.5f, 2f), source);

target.Effects.Remove(handle);
target.Effects.RemoveSource(source);
```

ID는 사용 예시다. 효과 정의 ID와 출처의 스킬·시너지·강화 ID, 시전자 인스턴스 ID는 서로 다른 의미다. 실제 스킬을 추가할 때 정의 데이터의 ID로 연결한다. `EffectDefinition`은 정의 데이터이며 남은 시간과 보호막은 `EffectInstance`가 보관한다. 현재 Google Sheets의 기존 8개 탭과 JSON 스키마는 유지하며, Effects 탭 가져오기는 이후 스킬 데이터 확장 단계에서 연결한다.

지속 시간이 양수이면 전투 시간에 따라 만료하고 음수이면 명시적으로 제거할 때까지 유지한다. 회복은 즉시 적용하고 활성 효과를 남기지 않는다. 다른 종류에서 지속 시간 0은 적용하지 않는다. `potency`는 스킬 실행기가 계산한 위력 계수를 전달하는 인수이며 자동으로 성급이나 시전자 능력치를 읽지 않는다.

`Refresh`는 같은 효과 ID와 출처를 갱신하며 남은 시간과 새 지속 시간 중 큰 값을 유지한다. 보호막은 현재 잔량과 새 보호막량 중 큰 값으로 갱신한다. 감속은 더 강한 값을 유지한다. 능력치 수정 효과는 새 값으로 교체한다. `Stack`은 별도 Handle로 중첩한다. 다른 출처의 기절과 감속은 각각 유지하며 기절은 하나라도 남으면 유지, 감속은 가장 강한 활성 값만 적용한다.

## 전투 연결과 정리

- 피해: 현재 방어력·피해 배율·방어 무시 계산 → 만료가 가까운 보호막부터 흡수 → 남은 피해를 체력에 적용.
- 회복: 살아 있는 대상만 회복하며 최대 체력 초과와 일반 회복을 통한 부활은 차단.
- 최대 체력: 전투 중 증가는 현재 체력을 늘리지 않고 감소는 현재 체력을 제한. 준비 복원과 전투 시작에서 완전 회복.
- 기절: 이동과 기본 공격 시작 차단, 진행 중 이동과 공격 선딜 취소. 공격 시작으로 받은 마나는 유지. 기절한 대상도 피해를 받음.
- 감속: `최종 이동 속도 × (1 - 가장 강한 감속 비율)`로 이동 진행률 계산. 100% 감속은 이동 취소.
- 효과 시간: 전투 중 `Time.deltaTime`으로 갱신. UI 애니메이션 시간과 분리.
- 사망·결과/준비 전환·비활성화: 활성 효과와 해당 효과가 소유한 수정자를 제거.
- 풀 반환·재설정: 직접 등록한 수정자도 초기화. 시너지·강화 등 외부 시스템이 직접 등록한 수정자는 해당 시스템에서 `RemoveSource` 또는 `ReplaceSource`로 관리.

## 검사

`PixelTactics > Checks > Run Effects Domain Checks`에서 계산과 중첩·갱신·만료를 검사한다. 기존 Play 검사에는 실제 프리팹의 방어·보호막·회복·기절·감속·사망·단계 전환·풀 재사용 검사가 포함된다. 검사는 분리된 프로젝트에서 실행한다.
