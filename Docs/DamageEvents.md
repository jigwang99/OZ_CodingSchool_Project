# 피해 출처와 처치 이벤트

`DamageContext`는 피해 발사 시 공격자 인스턴스·정의·계열·직업·팀·수명, 피해 종류, 스킬/효과 ID와 치명타 여부를 고정.
종류는 `Environment`, `BasicAttack`, `Skill`, `Effect`. 출처 없는 기존 `TakeDamage` 호출은 환경 피해로 처리.
현재 기본 공격과 고유 스킬 실행 경로에 출처 전달. `Effect`는 향후 지속 피해 등에서 사용할 명시적 입력 경로이며 지속 피해 기능 자체를 추가한 것은 아님.

## 피해 적용

방어·피해 배율 계산 → 보호막 흡수 → 실제 HP 감소 → 피해 결과 고정 → 이벤트 발행.

`DamageResult`의 `ResolvedDamage`는 방어 후 피해, `ShieldDamage`는 보호막 손실, `HealthDamage`는 실제 HP 손실, `OverkillDamage`는 남은 체력을 초과한 피해.
보호막만 감소해도 상세 피해 이벤트 발생. 피해 0·사망 대상 재타격·회복에는 피해 이벤트 없음.
기존 `DamageTaken(int)`는 HP가 실제 감소했을 때만 유지하므로 보호막만 맞은 경우 피격 MP를 지급하지 않음.
스탯과 치명타 계산은 기존 경로 유지. 기본 공격의 정수 계산과 스킬의 소수점 계산을 구분하고 방어 계산은 한 번만 적용.

## 구독 위치와 순서

| 위치 | 이벤트 | 용도 |
|---|---|---|
| 피해자 `UnitHealth` | `DamageTaken` | 기존 실제 HP 손실, 피격 MP |
| 피해자 `UnitHealth` | `DamageResolved` | 공격자·피격자·피해 종류·보호막/HP 결과 |
| 피해자 `UnitHealth` | `Died` | 기존 사망 표현·시전 취소·효과 정리 |
| 피해자 `UnitHealth` | `DeathResolved` | 출처 없는 사망까지 포함한 원인 기록 |
| 공격자 `UnitCombat` | `DamageDealt` | 해당 공격자가 가한 유효 피해 |
| 공격자 `UnitCombat` | `Killed` | 적 처치에 따른 시너지·보상 확장 |

순서는 HP 변경 → DamageTaken → DamageResolved → Died → DeathResolved → DamageDealt → Killed.
해당하지 않는 이벤트는 생략. 사망은 이미 HP 0으로 확정된 뒤 발행하므로 치명 피해로 MP를 회복하지 않음.
피해 처리 중 같은 대상에 재진입한 피해는 기존 정책대로 무시.
구독자 예외는 Console에 기록하고 나머지 구독자와 사망 처리는 계속 진행.

## 처치 귀속

- HP를 0으로 만든 마지막 피해의 출처에만 귀속. 이전 공격자나 누적 피해량으로 추정하지 않음.
- 같은 사망에 처치 이벤트 한 번만 발생. 이후 시체를 타격해도 추가 지급 없음.
- 자기 피해·같은 팀 피해·환경 피해에는 공격자의 `Killed` 이벤트 없음. 피해자의 `DeathResolved`에서 원인 확인 가능.
- 이미 발사된 스킬의 시전자가 사망해도 발사 당시 출처 유지. 죽은 유닛의 처치도 기록하지만 `Heal`은 기존 생존 조건을 적용하므로 부활 없음.
- 기존 기본 공격의 선딜·공격자 생존 조건은 유지. 기본 투사체도 발사 전 대상의 수명을 기억하여 재사용된 대상에 오적중하지 않도록 연결.

## 풀 재사용과 라운드

`CombatUnitReference`는 인스턴스 ID, `Unit.SpawnVersion`, `UnitHealth.LifeVersion`으로 현재 수명 확인.
초기화·완전 복원·비활성화로 HP 수명이 변경되면 이전 참조 무효.
이전 출처의 메타데이터는 남지만 `Unit` 프로퍼티는 수명이 다르면 null. 풀에서 꺼낸 새 유닛에 이전 피해·처치 콜백을 보내지 않음.
피해자의 사망 콜백에서 풀 반환·재설정이 발생하면 새 생명에 후속 피해자 이벤트를 보내지 않음. 이미 고정된 공격자 처치 기록은 이전 피해자 ID를 유지.
`IsCredited`는 사건 당시 적 처치 귀속 여부, `IsAlive`는 지금 보상을 받을 수 있는 생존 여부이므로 구분 필요.

```csharp
// 구독과 해제는 소비자의 OnEnable/OnDisable에서 대칭 처리.
unit.Combat.Killed += OnKilled;

private void OnKilled(KillEvent info)
{
    if (!info.Killer.IsAlive)
        return;

    // 향후 활성 언데드 시너지 등을 확인한 뒤 회복량 결정.
    Unit killer = info.Killer.Unit;
    killer.Health.Heal(Mathf.FloorToInt(killer.Health.MaxHp * 0.15f));
}
```

위 코드는 사용 예시이며 언데드 시너지 효과를 자동 적용하지 않음.
`PixelTactics > Checks > Run Damage Event Checks`는 기존 스킬·풀·자동 전투 검사와 함께 실제 프리팹 출처·처치·재사용을 검사.
