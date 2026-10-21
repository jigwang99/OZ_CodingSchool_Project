using UnityEngine;

namespace AutoBattler.Run
{
    /// <summary>빌드에 등록한 장면 인덱스와 선택 가능한 스테이지 참조.</summary>
    [CreateAssetMenu(menuName="PixelTactics/Game Navigation Settings")]
    public sealed class GameNavigationSettings : ScriptableObject
    {
        [SerializeField] private int titleSceneBuildIndex;
        [SerializeField] private StageDefinition defaultStage;
        public int TitleSceneBuildIndex=>titleSceneBuildIndex;
        public StageDefinition DefaultStage=>defaultStage;
        public bool Contains(StageDefinition stage)=>stage!=null && stage==defaultStage;
#if UNITY_EDITOR
        public void Configure(int titleIndex,StageDefinition stage)
        { titleSceneBuildIndex=titleIndex; defaultStage=stage; }
#endif
    }
}
