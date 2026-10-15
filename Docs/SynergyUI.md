# 시너지 UI

`BattleHUD`에 중첩한 `SynergyPanel.prefab`에서 계열 4종과 직업 4종을 구분해 표시.
기존 HUD의 Canvas를 공유하고 별도 Canvas를 추가하지 않음. 모든 Graphic의 Raycast Target을 꺼서 기물 드래그 입력 통과.

- 이름과 현재 종류 수/필요 종류 수, 적용 대상과 효과 표시.
- 0종인 시너지는 숨김. 미충족 상태(예: 1/2)는 회색, 활성 상태는 녹색과 ACTIVE 문구로 구분. 변경 시 DOTween 색상 전환.
- 표시할 행만 위로 모아 배치하고 패널 높이도 조절. 비어 있는 그룹 제목도 숨김. 배치 기물이 없으면 배치 안내 표시.
- 효과의 비율·시간·체력 기준 등은 현재 TraitDefinition에서 읽어 설명 생성.
- 같은 그룹의 단계가 여러 개이면 충족한 최고 단계 표시. 미충족이면 첫 단계 표시.
- 준비 화면에서 RunController.DisplayChanged → RunHUD → SynergyPanel로 갱신. 패널은 매 프레임 집계하지 않음.
- 전투와 결과에서는 확정한 스냅샷 표시. 사망으로 시너지 수를 줄이지 않음.
- 로드 전과 시너지 데이터가 없는 이전 스키마에 별도 안내 표시.

UI는 기존 HUD 및 기본 TMP 폰트와 동일하게 영문 사용. 주석은 한글 사용.
씬은 기존 BattleHUD 프리팹 연결을 통해 자동 반영. 새 UI 객체는 에디터에서 저장하며 런타임에서 UI를 생성하지 않음.
설정 SO와 RunHUD의 직렬화 참조를 사용하고 오브젝트 이름으로 조회하지 않음.

`PixelTactics > Setup > Connect Synergy UI`는 기존 HUD 구성을 유지하며 누락된 패널만 연결.
`PixelTactics > Checks > Run Synergy UI Checks`에서 연결·집계 표시·단계·문구·상태 전환·텍스트 잘림과 기존 플레이 흐름 검사.
