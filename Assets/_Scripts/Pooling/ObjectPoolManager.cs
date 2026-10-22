using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler.Pooling
{
    /// <summary>Inspector 프리팹과 Addressables 로드 결과를 같은 Queue 풀로 관리.</summary>
    [DisallowMultipleComponent]
    public sealed class ObjectPoolManager : MonoBehaviour,IDisposable
    {
        [SerializeField,Min(0)] private int poolSize=1;
        [SerializeField] private List<GameObject> objList=new List<GameObject>();
        private readonly Dictionary<GameObject,Pool> pools=new Dictionary<GameObject,Pool>();
        private readonly Dictionary<Enum,Pool> keyedPools=new Dictionary<Enum,Pool>();
        private readonly Dictionary<GameObject,Pool> owners=new Dictionary<GameObject,Pool>();
        private readonly List<GameObject> roots=new List<GameObject>();
        private bool disposed;
        public int PoolCount=>pools.Count;

        private void Awake()=>InitializePools();
        private void InitializePools()
        {
            foreach(var prefab in objList)
            {
                if(prefab==null) continue;
                var poolable=prefab.GetComponent<IPoolable>();
                if(poolable!=null) RegisterPool(poolable.PoolKey,prefab,poolSize);
                else RegisterPool(prefab,poolSize);
            }
        }
        public Pool RegisterPool(GameObject prefab,int size=0)
        {
            if(disposed) throw new ObjectDisposedException(nameof(ObjectPoolManager));
            if(prefab==null) throw new ArgumentNullException(nameof(prefab));
            if(pools.TryGetValue(prefab,out var existing)) return existing;
            var root=new GameObject($"{prefab.name}_Pool");
            root.transform.SetParent(transform,false);
            var pool=new Pool(prefab,root.transform,Mathf.Max(0,size));
            pools.Add(prefab,pool); roots.Add(root);
            return pool;
        }
        public Pool RegisterPool(Enum key,GameObject prefab,int size=0)
        {
            if(key==null) throw new ArgumentNullException(nameof(key));
            if(keyedPools.TryGetValue(key,out var existing))
            {
                if(prefab!=null && pools.TryGetValue(prefab,out var same) && same==existing) return existing;
                throw new InvalidOperationException($"이미 다른 프리팹에 등록된 풀 키: {key}");
            }
            var pool=RegisterPool(prefab,size); keyedPools.Add(key,pool); return pool;
        }
        public T GetObject<T>(GameObject prefab,bool activate=true) where T:Component
        {
            if(disposed) throw new ObjectDisposedException(nameof(ObjectPoolManager));
            return prefab!=null && pools.TryGetValue(prefab,out var pool)?Rent<T>(pool,activate):null;
        }
        public T GetObject<T>(Enum key,bool activate=true) where T:Component
        {
            if(disposed) throw new ObjectDisposedException(nameof(ObjectPoolManager));
            return key!=null && keyedPools.TryGetValue(key,out var pool)?Rent<T>(pool,activate):null;
        }
        // 전투 표현처럼 고정 용량 재사용이 필요한 호출자를 위한 대여.
        public bool TryGetObject<T>(Enum key,out T component,bool activate=true) where T:Component
        {
            component=null;
            if(disposed || key==null || !keyedPools.TryGetValue(key,out var pool) || pool.AvailableCount==0) return false;
            component=Rent<T>(pool,activate); return component!=null;
        }
        private T Rent<T>(Pool pool,bool activate) where T:Component
        {
            // Init·OnEnable 전에 소유 풀 등록. 초기화 중 자체 반환도 소유권 유지.
            var component=pool.TakeObject<T>();
            owners[component.gameObject]=pool;
            pool.InitializeObject(component.gameObject,activate);
            return component;
        }
        public bool ReturnObject(GameObject go)
        { return !disposed && go!=null && owners.TryGetValue(go,out var pool) && pool.ReturnObject(go); }
        public bool ReturnObject(Enum key,GameObject go)
        {
            return !disposed && key!=null && go!=null && keyedPools.TryGetValue(key,out var pool)
                && owners.TryGetValue(go,out var owner) && owner==pool && pool.ReturnObject(go);
        }
        public void Dispose()
        {
            if(disposed) return;
            disposed=true;
            foreach(var pool in pools.Values) pool.Dispose();
            foreach(var root in roots) if(root!=null) UnityEngine.Object.Destroy(root);
            pools.Clear(); keyedPools.Clear(); owners.Clear(); roots.Clear();
        }
        private void OnDestroy()=>Dispose();
    }
}
