#if UNITY_EDITOR
using System;
using AutoBattler.Pooling;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    /// <summary>초기화 호출 횟수·활성화 순서·자체 반환을 확인. 에디터 실행에만 포함.</summary>
    public sealed class ObjectPoolLifecycleProbe : MonoBehaviour,IPoolable
    {
        private enum ProbeKey { Lifecycle }
        public Enum PoolKey=>ProbeKey.Lifecycle;
        public ObjectPoolManager Manager;
        public int InitCount { get; private set; }
        public int InitCountAtEnable { get; private set; }
        public bool WasInactiveDuringInit { get; private set; }
        public bool ThrowOnInit;
        public bool ReturnDuringInit;
        public void Init()
        {
            InitCount++;
            WasInactiveDuringInit=!gameObject.activeSelf;
            if(ThrowOnInit) throw new InvalidOperationException("Lifecycle probe Init failure.");
            if(ReturnDuringInit) ReturnToPool();
        }
        public void ReturnToPool()=>Manager.ReturnObject(gameObject);
        private void OnEnable()=>InitCountAtEnable=InitCount;
    }
}
#endif
