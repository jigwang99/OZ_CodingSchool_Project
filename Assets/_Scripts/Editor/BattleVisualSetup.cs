#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using AutoBattler.Assets;
using AutoBattler.Presentation.Battle;
using AutoBattler.Presentation.Units;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace AutoBattler.EditorTools
{
    /// <summary>선정 에셋의 프로젝트용 프리팹·URP 재질·직접 참조 초기 연결.</summary>
    public static class BattleVisualSetup
    {
        private const string Folder="Assets/_Prefabs/VFX";
        private const string SettingsPath="Assets/_Data/BattleVisualSettings.asset";
        private const string Icons="Assets/SPUM/Retro UI Set/1_UI_Images/Theme2/";
        [MenuItem("PixelTactics/Setup/Connect Battle Effects and Icons")]
        public static void Connect()
        {
            var setup=ProjectSetupSettings.Current;
            if(setup.BattleVisuals==null)
            {
                EnsureFolder(Folder); EnsureFolder(Folder+"/Materials");
                var settings=AssetDatabase.LoadAssetAtPath<BattleVisualSettings>(SettingsPath);
                if(settings==null) { settings=ScriptableObject.CreateInstance<BattleVisualSettings>(); AssetDatabase.CreateAsset(settings,SettingsPath); }
                // 최초 기본값 연결 이후에는 SO의 AssetReference와 아이콘 참조 사용.
                string[] sources={"Style_06/Effect_06","Style_42/Effect_042","Style_43/Effect_43","Style_41/Effect_041","Style_23/Effect_23","Style_38/Effect_38"};
                int[] capacity={32,24,8,12,20,8,32,20};
                float[] scale={.15f,.16f,.38f,.12f,.13f,.32f,1f,1f};
                var entries=new BattleVisualEntry[8];
                for(int i=0;i<entries.Length;i++)
                {
                    var id=(BattleVisualId)i;
                    string path=Folder+"/"+id+".prefab";
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if(prefab==null) prefab=i<6 ? CreateParticle(id,"Assets/HitEffectPack/Prefabs/"+sources[i]+".prefab",path)
                        : CreateSprite(id,path);
                    entries[i]=new BattleVisualEntry {id=id,prefab=new UnityEngine.AddressableAssets.AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(path)),
                        capacity=capacity[i],scale=scale[i],lifetime=i==6?12f:1.5f};
                }
                var skills=new[]{
                    Skill(2001,28,BattleVisualId.Shield),Skill(2002,17,BattleVisualId.Hit),Skill(2003,29,BattleVisualId.Heal),
                    Skill(2004,16,BattleVisualId.Hit),Skill(2005,13,BattleVisualId.Spin,true),Skill(2006,11,BattleVisualId.Hit),
                    Skill(2007,1,BattleVisualId.Fire,true,true),Skill(2008,26,BattleVisualId.Hit)};
                settings.Configure(entries,skills,AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SPUM/Retro UI Set/1_UI_Images/Theme1/02_Icons/0_Flat/Icon_Flat__43.png"),
                    CreateOrb(),Icon(26),Icon(24),Icon(25),Icon(28));
                var addressables=setup.UnitGroup.Settings;
                var group=addressables.FindGroup("BattleVFX") ?? addressables.CreateGroup("BattleVFX",false,false,false,null,
                    typeof(BundledAssetGroupSchema),typeof(ContentUpdateGroupSchema));
                group.GetSchema<BundledAssetGroupSchema>().BundleMode=BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                foreach(var entry in entries) MilestoneSetup.AddAddress(addressables,group,AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(entry.prefab.AssetGUID)));
                setup.SetBattleVisuals(settings,group); setup.Catalog.ConfigureBattleVisuals(settings);
                int[] unitIcons={15,17,19,16,13,11,1,21};
                setup.Catalog.icons=unitIcons.Select((icon,index)=>new UnitIconEntry {id=index+1,sprite=Icon(icon)}).ToArray();
                EditorUtility.SetDirty(settings); EditorUtility.SetDirty(setup); EditorUtility.SetDirty(setup.Catalog); EditorUtility.SetDirty(addressables);
            }
            ConnectInfo(); AssetDatabase.SaveAssets();
            Debug.Log("BATTLE_VISUAL_SETUP_PASS 8 pools, 8 unit icons, 8 skill icons");
        }
        private static SkillVisualEntry Skill(int id,int icon,BattleVisualId impact,bool area=false,bool magic=false)
        { return new SkillVisualEntry {skillId=id,icon=Icon(icon),impact=impact,areaImpact=area,magicProjectile=magic,
            projectileColor=magic?new Color(1f,.35f,.08f):Color.white}; }
        private static Sprite Icon(int id)
        {
            string category=id<=11?"01_Properties/":id<=21?"02_Job/":"03_Status/";
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Icons+category+"Spum_Icon"+id+".png");
            if(sprite==null) throw new InvalidOperationException("SPUM 아이콘 연결 필요: "+id);
            return sprite;
        }
        private static void EnsureFolder(string path)
        { if(!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path)); }
        private static GameObject CreateParticle(BattleVisualId id,string source,string path)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if(prefab==null) throw new InvalidOperationException("HitEffectPack 원본 연결 필요: "+source);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                root.name=id.ToString(); root.SetActive(false); root.transform.rotation=Quaternion.Euler(-90f,0f,0f);
                var systems=root.GetComponentsInChildren<ParticleSystem>(true);
                foreach(var system in systems)
                {
                    system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=system.main; main.stopAction=ParticleSystemStopAction.None; main.playOnAwake=false; main.loop=false;
                    // 프리팹 축소와 함께 입자 크기·속도·방출 영역도 축소.
                    main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                }
                foreach(var renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(ConvertMaterial).ToArray();
                    renderer.sortingOrder=id==BattleVisualId.Fire || id==BattleVisualId.Spin?-10:20;
                }
                root.AddComponent<PooledBattleVisual>().Configure(id,systems,null);
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static Material ConvertMaterial(Material source)
        {
            if(source==null) return null;
            string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
            string path=Folder+"/Materials/"+source.name+"_"+guid.Substring(0,8)+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null) return material;
            material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetTexture("_BaseMap",source.HasProperty("_MainTex")?source.GetTexture("_MainTex"):null);
            if(source.HasProperty("_MainTex"))
            { material.SetTextureScale("_BaseMap",source.GetTextureScale("_MainTex")); material.SetTextureOffset("_BaseMap",source.GetTextureOffset("_MainTex")); }
            var tint=source.HasProperty("_TintColor")?source.GetColor("_TintColor"):source.HasProperty("_Color")?source.GetColor("_Color"):Color.white;
            // 가산 파티클의 밝기를 URP에서도 유지. 투명도는 원본 값 유지.
            material.SetColor("_BaseColor",new Color(tint.r*2f,tint.g*2f,tint.b*2f,tint.a));
            material.SetFloat("_Surface",1f); material.SetFloat("_Blend",2f); material.SetFloat("_Cull",0f);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend",(float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha",(float)BlendMode.One); material.SetFloat("_DstBlendAlpha",(float)BlendMode.One);
            material.SetFloat("_ZWrite",0f); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue=3000;
            AssetDatabase.CreateAsset(material,path); return material;
        }
        private static GameObject CreateSprite(BattleVisualId id,string path)
        {
            string materialPath=Folder+"/Materials/CombatSprite.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null)
            { material=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); AssetDatabase.CreateAsset(material,materialPath); }
            var root=new GameObject(id.ToString()); root.SetActive(false);
            try
            {
                var renderer=root.AddComponent<SpriteRenderer>(); renderer.sharedMaterial=material; renderer.sortingOrder=id==BattleVisualId.Stun?40:30;
                root.AddComponent<PooledBattleVisual>().Configure(id,Array.Empty<ParticleSystem>(),renderer);
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static Sprite CreateOrb()
        {
            string path=Folder+"/MagicOrb.png";
            if(!File.Exists(path)) File.Copy("Assets/HitEffectPack/Textures/glowCircle_0.png",path);
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static void ConnectInfo()
        {
            var setup=ProjectSetupSettings.Current; var root=PrefabUtility.LoadPrefabContents(setup.UnitInfoPath);
            try
            {
                var panel=root.GetComponent<UnitInfoPanel>(); if(panel.UnitIcon!=null) return;
                var unit=Image(root.transform,"UnitIcon",new Vector2(22,-22),48,null);
                panel.NameText.rectTransform.anchoredPosition=new Vector2(84,-22); panel.NameText.rectTransform.sizeDelta=new Vector2(314,38);
                panel.IdentityText.rectTransform.anchoredPosition=new Vector2(84,-66); panel.IdentityText.rectTransform.sizeDelta=new Vector2(314,25);
                Image(root.transform,"HPIcon",new Vector2(22,-143),24,setup.BattleVisuals.Health);
                Image(root.transform,"MPIcon",new Vector2(22,-204),24,setup.BattleVisuals.Mana);
                panel.HealthText.rectTransform.anchoredPosition=new Vector2(54,-143); panel.HealthText.rectTransform.sizeDelta=new Vector2(310,25);
                panel.ManaText.rectTransform.anchoredPosition=new Vector2(54,-204); panel.ManaText.rectTransform.sizeDelta=new Vector2(344,25);
                var shield=Image(root.transform,"ShieldIcon",new Vector2(374,-143),24,setup.BattleVisuals.Shield); shield.enabled=false;
                var skill=Image(root.transform,"SkillIcon",new Vector2(22,-512),32,null);
                panel.SkillNameText.rectTransform.anchoredPosition=new Vector2(66,-512); panel.SkillNameText.rectTransform.sizeDelta=new Vector2(332,32);
                panel.ConfigureIcons(setup.Catalog,unit,skill,shield);
                PrefabUtility.SaveAsPrefabAsset(root,setup.UnitInfoPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static Image Image(Transform parent,string name,Vector2 position,float size,Sprite sprite)
        {
            var root=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)); root.layer=parent.gameObject.layer;
            root.transform.SetParent(parent,false); var rect=(RectTransform)root.transform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1); rect.anchoredPosition=position; rect.sizeDelta=Vector2.one*size;
            var image=root.GetComponent<Image>(); image.sprite=sprite; image.preserveAspect=true; image.raycastTarget=false;
            return image;
        }
    }
}
#endif
