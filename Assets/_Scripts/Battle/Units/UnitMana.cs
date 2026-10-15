using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    /// <summary>마나 획득·소비·반환과 라운드 시작값 관리.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitMana : MonoBehaviour
    {
        public int Current { get; private set; }
        public int Maximum { get; private set; }=100;
        private int baseStarting;
        private readonly Dictionary<ModifierSource,int> startingBonuses=new Dictionary<ModifierSource,int>();
        public int Starting
        {
            get
            {
                long value=baseStarting;
                foreach(int bonus in startingBonuses.Values) value+=bonus;
                return (int)Math.Max(0,Math.Min(Maximum,value));
            }
        }
        public event Action<int,int> Changed;
        public void Initialize(int maximum=100,int starting=0)
        { Maximum=Math.Max(1,maximum); baseStarting=Mathf.Clamp(starting,0,Maximum); startingBonuses.Clear(); ResetForRound(); }
        public void SetStartingBonus(ModifierSource source,int amount)=>startingBonuses[source]=Math.Max(0,amount);
        public void RemoveStartingBonus(ModifierSource source)=>startingBonuses.Remove(source);
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
