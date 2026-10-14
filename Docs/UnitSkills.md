# 유닛 스킬 시전

기획서 8·9쪽의 준비 시 마나 소비, 기절 반환, 대상 재선정, 8종 스킬 규칙 연결.
정확한 수치가 미정인 피해·회복·보호막·준비 시간은 시트 샘플 초기값이며 플레이테스트로 조정 필요.

## 구성

- UnitMana: 획득·소비·반환. 기본 공격 시작 시 10, 전투 중 실제 체력 피해를 받고 생존하면 피격 1회당 10, 최대 100. 기본 시작 0, 보스 시작 100.
- 피격 마나: UnitHealth.DamageTaken → UnitCombat → UnitMana.Add 연결. 기본 공격과 스킬 피해 모두 적용. 보호막에 전부 흡수된 피해·0 피해·사망하는 피해에는 지급 없음. UnitCombat의 Mana On Damage에서 회복량 설정.
- UnitSkill: 준비·발동·쿨다운·취소 수명과 마나 반환 관리.
- SkillTargetSelector: 자기 자신, 현재 적, 최저 체력 비율 아군 선택. 동률은 ID 순서.
- SkillExecutor: 피해, 방어 일부 무시, 육각 범위, 논리 투사체, UnitEffects 적용.
- SkillDefinition: 시트에서 생성하는 불변 데이터.
- UnitSkillView: PIXEM 애니메이션 해시와 시전 방향 연결.

```text
행동트리
  사망 → 점유 정리
  기절 / 이동 중 / 공격 중 / 시전 중 → 대기
  스킬 사용 조건과 대상 충족 → 시전
  기본 공격 재시도 대기 → 대기
  적 확보 → 기본 공격 또는 이동
```

기본 공격 또는 피격으로 마나가 100이 되면 진행 중 공격을 완료·중단한 뒤 스킬 우선 판단.
치유·자기 보호막은 기본 공격 대상 없이도 시전 가능. 모두 최대 체력인 아군만 있으면 치유 보류·마나 유지.

## 준비와 취소

준비 시작에서 실제 마나 차감량 기록 → 준비 시간 대기 → 대상 재확인 → 발동 → 쿨다운 시작.
준비 중 기절은 취소 후 실제 차감량을 한 번 반환. 다음 시전은 준비 시간부터 재시작.
준비 중 대상 사망·거리 이탈·풀 재사용 시 유효 대상 재선정. 후보가 없으면 취소·마나 반환.
시전자 사망, 전투 종료, 풀 반환은 반환 없음. 발동 이후 기절·사망은 이미 발사한 효과를 되돌리지 않음.
전투 종료·풀 반환 시 진행 중 작업 취소. 이전 시전의 finally가 새 시전 상태를 덮어쓰지 않도록 작업 참조 구별.

투사체는 셀 거리와 속도로 비행 시간을 진행. 생존 대상 셀을 추적하고 대상 소멸 시 마지막 셀로 계속 비행.
단일 공격은 원래 대상이 살아 있고 같은 SpawnVersion인 경우에만 명중.
범위 공격은 도착 셀 주변의 생존 적을 다시 수집. 타깃이 사망해도 범위 효과 적용.
현재 투사체는 논리 처리이며 별도 비행 프리팹·타격 VFX·사운드 에셋은 연결하지 않음.
CastStarted·EffectReleased·CastFinished 이벤트로 연출 확장 가능. 시각 오브젝트는 공통 풀로 대여·반환하도록 연결.

## 8종 연결과 초기값

| 유닛 | 스킬 ID | 초기 효과 |
|---|---|---|
| Shield | 2001 | 보호막 80, 5초 |
| Scout | 2002 | 공격력 2배, 강화 화살 |
| Healer | 2003 | 체력 90 회복 |
| Crossbow | 2004 | 공격력 1.8배, 방어력 50% 무시 |
| Warrior | 2005 | 공격력 1.5배, 자신 중심 육각 반경 1 |
| Shadow | 2006 | 공격력 2.5배, 단일 피해 |
| Pyromancer | 2007 | 고정 30 + 공격력 1.2배, 대상 중심 반경 1 |
| Tracker | 2008 | 공격력 1.8배 + 1.5초 기절 |

기본 마나 비용 100, 준비 시간 0.3~0.5초, 투사체 8칸/초. Skills.csv에서 개별 수정.
고정 피해·보호막·회복은 2성일 때 1.8배. 공격력 계수 부분은 성장한 공격력을 사용하며 계수를 다시 증폭하지 않음.
SkillPower는 피해·보호막·회복에 적용. 기본 공격 전용 배율·치명타는 스킬에 적용하지 않음.
피해에 방어력은 UnitHealth에서 한 번 적용하고, 보호막 흡수·사망 처리를 기존 모듈에 위임.

## 시트 가져오기

스킬 사용 데이터는 Manifest schema_version=2, 총 10개 표 사용.
기존 8개 표에 Skills·Effects 탭 추가. Unit의 skill_id에 2001~2008 연결.
샘플 위치 Docs/Sheets/Skills.csv·Effects.csv·Units.csv·Manifest.csv.

Skills: skill_id, name, target_rule, delivery, animation, mana_cost, cast_range, windup, cooldown,
radius, center_on_caster, base_damage, attack_ratio, armor_ignore, effect_id, projectile_speed.
Effects: effect_id, kind, value, duration, stacking, stat, operation.
대상 규칙 Self·CurrentEnemy·LowestHealthAlly, 전달 Instant·Projectile, 애니메이션 Slash·Shoot·Magic.
Enum 이름은 외부 CSV 경계에서만 파싱하고 실행 코드는 Enum·정수 ID·직접 참조 사용.

Google Sheets 가져오기는 기본 표의 Manifest를 읽고 스키마 2일 때 Skills·Effects를 추가 요청.
schema_version=1·skill_id=0인 기존 8개 표도 사용 가능하며 스킬 없이 기존 행동 유지.
스킬을 사용하려면 새 10개 표 샘플을 적용해야 함. 시트 가져오기는 로컬 스냅샷 교체이며 외부 시트 업로드는 수행하지 않음.
JSON은 스키마 2 초기 데이터로 교체. 원본 프로젝트에서 Local Addressables를 다시 빌드한 뒤 사용·배포.

## 확인

SkillChecks.BuildAndPlay에서 시트 호환·참조, 8종 스킬, 준비 행동 잠금, 기절 중복 반환 방지,
대상 소멸·재선정, 투사체 대상 재사용, 2성 보호막, 시전자 사망, 준비 중 풀 반환 검사.
기존 공통 풀·효과·방향·드래그·자동 전투·정산·다음 준비 검사도 함께 실행.
