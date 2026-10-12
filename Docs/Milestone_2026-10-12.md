# 2026-10-12 구현 기준

기획서 일정의 10월 11일 마감 항목까지 구현. 10월 12~18일 작업인 고유 스킬·효과·시너지는 다음 범위. 8종의 정의와 12라운드 적 구성은 기본 공격으로 현재 흐름을 실행하기 위한 초기 데이터이며 최종 밸런스가 아님.

## 실행

1. 원본 프로젝트의 `Assets/_Scenes/BattleScene.unity`를 열고 Play.
2. JSON 정의와 PIXEM 프리팹을 Addressables로 로드한 후 시작 기물 1기, 4골드, 레벨 1, 목숨 3으로 시작.
3. 대기석 기물을 Game 뷰에서 아군 영역으로 드래그. 적 영역 드롭·인원 초과는 원위치 복원, 이미 점유한 칸은 교환.
4. 상점에서 구매, Refresh, XP 구매. 기물 클릭 후 Sell selected로 판매.
5. Start battle을 누르면 자동 이동·공격. 적 전멸이면 승리, 아군 전멸·동시 전멸·60초 초과면 패배.
6. 전투 결과에서 수입·경험치·목숨을 한 번 정산. Next round로 체력·준비 배치 복원, 적 편성 교체, 무료 상점 갱신.

레벨은 경험치 문턱을 넘으면 자동 상승. 2성은 동일 1성 3개 구매 시 자동 합성하며 전장 기물을 우선 유지. 대기석 8칸이 차 있어도 합성 가능한 구매는 성공. 2성 체력·공격력은 1.8배, 판매는 가격×3−1. 적은 플레이어 기물 풀을 사용하지 않음.

보드는 사용자가 앞서 수정한 8×7, 아군 4열을 유지. 기획서의 6×6 표기보다 사용자 지시 우선. 씬 뷰 이동 도구로 Transform만 바꾸는 동작은 런타임 배치 명령이 아니므로 Game 뷰에서 드래그해야 함.

## 책임과 수명

- `RunState`: 골드·XP·목숨·라운드, 상점 예약과 재고, 구매·판매·2성 합성. 준비 중 명령만 허용.
- `RunController`: 로드 완료 전 입력 잠금, 런타임 기물 생성, 전투 시작·정산·다음 준비 연결.
- `Unit`: 정수 정의 ID로 주입된 기물별 데이터, 팀, 준비 좌표와 전투 좌표 연결.
- `UnitHealth`: 체력·방어 공식·피해·사망·복원.
- `UnitCombat`: 사거리, 선딜 0.2초, 치명타, 공격 간격, 기본 공격당 마나 10. 원거리는 논리 투사체 도착 시간에 피해 적용.
- `UnitMove`: 셀당 0.3초 이동과 취소. 경로 선택은 담당하지 않음.
- `CombatBoard` / `HexPathfinder` / `AutoBattleController`: 점유·다음 칸 예약, 6방향 A*, 대상 거리→역할→인스턴스 ID 우선순위. 이동 요청은 ID 순서로 처리.
- `DataRepository`: 도전 동안 고정한 JSON 정의의 읽기 전용 조회. 수치 SO는 도전 로딩에 사용하지 않음.
- `AssetCatalog`: 정수 visual_id를 Addressables 주소에 연결하는 SO. 현재 8종 모두 PIXEM Character_2 샘플 외형을 공유하며 이름·성급·팀으로 구분.
- `AssetService`: 동시 요청의 핸들 공유, 이용자별 Lease, 마지막 이용자 해제 시 Release. 로드 중 취소·실패에도 정리.
- `UnitPrefabPool`: 프리팹 사전 로드 후 일반 Instantiate 및 재사용. 인스턴스 파괴가 끝난 다음 핸들 해제.
- `RunHUD` / `UnitHealthBar`: TextMeshProUGUI와 DOTween으로 표시. 현재 UI는 기본 폰트 범위에 맞춘 영문 프로토타입.

## 시트 가져오기

시트 ID는 `1BTGlKR7D8g1PKnhi-0NfWXK7bCJyaFRTLd2hGSlI6mo`. 현재 사용자 시트는 데이터가 없으므로 원격 실제 데이터 가져오기는 아직 확인할 수 없음.

1. `Docs/Sheets`의 CSV 8개를 각각 같은 이름의 시트 탭으로 가져오기: Manifest, Units, Rounds, RoundUnits, Levels, ShopRates, PoolCounts, Economy.
2. Unity 메뉴 `PixelTactics > Data > Google Sheets Import`.
3. 공개 읽기 시트는 인증 없이 CSV 가져오기. 비공개 시트는 읽기 권한 OAuth 액세스 토큰을 입력하거나 Sheets API 키에 맞는 접근 환경 사용. 인증 값은 창 메모리에만 저장하고 빌드에 포함하지 않음.
4. 모든 표·양수 고유 ID·현재 지원하는 참조·범위·좌표 중복·상점 확률 합계가 정상이면 `Assets/_Data/Snapshots/GameSnapshot.json` 교체. 실패·취소 시 이전 스냅샷 유지. CSV 폴더 가져오기로 같은 입력 경로를 오프라인 확인 가능.
5. 배포 전 `PixelTactics > Setup > Build Local Addressables` 실행. 에디터 기본 Play는 Asset Database 사용 모드이므로 로컬 JSON 수정 후 즉시 반영.

### 초기값과 스키마

기획서에서 정한 경제·레벨·확률·재고·성급 규칙은 그대로 초기화. 유닛 HP·공격력·방어력·공격 간격·사거리는 기획서에 최종 숫자가 없어 임시 값으로 작성. 이름은 현재 UI 표시를 위한 영문 별칭. Origin ID: 인간 1, 요정 2, 언데드 3, 오크 4. Class ID: 검사 1, 마법사 2, 궁수 3, 암살자 4. Combat role: 탱커 0, 전사 1, 딜러 2, 힐러 3. Skill ID는 현재 0이며 고유 스킬 단계에서 확장. icon_id는 다음 UI 에셋 단계용 예약 ID.

Economy rule_id: 1 시작 골드, 2 시작 목숨, 3 갱신 비용, 4 XP 구매 비용, 5 구매 XP, 6 라운드 XP, 7 이자 골드 단위, 8 이자 상한, 9 전투 제한 초, 10 셀 이동 초, 11 선딜 초, 12 투사체 셀/초, 13 2성 배율.

JSON schema_version=1은 현재 구현한 8개 탭에 한정. Skills·Effects·Traits·Augments 및 최종 BossProfiles 스키마는 해당 주차에 추가. 보스 수치도 기획서의 초기 배율을 사용하는 샘플이며 스킬은 미적용.

## 확인 메뉴

- `PixelTactics > Checks > Run Domain Checks`: 경제·재고 보존, 구매 실패, 2성, 레벨 상한, 정산, 좌표·경로·예약, CSV와 오류 입력 검사.
- `PixelTactics > Checks > Run Play Checks`: 초기 로드·구매·실제 마우스 드래그·자동 전투 종료·복원·Addressables 동시 로드와 수명 검사. 검사용 Play에서 시간 배율 10을 사용하고 종료 시 1로 복구.

검사와 구현은 분리한 검증 프로젝트에서 확인 후 원본에 적용. 빌드 프로필의 잘못된 SampleScene 경로는 BattleScene으로 수정. 최종 아트, 고유 스킬, 시너지, 강화 선택, 저장, 라운드 편집기, 음향은 이후 마감 범위.
