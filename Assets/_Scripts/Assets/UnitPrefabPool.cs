using System;
using System.Collections.Generic;
using System.Threading;
using AutoBattler.Battle.Units;
using AutoBattler.Pooling;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AutoBattler.Assets
{
    /// <summary>Addressables 프리팹 수명 관리와 공통 ObjectPoolManager 연결.</summary>
    public sealed class UnitPrefabPool
    {
        private readonly AssetService assets;
        private readonly Dictionary<object,AssetService.Lease<GameObject>> leases=new Dictionary<object,AssetService.Lease<GameObject>>();
        private readonly ObjectPoolManager manager;
        private readonly int initialSize;
        private bool disposed;
        public ObjectPoolManager Manager=>manager;

        public UnitPrefabPool(AssetService service,Transform parent,int initialSize=1)
        {
            assets=service; this.initialSize=Mathf.Max(0,initialSize);
            GameObject root=new GameObject(nameof(ObjectPoolManager)); root.transform.SetParent(parent,false);
            manager=root.AddComponent<ObjectPoolManager>();
        }
        public async UniTask PreloadAsync(AssetReferenceGameObject prefab,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(disposed) throw new ObjectDisposedException(nameof(UnitPrefabPool));
            object address=prefab.RuntimeKey;
            if(leases.ContainsKey(address)) return;
            AssetService.Lease<GameObject> lease=await assets.AcquireAsync<GameObject>(prefab,token);
            try
            {
                if(disposed) throw new ObjectDisposedException(nameof(UnitPrefabPool));
                // 동시 로드 완료 시 먼저 등록한 풀과 핸들 이용권만 유지.
                if(leases.ContainsKey(address)) { lease.Dispose(); return; }
                manager.RegisterPool(lease.Asset,initialSize); leases.Add(address,lease);
            }
            catch { lease.Dispose(); throw; }
        }
        public Unit Rent(AssetReferenceGameObject prefab)
        {
            if(disposed) throw new ObjectDisposedException(nameof(UnitPrefabPool));
            if(!leases.TryGetValue(prefab.RuntimeKey,out AssetService.Lease<GameObject> lease))
                throw new InvalidOperationException("프리팹 사전 로드 필요.");
            // Configure와 배치 정보 연결 후 호출 측에서 활성화.
            return manager.GetObject<Unit>(lease.Asset,false);
        }
        public void Return(Unit unit)
        {
            if(disposed || unit==null || manager==null) return;
            manager.ReturnObject(unit.gameObject);
        }
        public async UniTask DisposeAsync()
        {
            if(disposed) return;
            disposed=true;
            if(manager!=null) { manager.Dispose(); UnityEngine.Object.Destroy(manager.gameObject); }
            // 인스턴스 파괴 처리 뒤 Addressables 프리팹 핸들 해제.
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            foreach(AssetService.Lease<GameObject> lease in leases.Values) lease.Dispose(); leases.Clear();
        }
    }
}
