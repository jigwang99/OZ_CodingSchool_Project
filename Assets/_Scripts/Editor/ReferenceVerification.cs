#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    public static class ReferenceVerification
    {
        [MenuItem("PixelTactics/Checks/Run Reference Build and Play Checks")]
        public static void BuildAndPlay()
        {
            var setup=ProjectSetupSettings.Current;
            string before=setup.HUDPath;
            string moved=Path.Combine(Path.GetDirectoryName(before),"ReferenceProbeHUD.prefab").Replace('\\','/');
            string guid=AssetDatabase.AssetPathToGUID(before);
            try
            {
                string error=AssetDatabase.MoveAsset(before,moved);
                if(!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                if(setup.HUDPath!=moved || AssetDatabase.AssetPathToGUID(setup.HUDPath)!=guid)
                    throw new InvalidOperationException("HUD 이름 변경 후 SO 참조 유지 실패.");
                ShopItemSetup.UpgradeBattleHUD();
            }
            finally
            {
                if(AssetDatabase.GUIDToAssetPath(guid)!=before)
                {
                    string error=AssetDatabase.MoveAsset(AssetDatabase.GUIDToAssetPath(guid),before);
                    if(!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                }
            }
            CharacterPrefabSetup.Connect();
            CharacterPrefabSetup.CheckPrefabs();
            string hudBefore=File.ReadAllText(setup.HUDPath);
            ShopItemSetup.UpgradeBattleHUD(); ReusableUISetup.ConnectPrefabs();
            if(hudBefore!=File.ReadAllText(setup.HUDPath))
                throw new InvalidOperationException("재연결 시 HUD 레이아웃 변경 발생.");
            BattleHUDSetup.BuildForVerification();
            Debug.Log("REFERENCE_SETUP_PASS asset-rename-layout-idempotent");

            // 표시 주소를 변경한 번들에서도 GUID로 로드되는지 확인. 설정 주소는 즉시 복원.
            var settings=setup.UnitGroup.Settings;
            var entries=setup.UnitGroup.entries.Concat(setup.DataGroup.entries).ToArray();
            var addresses=entries.Select(entry=>entry.address).ToArray();
            try
            {
                for(int i=0;i<entries.Length;i++) entries[i].address="ReferenceProbe_"+i;
                EditorUtility.SetDirty(setup.UnitGroup); EditorUtility.SetDirty(setup.DataGroup);
                AssetDatabase.SaveAssets(); MilestoneSetup.BuildLocal();
            }
            finally
            {
                for(int i=0;i<entries.Length;i++) entries[i].address=addresses[i];
                EditorUtility.SetDirty(setup.UnitGroup); EditorUtility.SetDirty(setup.DataGroup);
                AssetDatabase.SaveAssets();
            }
            Debug.Log("REFERENCE_ADDRESS_BUILD_PASS renamed-addresses-guid-catalog");
            settings.ActivePlayModeDataBuilderIndex=2;
            MilestonePlayChecks.Start();
        }
    }
}
#endif
