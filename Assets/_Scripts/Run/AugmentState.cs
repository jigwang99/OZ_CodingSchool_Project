using System;
using System.Collections.Generic;
using System.Linq;
using AutoBattler.Data.Augments;

namespace AutoBattler.Run
{
    /// <summary>강화 후보·선택 이력과 전용 난수 관리. 창을 닫아도 후보 유지.</summary>
    public sealed class AugmentState
    {
        private readonly IReadOnlyDictionary<int,AugmentDefinition> definitions;
        private readonly RunRandom random;
        private readonly List<int> selected=new List<int>();
        private readonly HashSet<int> completed=new HashSet<int>();
        private int[] offers=Array.Empty<int>();
        public IReadOnlyList<int> SelectedIds=>selected.AsReadOnly();
        public IReadOnlyList<int> OfferedIds=>Array.AsReadOnly(offers);
        public bool HasPending=>offers.Length>0;
        public int OfferedRound { get; private set; }
        public AugmentState(IReadOnlyDictionary<int,AugmentDefinition> definitions,int seed)
            :this(definitions,new RunRandom(seed^0x41A96B)) { }
        private AugmentState(IReadOnlyDictionary<int,AugmentDefinition> definitions,RunRandom random)
        { this.definitions=definitions; this.random=random; }
        public SavedAugments Capture()=>new SavedAugments
        {
            randomSeed=random.Seed, randomDrawCount=random.DrawCount, offeredRound=OfferedRound,
            selected=selected.ToArray(), completedRounds=completed.OrderBy(round=>round).ToArray(), offers=(int[])offers.Clone()
        };
        public static AugmentState Restore(IReadOnlyDictionary<int,AugmentDefinition> definitions,SavedAugments saved)
        {
            if(saved==null || saved.selected==null || saved.completedRounds==null || saved.offers==null
                || saved.selected.Distinct().Count()!=saved.selected.Length
                || saved.completedRounds.Distinct().Count()!=saved.completedRounds.Length
                || saved.selected.Length!=saved.completedRounds.Length
                || saved.selected.Any(id=>!definitions.ContainsKey(id))
                || saved.offers.Length!=0 && saved.offers.Length!=3
                || saved.offers.Distinct().Count()!=saved.offers.Length
                || saved.offers.Any(id=>!definitions.ContainsKey(id) || saved.selected.Contains(id)))
                throw new ArgumentException("저장된 증강 정보가 올바르지 않습니다.");
            var state=new AugmentState(definitions,new RunRandom(saved.randomSeed,saved.randomDrawCount));
            state.selected.AddRange(saved.selected); state.completed.UnionWith(saved.completedRounds);
            state.offers=(int[])saved.offers.Clone(); state.OfferedRound=saved.offeredRound;
            return state;
        }
        public void EnterRound(int round,bool enabled)
        {
            if(!enabled || completed.Contains(round) || HasPending || definitions.Count==0) return;
            var candidates=definitions.Keys.Where(id=>!selected.Contains(id)).OrderBy(id=>id).ToList();
            if(candidates.Count<3) throw new InvalidOperationException("강화 선택에는 미선택 후보 3개 필요.");
            offers=new int[3]; OfferedRound=round;
            for(int i=0;i<offers.Length;i++)
            { int index=random.Next(candidates.Count); offers[i]=candidates[index]; candidates.RemoveAt(index); }
        }
        public bool TrySelect(int round,int id,out AugmentDefinition definition)
        {
            definition=null;
            if(round!=OfferedRound || !HasPending || completed.Contains(round) || !offers.Contains(id)) return false;
            definition=definitions[id]; selected.Add(id); completed.Add(round); offers=Array.Empty<int>(); return true;
        }
        public IEnumerable<AugmentDefinition> Selected=>selected.Select(id=>definitions[id]);
    }
}
