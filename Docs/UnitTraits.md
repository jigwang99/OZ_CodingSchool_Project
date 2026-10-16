# 시너지 집계와 자동 적용

기획서의 계열 4종·직업 4종 연결. 수치는 `Docs/Sheets/Traits.csv`와 스키마 3의 `GameSnapshot.traits`에서 조회.

## 집계

- 팀별로 전장에 배치된 서로 다른 `unit_id`만 집계. 대기석은 제외.
- 동일 종류를 여러 기 배치하거나 성급이 달라도 종류 수는 한 번만 증가.
- 중복 종류의 유닛도 해당 시너지 적용 대상이면 각각 효과 수신.
- 계열과 직업을 독립 집계. 같은 그룹에 여러 단계가 있으면 충족한 최고 단계만 활성.
- 준비 중 배치·판매·구매·합성·새 라운드 진입 시 `RunController.AllySynergies`와 `EnemySynergies` 갱신.
- 전투 시작 시 스냅샷 고정. 전투 중 이동·사망으로 재집계하지 않음.

## 자동 적용

| ID | 시너지 | 서로 다른 2종 배치 시 효과 |
|---|---|---|
| 4001 | 인간 | 해당 인간에게 최대 체력 15% 보호막, 5초 |
| 4002 | 요정 | 해당 요정 시작 마나 +20, 최대 100 |
| 4003 | 언데드 | 해당 언데드가 적 처치 시 최대 체력 15% 회복 |
| 4004 | 오크 | 체력 50% 이하에서 공격력 +25%, 회복으로 기준 초과 시 해제 |
| 4101 | 검사 | 해당 검사 기본 공격 위력 +15%, 스킬 제외 |
| 4102 | 마법사 | 해당 마법사 스킬 피해·회복·보호막 위력 +20% |
| 4103 | 궁수 | 해당 궁수 공격 속도 +20% |
| 4104 | 암살자 | 해당 암살자 치명타 확률 +20%p, 배율 +0.3 |

`TraitBattleSession`은 전투 시작 후 상시 능력치와 시작 마나를 먼저 확정하고 시작 보호막을 적용.
시작 마나는 `ModifierSource`별로 합산하므로 향후 강화의 마나 보너스와 함께 계산 가능.
언데드는 `UnitCombat.Killed`를 구독하며 살아 있는 원래 처치자만 회복. 오크는 체력 변경 이벤트로 갱신.
전투 종료·취소·씬 종료에서 구독과 시너지 출처를 제거. 다른 스킬·강화 출처는 유지.
풀 재사용은 `CombatUnitReference`로 구분하여 이전 전투 정리가 새 유닛에 영향을 주지 않도록 처리.

## 데이터

`trait_id,name,category,group_id,required_count,effect,value,secondary_value,duration,threshold`

category는 Origin/Class, group_id는 기존 계열·직업 ID 1~4. value는 효과별 비율 또는 시작 마나 정수.
Critical의 secondary_value는 치명타 배율 증가량, LowHealthAttack의 threshold는 체력 발동 비율.
ShieldAtStart의 duration은 초 단위. 문자열은 CSV 입력 경계에서 Enum으로 변환하고 런타임은 정수 ID·Enum 사용.
Importer는 시너지 ID·그룹·단계 중복·수치 범위를 기존 전체 표 적용 과정에서 확인.

현재 로컬 Manifest는 schema_version=3, data_version=2026-10-15-traits-initial.
기존 구글 시트에 Traits 탭을 추가하고 Traits.csv를 가져온 뒤 Manifest.csv의 두 값을 함께 반영하면 Sheets Import로 갱신 가능.
기존 다른 탭의 수치를 덮어쓸 필요 없음. 이전 스키마 1·2는 기존 동작과 호환되며 시너지 정의 없이 실행.
시너지 UI는 `SynergyPanel`에 연결. 암살자는 시너지 활성 여부와 무관하게 전투 시작 후방 도약 사용. 상세 규칙은 `Docs/AssassinEntry.md` 참고.

## 검사

`PixelTactics > Checks > Run Trait Checks`: 집계·중복 종류·최고 단계·구버전·CSV 오류 및 실제 8종 효과·처치·회복·풀 재사용 확인.
기존 전체 Play 검사에도 시너지 검사를 연결.
