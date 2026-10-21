using UnityEngine;

namespace AutoBattler.Run
{
    /// <summary>선택 화면의 스테이지 정의. 내부 전투 라운드와 구분.</summary>
    [CreateAssetMenu(menuName="PixelTactics/Stage Definition")]
    public sealed class StageDefinition : ScriptableObject
    {
        [SerializeField] private int id=1;
        [SerializeField] private string displayName="1-1";
        [SerializeField,TextArea] private string description;
        [SerializeField] private int sceneBuildIndex=1;
        public int Id=>id;
        public string DisplayName=>displayName;
        public string Description=>description;
        public int SceneBuildIndex=>sceneBuildIndex;
#if UNITY_EDITOR
        public void Configure(int stageId,string name,string details,int buildIndex)
        { id=stageId; displayName=name; description=details; sceneBuildIndex=buildIndex; }
#endif
    }
}
