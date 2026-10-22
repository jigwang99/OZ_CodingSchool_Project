# 전투 이펙트와 아이콘

`Assets/_Data/BattleVisualSettings.asset`에서 이펙트 AssetReference, 사전 준비 수량, 크기, 최대 재생 시간과 스킬별 표현·아이콘 설정.
`AssetCatalog`의 `icons`는 기존 시트의 `icon_id` 1~8과 연결. 시트 스키마 변경 없음.

## 표시 시점

- 일반 공격: 선딜 종료 시 원거리 투사체 출발, 실제 피해 확정 시 타격 효과. 보호막만 소모된 피격도 표시.
- 스킬: 논리 투사체 도착 시 범위 효과 1회 재생. 개별 피해는 해당 대상에 타격 효과. 효과 자체가 피해·회복 판정을 수행하지 않음.
- 회복: 실제 회복량이 양수인 경우 민트색 효과.
- 보호막: 보호막 총량이 증가할 때 푸른 효과. 잔여량은 기존 회색 체력바.
- 기절: 머리 위 상태 아이콘. 해제·사망·비활성화·라운드 종료 시 반환.
- 상점: 유닛 8종의 직업 아이콘. 유닛 정보창: 유닛·스킬·HP·MP·보호막 아이콘.

## 재사용

기존 `Pool`, `ObjectPoolManager`, `IPoolable` 사용. 도전 로딩 중 Addressables 프리팹 8개 로드 후 인스턴스 156개 사전 준비. 전투 시작 전 모든 준비 완료.

`ObjectPoolManager.TryGetObject`는 확장 생성 없이 대여. 풀이 소진되면 해당 시각 효과만 생략하며 전투 판정은 계속 진행. 필요하면 SO의 capacity 조절.

`PooledBattleVisual.Init`와 반환에서 파티클 Stop/Clear, 스프라이트·색상·대상·타이머 초기화. 대여 세대 번호와 유닛 수명 참조로 이전 대여의 지연 반환이 새 효과를 반환하거나 새 유닛을 추적하지 않도록 구분.

전투 종료 시 구독 해제 및 전체 반환. 도전 종료·타이틀 이동 시에만 풀 인스턴스 정리 후 Addressables 핸들 해제. 전투 중 이펙트의 Instantiate/Destroy 사용 없음.

## 에셋과 빌드

HitEffectPack 원본을 상속하는 프로젝트 전용 Variant와 별도 URP 재질은 `Assets/_Prefabs/VFX`에 위치. 원본 에셋 수정 없음. 투사체·기절은 프로젝트 전용 SpriteRenderer 프리팹. VFX는 별도 로컬 Addressables `BattleVFX` 그룹으로 분리.

설정 SO가 연결된 이후 셋업은 기존 참조와 사용자 설정 유지. 최초 기본 에셋 연결 경로만 `BattleVisualSetup`에 존재하며 런타임에는 SO 참조와 ID 사용.

원본 프로젝트에는 HitEffectPack과 SPUM 패키지가 필요. 이 두 폴더는 기존 .gitignore 정책 유지. Packed Play Mode 및 배포 전 `PixelTactics > Setup > Build Local Addressables` 실행.

## 검증

`PixelTactics > Checks > Run Battle Effects and Icons Checks` 실행. 8종 스킬 발동, 기본 투사체·명중, 상태 해제, 풀 중복 반환·동일 인스턴스 재사용·용량 소진·자동 반환·라운드 정리·핸들 해제와 기존 전체 플레이 흐름 검사.
