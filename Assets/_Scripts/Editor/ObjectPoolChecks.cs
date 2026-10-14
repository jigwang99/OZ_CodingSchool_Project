#if UNITY_EDITOR
using System;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Units;
using AutoBattler.Pooling;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AutoBattler.EditorTools
{
    public static class ObjectPoolChecks
    {
        private enum ProbeKey { Unit,Projectile,Missing }
        private static int checks;
        private static void Expect(bool condition,string message)
        { if(!condition) throw new Exception("Object pool check: "+message); checks++; }

        public static async UniTask RunPlay(RunController run)
        {
            checks=0;
            var root=new GameObject("ObjectPoolChecks");
            var prefab=new GameObject("PoolProbePrefab");
            var secondPrefab=new GameObject("OtherPoolProbePrefab");
            var foreign=new GameObject("ForeignPoolProbe");
            prefab.transform.localScale=new Vector3(2f,3f,1f);
            var manager=root.AddComponent<ObjectPoolManager>();
            try
            {
                var pool=manager.RegisterPool(ProbeKey.Unit,prefab,2);
                Expect(pool.InstanceCount==2 && pool.AvailableCount==2,"초기 수량 예열");
                Expect(manager.RegisterPool(ProbeKey.Unit,prefab,2)==pool && manager.PoolCount==1,"동일 등록의 풀 공유");
                var first=manager.GetObject<Transform>(ProbeKey.Unit,false);
                var second=manager.GetObject<Transform>(prefab,false);
                Expect(first!=second && !first.gameObject.activeSelf && !second.gameObject.activeSelf,"초기 인스턴스 분리와 비활성 대여");
                var third=manager.GetObject<Transform>(prefab);
                Expect(third.gameObject.activeSelf && pool.InstanceCount==3,"고갈 시 확장과 기본 활성화");
                first.SetParent(foreign.transform); first.localScale=Vector3.one; first.localPosition=Vector3.one*20f;
                Expect(manager.ReturnObject(first.gameObject),"비활성 상태에서 반환 허용");
                Expect(first.parent!=foreign.transform && first.localScale==prefab.transform.localScale
                    && first.localPosition==prefab.transform.localPosition,"반환 시 부모와 Transform 복원");
                Expect(!manager.ReturnObject(first.gameObject) && pool.AvailableCount==1,"중복 반환 차단");
                Expect(manager.ReturnObject(ProbeKey.Unit,second.gameObject),"Enum 키 반환");
                var again=manager.GetObject<Transform>(prefab,false);
                var againSecond=manager.GetObject<Transform>(ProbeKey.Unit,false);
                Expect(again==first && againSecond==second,"Queue FIFO 재사용");
                manager.RegisterPool(ProbeKey.Projectile,secondPrefab,0);
                Expect(!manager.ReturnObject(ProbeKey.Projectile,third.gameObject)
                    && !manager.ReturnObject(foreign),"다른 풀과 외부 오브젝트 반환 거부");
                bool conflict=false;
                try { manager.RegisterPool(ProbeKey.Unit,secondPrefab); } catch(InvalidOperationException) { conflict=true; }
                Expect(conflict && manager.PoolCount==2,"동일 Enum에 다른 프리팹 등록 차단");
                Expect(manager.GetObject<Transform>(ProbeKey.Missing)==null,"미등록 키 결과");
                CheckLifecycle(manager);
                manager.ReturnObject(again.gameObject); manager.ReturnObject(againSecond.gameObject);
                manager.ReturnObject(third.gameObject);
                bool wrongType=false;
                try { manager.GetObject<SpriteRenderer>(prefab); } catch(InvalidOperationException) { wrongType=true; }
                Expect(wrongType && pool.AvailableCount==3,"없는 컴포넌트 요청 시 인스턴스 보존");
                var destroyed=manager.GetObject<Transform>(secondPrefab,false);
                manager.ReturnObject(destroyed.gameObject); UnityEngine.Object.Destroy(destroyed.gameObject);
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate); await UniTask.Yield();
                var replacement=manager.GetObject<Transform>(secondPrefab,false);
                Expect(replacement!=null && replacement!=destroyed,"외부에서 파괴된 대기 인스턴스 대체");
                manager.Dispose(); manager.Dispose();
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate); await UniTask.Yield();
                Expect(first==null && second==null && third==null && replacement==null && manager.PoolCount==0,
                    "대여·대기 전체 파괴와 반복 해제");
                bool disposed=false;
                try { manager.GetObject<Transform>(prefab); } catch(ObjectDisposedException) { disposed=true; }
                Expect(disposed,"해제 후 대여 차단");
            }
            finally
            {
                manager.Dispose(); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(prefab);
                UnityEngine.Object.Destroy(secondPrefab); UnityEngine.Object.Destroy(foreign);
            }

            using var assets=new AssetService();
            var unitsRoot=new GameObject("AddressablePoolChecks");
            var flowRoot=new GameObject("PoolCheckFlow",typeof(BattleFlowController));
            var flow=flowRoot.GetComponent<BattleFlowController>();
            var unitPool=new UnitPrefabPool(assets,unitsRoot.transform,2);
            var definition=run.State.Data.Units.Values.First();
            var reference=run.Catalog.VisualPrefab(definition.VisualId);
            try
            {
                var copy=new AssetReferenceGameObject(reference.AssetGUID);
                await UniTask.WhenAll(unitPool.PreloadAsync(reference,CancellationToken.None),
                    unitPool.PreloadAsync(copy,CancellationToken.None));
                Expect(assets.ActiveHandleCount==1 && unitPool.Manager.PoolCount==1,"동일 GUID 동시 로드의 핸들·풀 공유");
                var unit=unitPool.Rent(reference); var second=unitPool.Rent(copy);
                Expect(unit!=second && !unit.gameObject.activeSelf && unitPool.Manager.PoolCount==1,"Addressables 예열 유닛 비활성 대여");
                unit.Configure(definition,1,720001,UnitTeam.Enemy,run.State.Data,flow,run.Grid,run.Bench,HexCoord.FromOffset(6,3));
                unit.gameObject.SetActive(true); unit.BeginBattle(); Expect(flow.TryStartCombat(),"검사 전투 시작");
                unit.Effects.Apply(new EffectDefinition(9951,EffectKind.Stun,1f,10f),
                    new ModifierSource(ModifierSourceKind.Skill,9951,720002));
                unit.StatModifiers.AddModifier(new ModifierSource(ModifierSourceKind.System,9952),
                    StatType.AttackDamage,ModifierOperation.Flat,50f);
                unitPool.Return(unit); unitPool.Return(second); unitPool.Return(unit);
                var reused=unitPool.Rent(reference); var reusedSecond=unitPool.Rent(reference);
                Expect(reused==unit && reusedSecond==second,"유닛 FIFO 재사용과 중복 반환 방지");
                Expect(reused.Effects.Active.Count==0 && reused.StatModifiers.State.ModifierCount==0
                    && !reused.Combat.IsAttacking && !reused.Move.IsMoving,"유닛 반환 시 효과·수정자·행동 정리");
                reused.Configure(definition,1,720003,UnitTeam.Enemy,run.State.Data,flow,run.Grid,run.Bench,HexCoord.FromOffset(7,3));
                reused.gameObject.SetActive(true);
                Expect(reused.InstanceId==720003 && reused.Health.CurrentHp==definition.Hp && !reused.Effects.IsStunned,
                    "재사용 시 새 데이터와 체력 초기화");
                await unitPool.DisposeAsync(); await UniTask.Yield();
                Expect(reused==null && reusedSecond==null && assets.ActiveHandleCount==0,"유닛 파괴 뒤 Addressables 핸들 해제");
                await unitPool.DisposeAsync();
            }
            finally
            {
                await unitPool.DisposeAsync(); UnityEngine.Object.Destroy(unitsRoot); UnityEngine.Object.Destroy(flowRoot);
            }
            Debug.Log("OBJECT_POOL_PLAY_PASS "+checks+" prewarm-fifo-expand-ownership-duplicate-inactive-addressables-reset-dispose");
        }
        private static void CheckLifecycle(ObjectPoolManager manager)
        {
            var prefab=new GameObject("PoolLifecycleProbePrefab");
            var source=prefab.AddComponent<ObjectPoolLifecycleProbe>(); source.Manager=manager;
            var pool=manager.RegisterPool(source.PoolKey,prefab,1);
            try
            {
                var first=manager.GetObject<ObjectPoolLifecycleProbe>(source.PoolKey,false);
                Expect(first.InitCount==1 && first.WasInactiveDuringInit && !first.gameObject.activeSelf,
                    "비활성 대여에도 Init 한 번 호출");
                first.ReturnToPool(); first.ReturnToPool();
                Expect(pool.AvailableCount==1,"자체 반환과 중복 반환 차단");
                var again=manager.GetObject<ObjectPoolLifecycleProbe>(source.PoolKey);
                Expect(again==first && again.InitCount==2 && again.InitCountAtEnable==2,
                    "재사용마다 Init 실행 후 OnEnable");
                Expect(manager.ReturnObject(again.gameObject) && pool.AvailableCount==1,"관리자 반환 시 재귀 호출 없음");
                again.ThrowOnInit=true; bool failed=false;
                try { manager.GetObject<ObjectPoolLifecycleProbe>(prefab); } catch(InvalidOperationException) { failed=true; }
                Expect(failed && pool.AvailableCount==1 && !again.gameObject.activeSelf,"Init 예외 시 인스턴스 회수");
                again.ThrowOnInit=false; again.ReturnDuringInit=true;
                var returned=manager.GetObject<ObjectPoolLifecycleProbe>(prefab);
                Expect(!returned.gameObject.activeSelf && pool.AvailableCount==1,"Init 내부 반환 시 재활성화 방지");
                again.ReturnDuringInit=false;
                var healthy=manager.GetObject<ObjectPoolLifecycleProbe>(prefab,false);
                Expect(healthy==again && pool.AvailableCount==0,"예외와 자체 반환 뒤 정상 재사용");
                healthy.ReturnToPool();
                using var direct=new Pool(prefab,manager.transform,1);
                var directItem=direct.GetObject<ObjectPoolLifecycleProbe>(false);
                Expect(directItem.InitCount==1 && directItem.WasInactiveDuringInit,"Pool 직접 대여에도 Init 적용");
                direct.ReturnObject(directItem.gameObject);
            }
            finally { UnityEngine.Object.Destroy(prefab); }
        }
        public static void BuildAndPlay()
        {
            BehaviourTreeChecks.RunDomain(); UnitEffectsChecks.RunDomain(); MilestoneChecks.RunAll();
            CharacterPrefabSetup.BuildForVerification();
        }
    }
}
#endif
