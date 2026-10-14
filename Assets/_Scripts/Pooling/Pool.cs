using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler.Pooling
{
    /// <summary>Queue 기반 프리팹 인스턴스 재사용과 초기 수량 생성.</summary>
    public sealed class Pool : IDisposable
    {
        private readonly Queue<GameObject> pool=new Queue<GameObject>();
        private readonly HashSet<GameObject> instances=new HashSet<GameObject>();
        private readonly HashSet<GameObject> rented=new HashSet<GameObject>();
        private readonly GameObject prefab;
        private readonly Transform parent;
        private readonly Vector3 initialPosition,initialScale;
        private readonly Quaternion initialRotation;
        private bool disposed;
        public int AvailableCount=>pool.Count;
        public int InstanceCount=>instances.Count;

        public Pool(GameObject prefab,Transform parent,int size)
        {
            this.prefab=prefab; this.parent=parent;
            initialPosition=prefab.transform.localPosition;
            initialRotation=prefab.transform.localRotation;
            initialScale=prefab.transform.localScale;
            for(int i=0;i<size;i++) Create();
        }
        private GameObject Create()
        {
            GameObject go=UnityEngine.Object.Instantiate(prefab,parent);
            go.SetActive(false); instances.Add(go); pool.Enqueue(go);
            return go;
        }
        public T GetObject<T>(bool activate=true) where T:Component
        {
            var component=TakeObject<T>();
            InitializeObject(component.gameObject,activate);
            return component;
        }
        // 관리자에서 Init 전에 소유 풀을 기록할 수 있도록 대여와 초기화 분리.
        internal T TakeObject<T>() where T:Component
        {
            if(disposed) throw new ObjectDisposedException(nameof(Pool));
            GameObject go=null;
            while(go==null)
            {
                if(pool.Count==0) Create();
                go=pool.Dequeue();
                if(go==null) instances.Remove(go);
            }
            // 잘못된 타입 요청으로 인스턴스를 잃거나 OnEnable을 호출하지 않도록 먼저 확인.
            if(!go.TryGetComponent<T>(out var component))
            { pool.Enqueue(go); throw new InvalidOperationException($"프리팹에 {typeof(T).Name} 컴포넌트 필요."); }
            rented.Add(go);
            return component;
        }
        internal void InitializeObject(GameObject go,bool activate)
        {
            try
            {
                go.GetComponent<IPoolable>()?.Init();
                // Init에서 스스로 반환한 객체는 활성화하지 않음.
                if(activate && go!=null && rented.Contains(go)) go.SetActive(true);
            }
            catch
            {
                // 초기화 실패 시 인스턴스를 분실하지 않고 비활성 상태로 회수.
                ReturnObject(go);
                throw;
            }
        }
        public bool ReturnObject(GameObject go)
        {
            if(disposed || go==null || !rented.Remove(go)) return false;
            go.SetActive(false);
            go.transform.SetParent(parent,false);
            go.transform.SetLocalPositionAndRotation(initialPosition,initialRotation);
            go.transform.localScale=initialScale;
            pool.Enqueue(go);
            return true;
        }
        public void Dispose()
        {
            if(disposed) return;
            disposed=true;
            foreach(var go in instances) if(go!=null) UnityEngine.Object.Destroy(go);
            instances.Clear(); rented.Clear(); pool.Clear();
        }
    }
}
