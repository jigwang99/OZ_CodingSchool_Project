#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    /// <summary>Inspector에서 등록한 Addressables 리소스를 로컬 번들로 빌드.</summary>
    public static class AddressablesBuild
    {
        [MenuItem("PixelTactics/Build/Build Local Addressables")]
        public static void BuildLocal()
        {
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if(!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
            Debug.Log("ADDRESSABLES_BUILD_PASS "+result.Duration);
        }
    }
}
#endif
