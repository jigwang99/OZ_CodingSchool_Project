using UnityEngine;
using UnityEngine.AddressableAssets;

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
        [SerializeField] private AssetReferenceSprite battleBackground;
        public int Id=>id;
        public string DisplayName=>displayName;
        public string Description=>description;
        public int SceneBuildIndex=>sceneBuildIndex;
        public AssetReferenceSprite BattleBackground=>battleBackground;
    }
}
