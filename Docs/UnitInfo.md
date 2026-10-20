# 유닛 정보 UI

`BattleHUD`의 오른쪽 빈 영역에 `UnitInfoPanel` 프리팹을 연결. 초기 상태는 비활성화.

- 아군·적군 클릭: 이름, 성급, 비용, 종족·직업, 체력·마나, 현재 능력치, 스킬 정보 표시.
- 빈 보드 클릭: 선택 해제와 패널 숨김. 정보 패널이나 다른 UI 클릭은 보드 선택에 전달하지 않음.
- 준비 단계의 아군: 기존 드래그·교환·판매 유지. 적군은 정보 확인만 허용.
- 전투 중: 아군·적군 선택과 현재 수치 확인만 허용. 배치 변경·판매 불가.
- 증강 모달이 열린 동안: 정보 숨김과 유닛 선택 차단. 창을 닫으면 기존 배치 조작과 조회 허용.
- 판매·풀 반환: 선택 무효화. 같은 GameObject를 재대여해도 이전 선택을 이어받지 않음.

`UnitDragController.InspectedUnit`은 조회 대상, 기존 `SelectedUnit`은 아군 판매·배치 대상. `BattleFlowController.CanInspectUnits`와 `CanPlaceUnits`로 두 권한 분리.

`RunHUD`가 선택을 연결하고 `UnitInfoPanel`이 `UnitHealth`, `UnitMana`, `UnitStats`, `UnitEffects` 이벤트를 구독. 선택 변경·숨김 시 구독과 DOTween 정리. 표시 수치는 시너지·증강·효과를 반영한 현재 값. 스킬 피해는 현재 성급·공격력·스킬 위력을 반영한 방어 적용 전 수치.

공용 HUD Canvas 사용. 기준 해상도 1920×1080, 오른쪽 위에서 (-24, -136), 420×740. TextMeshProUGUI 사용. 일반 UI 문구는 영어, 시트의 한글 스킬명은 원문 표시. 셋업 SO의 원본 폰트 참조로 생성한 `UnitInfoKoreanFont` 사용. 원본은 프로젝트에 포함된 Pixem의 Mabinogi Classic 폰트.

에디터 연결: `PixelTactics/Setup/Connect Unit Info UI`.
통합 검사: `PixelTactics/Checks/Run Unit Info UI Checks`.
