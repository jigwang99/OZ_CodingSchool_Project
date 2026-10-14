# 유닛 행동트리

`AutoBattleController`에서 각 유닛의 `UnitBehaviourTree`를 생성하고 50ms 간격으로 ID 순서대로 실행.
공유 `CombatBoard`는 점유·예약 관리, 유닛별 `UnitBlackboard`는 대상·경로·재시도 시간 보관.
기존 `UnitMove`, `UnitCombat`, `UnitEffects`는 실제 행동 담당.

```text
Selector: 우선 성공 또는 실행 중인 분기 선택
├─ Sequence: 사망 확인 → 점유 해제 (한 번)
├─ Sequence: 기절 / 이동 중 / 공격 중 / 재시도 대기 → 대기
└─ Sequence: 대상 확보
   └─ Selector
      ├─ Sequence: 공격 사거리 안 → 기본 공격 또는 쿨다운 대기
      └─ 사거리까지 이동 (A* 경로, 다음 셀 예약)
```

## 노드 결과

- `Success`: 조건 통과 또는 처리 완료.
- `Failure`: 조건 불충족. Selector에서 다음 후보 선택.
- `Running`: 행동 진행 또는 대기. 해당 Tick의 하위 후보 실행 중단.

Sequence는 모든 자식이 성공하면 성공, Selector는 첫 성공·실행 중인 자식 선택.
매 Tick 첫 자식부터 다시 확인하므로 기절과 사망 조건이 하위 행동보다 우선.
이미 시작된 비동기 행동의 중단은 기존 UnitMove·UnitCombat의 기절·사망 처리와 전투 CancellationToken에서 담당.
Running 자체가 비동기 작업을 취소하지는 않음. 새 행동 추가 시 같은 수명 규칙 연결 필요.

## 대상과 이동

거리 → 역할 → 인스턴스 ID 순서로 공격 가능하거나 경로가 있는 적 선택.
선택한 대상 유지, 사망 또는 1초 이상 이동 실패 시 재선택.
대상·경로가 없으면 0.2초 후 재시도. 이동 도착 시 점유 이전, 취소 시 예약 해제.
전투 종료·제한 시간·승패 판단은 AutoBattleController 담당.

## 확장

코드로 구성한 행동트리이며 별도 시각 편집기나 외부 패키지 불필요.
스킬 시스템 추가 시 기본 공격 분기 앞에 `Sequence(사용 가능 조건, 스킬 실행)` 삽입 가능.
치유 대상 탐색·암살자 이동은 별도 노드와 필요한 블랙보드 필드로 확장 가능.
현재 구현 범위는 기존 기본 공격·이동 판단 전환이며, 스킬이나 직업별 전용 행동은 추후 구현 대상.

## 검사

Unity 메뉴 `PixelTactics/Checks/Run Behaviour Tree Domain Checks`: 조건·복합 노드 검사.
`BehaviourTreeChecks.BuildAndPlay`: 기존 도메인·프리팹·Addressables 검사와 실제 플레이 검사 실행.
실제 프리팹으로 대상 우선순위, 공격·이동 중복 방지, 기절 우선 처리, 대상 사망,
사망 점유 해제, 재시도 대기, 이동 예약·도착·취소 검사.
