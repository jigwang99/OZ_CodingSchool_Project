using System;
using System.Collections.Generic;
using System.Threading;
using AutoBattler.Battle.Units;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Assets
{
    /// <summary>프리팹 핸들 1개를 보유하고 일반 Instantiate 인스턴스 재사용.</summary>
    public sealed class UnitPrefabPool
    {
        private readonly AssetService assets;
        private readonly Dictionary<string,AssetService.Lease<GameObject>> leases=new Dictionary<string,AssetService.Lease<GameObject>>();
        private readonly Dictionary<string,Stack<Unit>> idle=new Dictionary<string,Stack<Unit>>();
        private readonly Dictionary<Unit,string> instances=new Dictionary<Unit,string>();
        private readonly Transform parent;
        public UnitPrefabPool(AssetService service,Transform parent) { assets=service; this.parent=parent; }
        public async UniTask PreloadAsync(string address,CancellationToken token)
        {
            if(leases.ContainsKey(address)) return;
            var lease=await assets.AcquireAsync<GameObject>(address,token);
            leases.Add(address,lease); idle.Add(address,new Stack<Unit>());
        }
        public Unit Rent(string address)
        {
            if(!leases.ContainsKey(address)) throw new InvalidOperationException("프리팹 사전 로드 필요.");
            if(idle[address].Count>0) return idle[address].Pop();
            var unit=UnityEngine.Object.Instantiate(leases[address].Asset,parent).GetComponent<Unit>();
            if(unit==null) throw new InvalidOperationException("프리팹 Unit 컴포넌트 필요.");
            unit.gameObject.SetActive(false); instances.Add(unit,address); return unit;
        }
        public void Return(Unit unit)
        {
            if(unit==null || !instances.TryGetValue(unit,out string address) || !unit.gameObject.activeSelf) return;
            unit.Move.Stop(); unit.gameObject.SetActive(false); idle[address].Push(unit);
        }
        public async UniTask DisposeAsync()
        {
            foreach(var unit in instances.Keys) if(unit!=null) UnityEngine.Object.Destroy(unit.gameObject);
            instances.Clear(); idle.Clear();
            // Destroy가 처리된 프레임 다음에 프리팹 핸들 해제.
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            foreach(var lease in leases.Values) lease.Dispose(); leases.Clear();
        }
    }
}
