using UnityEngine;
using UnityEngine.AddressableAssets;
using System;
using System.Collections.Generic;

namespace AutoBattler.Run
{
    /// <summary>선택 화면의 스테이지 정의. 내부 전투 라운드와 구분.</summary>
    [CreateAssetMenu(menuName="PixelTactics/Stage Definition")]
    public sealed class StageDefinition : ScriptableObject
    {
        [SerializeField] private int id=1;
        [SerializeField] private string displayName="1-1";
        [SerializeField,Min(1)] private int chapter=1;
        [SerializeField,Min(1)] private int number=1;
        [SerializeField] private string regionName="마을 광장";
        [SerializeField] private string locationName="광장 입구";
        [SerializeField,TextArea] private string description;
        [SerializeField,Min(2)] private int roundsPerSegment=5;
        [SerializeField] private int[] plannedUnlockUnitIds=Array.Empty<int>();
        [SerializeField] private int sceneBuildIndex=1;
        [SerializeField] private AssetReferenceSprite battleBackground;
        [SerializeField] private AssetReferenceSprite benchFloor;
        public int Id=>id;
        public string DisplayName=>displayName;
        public int Chapter=>chapter;
        public int Number=>number;
        public string RegionName=>regionName;
        public string LocationName=>locationName;
        public string Description=>description;
        public int RoundsPerSegment=>roundsPerSegment;
        public int TotalRounds=>roundsPerSegment*3;
        public IReadOnlyList<int> PlannedUnlockUnitIds=>Array.AsReadOnly(plannedUnlockUnitIds);
        public int SceneBuildIndex=>sceneBuildIndex;
        public AssetReferenceSprite BattleBackground=>battleBackground;
        public AssetReferenceSprite BenchFloor=>benchFloor;
    }
}
