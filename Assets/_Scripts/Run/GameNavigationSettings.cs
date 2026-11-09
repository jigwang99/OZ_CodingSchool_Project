using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoBattler.Run
{
    /// <summary>빌드에 등록한 장면 인덱스와 선택 가능한 스테이지 참조.</summary>
    [CreateAssetMenu(menuName="PixelTactics/Game Navigation Settings")]
    public sealed class GameNavigationSettings : ScriptableObject
    {
        [SerializeField] private int titleSceneBuildIndex;
        [SerializeField] private StageDefinition defaultStage;
        [SerializeField] private StageDefinition[] stages=Array.Empty<StageDefinition>();
        [SerializeField] private int[] initialUnlockedUnitIds=Array.Empty<int>();
        public IReadOnlyList<int> InitialUnlockedUnitIds=>Array.AsReadOnly(initialUnlockedUnitIds);
        public int TitleSceneBuildIndex=>titleSceneBuildIndex;
        public StageDefinition DefaultStage=>defaultStage;
        public IReadOnlyList<StageDefinition> Stages=>Array.AsReadOnly(stages);
        public StageDefinition FirstStage=>stages.Where(stage=>stage!=null).OrderBy(stage=>stage.Chapter).ThenBy(stage=>stage.Number).FirstOrDefault() ?? defaultStage;
        public StageDefinition PreviousStage(StageDefinition stage)=>stage==null?null:stages.Where(candidate=>candidate!=null
            && (candidate.Chapter<stage.Chapter || candidate.Chapter==stage.Chapter && candidate.Number<stage.Number))
            .OrderByDescending(candidate=>candidate.Chapter).ThenByDescending(candidate=>candidate.Number).FirstOrDefault();
        public bool Contains(StageDefinition stage)=>stage!=null && (stage==defaultStage || Array.IndexOf(stages,stage)>=0);
        public bool TryGetStage(int id,out StageDefinition stage)
        {
            foreach(StageDefinition candidate in stages)
                if(candidate!=null && candidate.Id==id) { stage=candidate; return true; }
            stage=defaultStage!=null && defaultStage.Id==id?defaultStage:null;
            return stage!=null;
        }
    }
}
