using AutoBattler.Data.Skills;
using AutoBattler.Data.Snapshot;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>해금 미리보기에서 1성 기본 수치로 공통 스킬 설명 생성.</summary>
    public static class SkillPreviewText
    {
        public static string Describe(UnitDefinition unit,SkillDefinition skill,DataRepository data)
            =>SkillDescriptionText.Describe(skill,data,unit.Attack,unit.Hp);
    }
}
