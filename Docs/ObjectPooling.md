# 공통 오브젝트 풀

사용자 작성 Pool·ObjectPoolManager의 Queue, 초기 수량 생성, GetObject·ReturnObject 구조를 기반으로 구성.
네임스페이스는 `AutoBattler.Pooling`.

- `Pool`: 프리팹 한 종류의 생성, FIFO 대여·반환, 초기 수량 생성, 전체 해제.
- `ObjectPoolManager`: 프리팹별 Pool 등록, Enum 키 선택 등록, 인스턴스 소유 풀 추적.
- `IPoolable`: Enum PoolKey, 대여 시 Init, 오브젝트 자체 반환 요청 ReturnToPool 제공.
- `UnitPrefabPool`: Addressables 로드 이용권을 보유하고 공통 관리자에 유닛 프리팹 등록.

## 유닛 흐름

```text
AssetReference → AssetService 로드 → ObjectPoolManager.RegisterPool(프리팹, 초기 수량)
Rent → GetObject<Unit>(프리팹, false) → IPoolable이 있으면 Init → Configure·배치 연결 → SetActive(true)
Return → 원래 Pool 확인 → SetActive(false) → 부모·Transform 복원 → Queue 보관
종료 → 모든 인스턴스 파괴 → 프레임 처리 → Addressables 이용권 해제
```

RunController의 Unit Pool Size로 프리팹별 초기 수량 설정. 기본값 1, 0이면 필요할 때만 생성.
초기 수량을 초과하면 Instantiate로 확장. 최대 보관 개수 제한이나 자동 축소는 없음.
판매·합성 제거 아군과 라운드 교체 적은 반환, 계속 보유하는 아군은 기존 오브젝트 유지.
Unit.OnDisable 등 기존 모듈에서 이동·공격·효과·능력치 수정자 정리.

## IPoolable 호출 규칙

```csharp
public interface IPoolable
{
    Enum PoolKey { get; }
    void Init();
    void ReturnToPool();
}
```

루트 GameObject에 IPoolable 구현 컴포넌트가 있으면 매 GetObject에서 Init을 정확히 한 번 호출.
초기 예열에서는 Init을 호출하지 않으며, GetObject(..., false)에도 Init 호출.
호출 순서는 대여 표시 → 관리자 소유권 기록 → Init → 요청한 경우 SetActive(true).
Init은 매 재사용에 필요한 타이머·잔상 등 공통 상태 초기화 용도. 대여 후 대상·위치 등 실행별 데이터는 별도 주입.

ReturnToPool은 구현 객체가 관리자에게 반환을 요청하는 메서드.
구현 예시: `public void ReturnToPool() => poolManager.ReturnObject(gameObject);`
poolManager는 생성·설정 시 해당 오브젝트를 소유하는 관리자 참조로 주입.
manager.ReturnObject는 IPoolable.ReturnToPool을 역호출하지 않음. 반환 재귀 방지.
반환 시 정리 코드는 기존 Unit처럼 OnDisable에서 수행.
비활성 대여 상태의 취소 반환도 필요한 정리가 있다면 구현 객체에서 별도 수행.

Init 예외 시 비활성 상태로 회수 후 예외 전달. Init 내부에서 자체 반환하면 자동 활성화 생략.
Pool을 직접 사용할 때도 GetObject에서 Init 호출. 이 경우 자체 반환 구현은 해당 Pool.ReturnObject로 연결 필요.
기존 Unit은 IPoolable 구현 없이 Configure·OnDisable 흐름 유지. 유닛별 초기화는 Definition 주입이 필요하므로
매개변수가 없는 Init으로 대체하지 않음. IPoolable 없이도 공통 풀 사용 가능.

## 다른 오브젝트 사용 예시

```csharp
// manager는 SerializeField 또는 생성 시 주입한 ObjectPoolManager 참조.
manager.RegisterPool(projectilePrefab, 10);
var projectile = manager.GetObject<Projectile>(projectilePrefab, false);
projectile.Initialize(target);
projectile.gameObject.SetActive(true);
// 충돌 또는 수명 종료 시 반환.
manager.ReturnObject(projectile.gameObject);
```

Projectile은 향후 구현할 컴포넌트 예시이며 현재 실제 투사체·VFX는 미구현.
해당 컴포넌트에서 입자 재생·잔상·타이머 등 각자의 재사용 상태 초기화 필요.

```csharp
// 기존 Enum 키 API도 사용 가능. key는 사용자가 정의한 Enum 값.
manager.RegisterPool(key, prefab, 5);
var component = manager.GetObject<SomeComponent>(key);
manager.ReturnObject(key, component.gameObject);
```

Inspector Obj List 등록 시 IPoolable이 있으면 Enum 키 등록, 없으면 프리팹 참조로 등록.
Addressables 유닛은 로드된 프리팹 자체를 키로 사용하므로 IPoolable 추가나 유닛별 Enum 확장 불필요.
전역 instance 대신 소유한 관리자 참조 사용. 다른 검사·장면의 관리자가 서로 제거되는 상황 방지.

## 원본 코드에서 보완한 동작

중복·외부·다른 풀 반환 거부. 비활성 대여 객체도 반환 가능.
없는 컴포넌트 요청은 명시적 예외를 발생시키고 인스턴스는 풀에 보존.
반환 시 활성 상태와 부모·위치·회전·크기를 초기값으로 복원.
대기 중 외부 파괴된 오브젝트는 다음 대여 시 대체.
일반 GetObject는 원본처럼 자동 활성화, 유닛은 false를 전달하여 설정 후 활성화.
새 인스턴스 생성은 기존처럼 Instantiate 후 비활성화 방식이므로 활성 원본 프리팹의 첫 생성에서
Awake·OnEnable이 설정 전 호출될 수 있음. 기존 유닛처럼 초기 데이터가 없는 상태를 처리하도록 작성 필요.
동일 AssetReference GUID의 동시 로드는 핸들과 풀을 공유.

FIFO이므로 반환 순서대로 재사용. 대기 객체가 여러 개면 방금 반환한 객체가 다음에 바로 나오지는 않을 수 있음.
공통 관리자는 Addressables를 직접 해제하지 않음. UnitPrefabPool에서 인스턴스 파괴 이후 해제.
직접 로드한 VFX 등을 등록할 때도 로드 이용권의 소유자가 같은 해제 순서 유지 필요.

## 검사

ObjectPoolChecks.BuildAndPlay에서 예열·확장·FIFO·소유권·중복 반환·비활성 반환·외부 파괴·
동시 Addressables 로드·유닛 초기화·전체 해제와 기존 행동트리·전투·UI 검사 실행.
IPoolable Init 호출 횟수·활성화 순서·자체 반환·초기화 예외 회수·직접 Pool 대여 검사 포함.
