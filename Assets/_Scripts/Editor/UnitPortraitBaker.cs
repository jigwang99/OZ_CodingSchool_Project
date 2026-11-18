#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AutoBattler.EditorTools
{
    /// <summary>유닛 프리팹의 대기 모습을 투명 스프라이트로 저장하고 Addressables 카탈로그에 연결.</summary>
    public static class UnitPortraitBaker
    {
        public const string Folder="Assets/_Art/UI/UnitPortraits";
        [MenuItem("PixelTactics/Assets/Bake Unit Portraits")]
        public static void Bake()=>Bake(false);
        public static void BakeMissing()=>Bake(true);
        private static void Bake(bool onlyMissing)
        {
            ProjectAssetReferences references=ProjectAssetReferences.Current;
            // 현재 편집 중인 장면을 보존한 채 별도 미리보기 장면에서 렌더링.
            UnityEngine.SceneManagement.Scene scene=EditorSceneManager.NewPreviewScene();
            GameObject cameraRoot=new GameObject("UnitPortraitCamera",typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraRoot,scene);
            Camera camera=cameraRoot.GetComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
            camera.backgroundColor=Color.clear;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.cullingMask=1<<31;
            camera.allowHDR=false;camera.allowMSAA=false;camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            Material material=new Material(Shader.Find("Sprites/Default"));
            try
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
                foreach(UnitPrefabReference entry in references.Units.OrderBy(unit=>unit.VisualId))
                {
                    if(onlyMissing && File.Exists($"{Folder}/Unit_{entry.VisualId:000}.png")) continue;
                    GameObject unit=UnityEngine.Object.Instantiate(entry.SourcePrefab);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(unit,scene);
                    try
                    {
                        unit.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);unit.transform.localScale=Vector3.one;
                        foreach(Transform child in unit.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=31;
                        foreach(Animator animator in unit.GetComponentsInChildren<Animator>(true))
                            if(animator.runtimeAnimatorController!=null) {animator.Rebind();animator.Update(0f);animator.enabled=false;}
                        SpriteRenderer[] sprites=unit.GetComponentsInChildren<SpriteRenderer>().Where(sprite=>sprite.enabled && sprite.sprite!=null).ToArray();
                        Bounds bounds=new Bounds(sprites[0].transform.TransformPoint(sprites[0].sprite.bounds.center),Vector3.zero);
                        foreach(SpriteRenderer sprite in sprites)
                        {
                            Bounds local=sprite.sprite.bounds;
                            foreach(float x in new[]{local.min.x,local.max.x}) foreach(float y in new[]{local.min.y,local.max.y})
                                bounds.Encapsulate(sprite.transform.TransformPoint(new Vector3(x,y,0)));
                            sprite.sharedMaterial=material;
                        }
                        camera.orthographicSize=Mathf.Max(4f,bounds.size.magnitude*2f);
                        camera.transform.position=new Vector3(0f,0f,-10f);
                        camera.aspect=1f;camera.ResetWorldToCameraMatrix();camera.ResetProjectionMatrix();
                        RenderTexture target=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
                        Texture2D pixels=new Texture2D(256,256,TextureFormat.RGBA32,false);
                        RenderTexture previous=RenderTexture.active;
                        try
                        {
                            // SPUM 파츠의 투명 여백을 제외한 실제 렌더 영역으로 카메라를 맞춤.
                            for(int pass=0;pass<3;pass++)
                            {
                                camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();
                                Color32[] colors=pixels.GetPixels32();int minX=256,minY=256,maxX=-1,maxY=-1;
                                for(int y=0;y<256;y++) for(int x=0;x<256;x++) if(colors[y*256+x].a>0)
                                {minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
                                if(maxX<0) throw new System.Exception("유닛 모습 렌더 실패: "+entry.VisualId);
                                if(pass==2) break;
                                Vector3 center=camera.ViewportToWorldPoint(new Vector3((minX+maxX+1)*.5f/256f,(minY+maxY+1)*.5f/256f,10f));
                                camera.transform.position=new Vector3(center.x,center.y,-10f);
                                camera.orthographicSize*=Mathf.Max(maxX-minX+1,maxY-minY+1)*1.12f/256f;
                                camera.ResetWorldToCameraMatrix();camera.ResetProjectionMatrix();
                            }
                            string path=$"{Folder}/Unit_{entry.VisualId:000}.png";File.WriteAllBytes(path,pixels.EncodeToPNG());
                            AssetDatabase.ImportAsset(path);
                            TextureImporter importer=(TextureImporter)AssetImporter.GetAtPath(path);
                            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;
                            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=256;importer.SaveAndReimport();
                        }
                        finally {RenderTexture.active=previous;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
                    }
                    finally {UnityEngine.Object.DestroyImmediate(unit);}
                }
                AddressableAssetSettings settings=AddressableAssetSettingsDefaultObject.Settings;
                AddressableAssetGroup group=settings.FindGroup("UnitPortraits") ?? settings.CreateGroup("UnitPortraits",false,false,false,
                    new List<AddressableAssetGroupSchema>{references.ItemIconGroup.GetSchema<BundledAssetGroupSchema>(),references.ItemIconGroup.GetSchema<ContentUpdateGroupSchema>()});
                SerializedObject fields=new SerializedObject(references.Catalog);SerializedProperty portraits=fields.FindProperty("portraits");portraits.arraySize=references.Units.Count;
                int index=0;
                foreach(UnitPrefabReference unit in references.Units.OrderBy(unit=>unit.VisualId))
                {
                    string guid=AssetDatabase.AssetPathToGUID($"{Folder}/Unit_{unit.VisualId:000}.png");
                    settings.CreateOrMoveEntry(guid,group).address=$"unit-portrait/{unit.VisualId}";
                    SerializedProperty entry=portraits.GetArrayElementAtIndex(index++);entry.FindPropertyRelative("visualId").intValue=unit.VisualId;
                    entry.FindPropertyRelative("sprite").FindPropertyRelative("m_AssetGUID").stringValue=guid;
                }
                fields.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(references.Catalog);
                EditorUtility.SetDirty(group);EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(group);AssetDatabase.SaveAssetIfDirty(settings);
                foreach(AddressableAssetGroupSchema schema in group.Schemas) AssetDatabase.SaveAssetIfDirty(schema);
                Debug.Log("UNIT_PORTRAITS_BAKED "+index);
            }
            finally {UnityEngine.Object.DestroyImmediate(cameraRoot);UnityEngine.Object.DestroyImmediate(material);EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
#endif
