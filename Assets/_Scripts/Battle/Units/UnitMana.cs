using System;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    /// <summary>마나 획득·소비·반환과 라운드 시작값 관리.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitMana : MonoBehaviour
    {
        public int Current { get; private set; }
        public int Maximum { get; private set; }=100;
        public int Starting { get; private set; }
        public event Action<int,int> Changed;
        public void Initialize(int maximum=100,int starting=0)
        { Maximum=Math.Max(1,maximum); Starting=Mathf.Clamp(starting,0,Maximum); ResetForRound(); }
        public void Add(int amount)
        { Set((int)Math.Min(Maximum,(long)Current+Math.Max(0,amount))); }
        public bool TrySpend(int amount,out int spent)
        {
            spent=0;
            if(amount<0 || Current<amount) return false;
            spent=amount; Set(Current-amount); return true;
        }
        public void ResetForRound()=>Set(Starting);
        public void Clear()=>Set(0);
        private void Set(int value)
        { Current=value; Changed?.Invoke(Current,Maximum); }
    }
}
