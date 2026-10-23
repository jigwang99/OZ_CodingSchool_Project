#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoBattler.Presentation.Battle;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    /// <summary>기존 전투 프리팹의 한국어 기본 문구·폰트와 숨길 설명 연결.</summary>
    public static class BattleKoreanSetup
    {
        private static readonly Dictionary<string,string> Labels=new Dictionary<string,string>
        {
            {"Round --","라운드 --"},{"Gold --","골드 --"},{"Lives --","목숨 --"},{"Level --","레벨 --"},{"XP --","경험치 --"},{"Deployed --","배치 --"},
            {"Loading data...","불러오는 중..."},{"Start battle","전투 시작"},{"Retry load","다시 불러오기"},{"Refresh","새로고침"},{"Buy XP","경험치 구매"},
            {"Sell selected","판매"},{"Select a unit","기물 선택"},{"Battle result","전투 결과"},{"Victory / Defeat","승리 / 패배"},{"Next round","다음 라운드"},
            {"Restart Play to try again.",""},{"Unit","기물"},{"1 gold","1골드"},{"Pool 0",""},{"Empty","빈 상점"},
            {"SYNERGIES","시너지"},{"ORIGINS","종족"},{"CLASSES","직업"},{"Your deployed army",""},{"Synergy data is unavailable.","시너지 정보가 없음."},
            {"Loading synergies...","시너지 불러오는 중..."},{"Deploy units to discover synergies.","기물을 배치하면 시너지 표시."},
            {"AUGMENT","증강"},{"Description","설명"},{"CLICK TO SELECT",""},{"CHOOSE AN AUGMENT","증강 선택"},
            {"Choose one. Its benefits last for this challenge.",""},{"Hide choices","증강 닫기"},{"Show choices","증강 열기"},
            {"AUGMENTS 0/3","증강 0/3"},{"No augments selected yet.","선택한 증강이 없습니다."},
            {"Unit name","기물 이름"},{"Ally   *   Cost 1G","아군   *   1골드"},{"Origin / Class","종족 / 직업"},{"Stats","능력치"},{"Skill","스킬"},{"Skill details","스킬 정보"}
        };
        [MenuItem("PixelTactics/Setup/Connect Battle Korean UI")]
        public static void Connect()
        {
            var setup=ProjectSetupSettings.Current;
            UnitInfoSetup.Connect();
            var font=setup.UnitInfoFont;
            // 실제 표시하는 한글 글리프를 미리 준비하여 빌드와 에디터에서 동일하게 표시.
            string characters=string.Concat(Directory.GetFiles("Assets/_Scripts","*.cs",SearchOption.AllDirectories)
                .Where(path=>!path.Contains("Editor")).Select(File.ReadAllText));
            characters=new string(characters.Where(character=>character>='가' && character<='힣' || character>=' ' && character<='~' || character=='★').Distinct().ToArray());
            font.TryAddCharacters(characters,out string missing);
            if(!string.IsNullOrEmpty(missing)) throw new System.InvalidOperationException("전투 한글 폰트에 필요한 글리프 누락: "+missing);
            EditorUtility.SetDirty(font);
            var paths=new[]{setup.ShopItemPath,setup.ResultPath,setup.HealthBarPath,setup.SynergyPanelPath,
                setup.AugmentCardPath,setup.AugmentPanelPath,setup.SelectedAugmentsPath,setup.UnitInfoPath,setup.HUDPath};
            foreach(var path in paths)
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(var label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        label.font=font;
                        if(Labels.TryGetValue(label.text,out var korean)) label.text=korean;
                        if(label.text.StartsWith("Duplicates ") || label.text.Contains("Buffs lock at battle start.")) label.text=string.Empty;
                        if(string.IsNullOrEmpty(label.text) && (label.name=="Status" || label.name=="Rules" || label.name=="PoolText" || label.name=="Subtitle" || label.name=="Hint"))
                            label.gameObject.SetActive(false);
                    }
                    var item=root.GetComponent<ShopItem>();
                    if(item!=null)
                    {
                        item.PoolText.text=string.Empty; item.PoolText.gameObject.SetActive(false);
                        item.NameText.rectTransform.anchoredPosition=new Vector2(35f,19f);
                        item.PriceText.rectTransform.anchoredPosition=new Vector2(35f,-18f);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach(var entry in setup.Units)
            {
                var path=setup.UnitPath(entry); var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var identity=root.GetComponent<UnitIdentityView>();
                    if(identity==null) identity=root.AddComponent<UnitIdentityView>();
                    identity.ConfigureFont(font); PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets(); Debug.Log("BATTLE_KOREAN_SETUP_PASS");
        }
    }
}
#endif
