using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace AutoBattler.Assets
{
    /// <summary>같은 키의 로드를 공유하고 마지막 이용자 해제 시 핸들 반환.</summary>
    public sealed class AssetService : IDisposable
    {
        internal sealed class Entry
        {
            public object Key; public Type Type; public AsyncOperationHandle Handle; public int Users; public bool Released;
        }
        public sealed class Lease<T> : IDisposable where T:UnityEngine.Object
        {
            public T Asset { get; }
            private AssetService owner; private readonly Entry entry;
            internal Lease(AssetService owner,Entry entry,T asset) { this.owner=owner; this.entry=entry; Asset=asset; }
            public void Dispose() { if(owner==null) return; owner.Return(entry); owner=null; }
        }
        private readonly Dictionary<object,Entry> entries=new Dictionary<object,Entry>();
        private bool disposed;
        public int ActiveHandleCount=>entries.Count;
        public async UniTask<Lease<T>> AcquireAsync<T>(AssetReference reference,CancellationToken token) where T:UnityEngine.Object
        {
            token.ThrowIfCancellationRequested();
            if(disposed) throw new ObjectDisposedException(nameof(AssetService));
            if(reference==null || !reference.RuntimeKeyIsValid()) throw new ArgumentException("에셋 참조 연결 필요.",nameof(reference));
            // 같은 GUID를 가진 별도 AssetReference도 핸들 공유.
            object key=reference.RuntimeKey;
            if(!entries.TryGetValue(key,out Entry entry))
            {
                entry=new Entry { Key=key, Type=typeof(T), Handle=Addressables.LoadAssetAsync<T>(key) };
                entries.Add(key,entry);
            }
            if(entry.Type!=typeof(T)) throw new ArgumentException("같은 주소에 다른 에셋 타입 요청.");
            entry.Users++;
            try
            {
                while(!entry.Handle.IsDone) await UniTask.Yield(PlayerLoopTiming.Update,token);
                token.ThrowIfCancellationRequested();
                if(disposed || entry.Released) throw new ObjectDisposedException(nameof(AssetService));
                if(entry.Handle.Status!=AsyncOperationStatus.Succeeded || !(entry.Handle.Result is T asset))
                    throw new InvalidOperationException($"에셋 로드 실패: {key}",entry.Handle.OperationException);
                return new Lease<T>(this,entry,asset);
            }
            catch { Return(entry); throw; }
        }
        private void Return(Entry entry)
        {
            if(entry.Released || --entry.Users>0) return;
            Release(entry);
        }
        private void Release(Entry entry)
        {
            if(entry.Released) return;
            entry.Released=true;
            if(entry.Handle.IsValid()) Addressables.Release(entry.Handle);
            if(entries.TryGetValue(entry.Key,out Entry stored) && ReferenceEquals(stored,entry)) entries.Remove(entry.Key);
        }
        public void Dispose()
        {
            if(disposed) return; disposed=true;
            foreach(Entry entry in new List<Entry>(entries.Values)) Release(entry);
        }
    }
}
