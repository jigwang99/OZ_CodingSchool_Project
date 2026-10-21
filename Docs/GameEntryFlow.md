# 타이틀과 스테이지 진입

타이틀 → 시작하기 → 스테이지 선택 → 1-1 선택 → 시작 → 기존 도전 진행 → 최종 결과 → 재도전 / 타이틀 화면.

선택 화면에는 1-1 스테이지 하나만 제공. 이는 기존 게임 전체의 진입점이며, 내부 전투는 기존 데이터의 1-1~3-4 총 12개 유지. 일반 전투 결과의 Next round와 지정 시점 증강 선택도 유지.

- 타이틀: 시작하기와 종료 버튼. 종료는 실행 파일에서는 앱 종료, Unity 에디터에서는 Play 종료.
- 스테이지: 선택 전 시작 버튼 잠금, 뒤로 버튼으로 타이틀 복귀.
- 최종 성공·실패: 재도전과 타이틀 화면 버튼. 재도전은 골드·목숨·기물·상점·증강·배치를 초기화한 새 도전.
- 장면 전환 중 중복 요청 차단. 전환 전 전투 취소, 풀 인스턴스 파괴, Addressables 핸들 해제 완료까지 대기.

`StageDefinition`과 `GameNavigationSettings` SO로 스테이지와 빌드 장면 인덱스 참조. 런타임 장면 이름 검색 미사용. `ProjectSetupSettings`는 에디터 장면·프리팹·설정 참조 보관. 셋업 메뉴가 TitleScene 0, BattleScene 1 순서로 빌드 등록.

UI: `TitleMenu.prefab`과 기존 `ResultPanel.prefab`. TextMeshProUGUI, 한글 SDF 폰트, DOTween 사용. 런타임 UI 생성 없음.

연결 메뉴: PixelTactics/Setup/Connect Title and Stage Flow.
검사 메뉴: PixelTactics/Checks/Run Title and Stage Flow Checks.

자동 검사는 실제 버튼 입력, 최종 결과의 재도전·복귀, 초기화, 중복 요청, 전투 취소, 핸들 해제 확인. 빠른 최종 결과 검사는 승패를 직접 정산하며, 실제 전투로 12라운드 공략 가능한지에 대한 밸런스 검증과 구분.
