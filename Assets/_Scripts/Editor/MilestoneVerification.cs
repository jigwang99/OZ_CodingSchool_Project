#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    public static class MilestoneVerification
    {
        public static void BuildAndPlay()
        {
            MilestoneChecks.RunAll();
            MilestoneSetup.BuildLocal();
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex=2;
            AssetDatabase.SaveAssets();
            MilestonePlayChecks.Start();
        }
    }
}
#endif
