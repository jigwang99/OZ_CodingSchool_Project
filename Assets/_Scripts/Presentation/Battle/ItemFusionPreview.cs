namespace AutoBattler.Presentation.Battle
{
    /// <summary>공용 아이템 패널을 사용한 합성 결과 표시.
    /// 기존 프리팹의 참조와 직렬화 필드 유지.</summary>
    public sealed class ItemFusionPreview : ItemTooltip
    {
        public int ResultItemId => DefinitionId;
    }
}
