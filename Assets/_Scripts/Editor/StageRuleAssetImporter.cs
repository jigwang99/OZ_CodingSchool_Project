#if UNITY_EDITOR
using System;
using System.Linq;
using AutoBattler.Data.Snapshot;
using AutoBattler.Data.Stages;
using AutoBattler.Run;
using UnityEditor;

namespace AutoBattler.EditorTools
{
    /// <summary>시트의 장소·전투 길이·해금 계획을 미리 배치한 스테이지 SO에 연결.</summary>
    public static class StageRuleAssetImporter
    {
        public static StageDefinition[] Targets(GameSnapshot snapshot)
        {
            GameNavigationSettings navigation=ProjectAssetReferences.Current.Navigation;
            return snapshot.stages.Select(row=>navigation.TryGetStage(row.stageId,out StageDefinition stage)?stage
                :throw new FormatException($"Stages stage_id={row.stageId}: Navigation에 연결한 StageDefinition 필요.")).ToArray();
        }
        public static void Apply(GameSnapshot snapshot,StageDefinition[] targets)
        {
            // 시트의 해금 계획을 기준으로 기본 유닛 참조도 함께 갱신.
            int[] basic=new DataRepository(snapshot).InitialUnlockedUnitIds.ToArray();
            SerializedObject navigation=new SerializedObject(ProjectAssetReferences.Current.Navigation);
            SerializedProperty initial=navigation.FindProperty("initialUnlockedUnitIds");initial.arraySize=basic.Length;
            for(int i=0;i<basic.Length;i++) initial.GetArrayElementAtIndex(i).intValue=basic[i];
            navigation.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(navigation.targetObject);
            for(int i=0;i<snapshot.stages.Length;i++)
            {
                StageRow row=snapshot.stages[i]; StageDefinition stage=targets[i];
                SerializedObject fields=new SerializedObject(stage);
                fields.FindProperty("chapter").intValue=row.chapter;
                fields.FindProperty("number").intValue=row.number;
                fields.FindProperty("displayName").stringValue=$"{row.chapter}-{row.number}";
                fields.FindProperty("regionName").stringValue=row.regionName;
                fields.FindProperty("locationName").stringValue=row.locationName;
                fields.FindProperty("roundsPerSegment").intValue=row.roundsPerSegment;
                int[] ids=snapshot.stageUnlockPlans.Where(plan=>plan.stageId==row.stageId).Select(plan=>plan.unitId).ToArray();
                SerializedProperty plans=fields.FindProperty("plannedUnlockUnitIds"); plans.arraySize=ids.Length;
                for(int index=0;index<ids.Length;index++) plans.GetArrayElementAtIndex(index).intValue=ids[index];
                fields.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(stage);
            }
        }
    }
}
#endif
