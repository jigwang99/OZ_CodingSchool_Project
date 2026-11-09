using UnityEngine.AddressableAssets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Data.Snapshot;
using AutoBattler.Presentation.Units;
using AutoBattler.Run;
using AutoBattler.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Managers
{
    /// <summary>한 도전의 데이터·프리팹 로드, 유닛·이펙트 풀과 에셋 핸들 수명 관리.</summary>
    [DisallowMultipleComponent]
    public sealed class AssetManager : Singleton<AssetManager>
    {
        private AssetService assets;
        private AssetService.Lease<TextAsset> snapshot;
        private AssetService.Lease<Sprite> background;
        private AssetService.Lease<Sprite> benchFloor;
        private readonly Dictionary<int,AssetService.Lease<Sprite>> itemIcons=new Dictionary<int,AssetService.Lease<Sprite>>();
        private readonly Dictionary<int,AssetService.Lease<Sprite>> unitPortraits=new Dictionary<int,AssetService.Lease<Sprite>>();
        public int ItemIconHandleCount=>itemIcons.Count;
        public Sprite ItemIcon(int id)=>itemIcons.TryGetValue(id,out AssetService.Lease<Sprite> lease)?lease.Asset:null;
        public int UnitPortraitHandleCount=>unitPortraits.Count;
        public bool IsLoadingUnitPortraits { get; private set; }
        public Sprite UnitPortrait(int visualId)=>unitPortraits.TryGetValue(visualId,out AssetService.Lease<Sprite> lease)?lease.Asset:null;
        private AssetCatalog catalog;
        private BattleFlowController flow;
        private HexGridLayout grid;
        private UniTaskCompletionSource shutdown;
        public DataRepository Data { get; private set; }
        public UnitPrefabPool Units { get; private set; }
        public BattleVisualService Visuals { get; private set; }
        public int ActiveHandleCount=>assets?.ActiveHandleCount ?? 0;
        public Sprite StageBackground=>background?.Asset;
        public Sprite StageBenchFloor=>benchFloor?.Asset;
        public void Initialize(AssetCatalog configuration,BattleFlowController phases,HexGridLayout layout,int poolSize)
        {
            if(assets!=null) return;
            catalog=configuration; flow=phases; grid=layout; assets=new AssetService();
            GameObject root=new GameObject("UnitPool"); root.transform.SetParent(transform,false);
            Units=new UnitPrefabPool(assets,root.transform,poolSize);
        }
        public async UniTask LoadAsync(CancellationToken token,IProgress<float> progress=null,int stageId=1)
        {
            token.ThrowIfCancellationRequested();
            if(shutdown!=null) throw new ObjectDisposedException(nameof(AssetManager));
            if(snapshot==null) snapshot=await assets.AcquireAsync<TextAsset>(catalog.Snapshot,token);
            DataRepository repository=new DataRepository(JsonUtility.FromJson<GameSnapshot>(snapshot.Asset.text),stageId);
            progress?.Report(.1f);
            AssetReferenceGameObject[] prefabs=repository.Units.Values.Select(unit=>catalog.VisualPrefab(unit.VisualId))
                .Concat(new[]{catalog.TrainingDummy})
                .GroupBy(reference=>reference.RuntimeKey).Select(group=>group.First()).ToArray();
            for(int i=0;i<prefabs.Length;i++)
            {
                await Units.PreloadAsync(prefabs[i],token);
                progress?.Report(.1f+.5f*(i+1)/prefabs.Length);
            }
            int[] icons=repository.Items.Values.Select(item=>item.IconId).Distinct().ToArray();
            for(int i=0;i<icons.Length;i++)
            {
                int iconId=icons[i];
                if(!itemIcons.ContainsKey(iconId)) itemIcons.Add(iconId,await assets.AcquireAsync<Sprite>(catalog.ItemIconReference(iconId),token));
                progress?.Report(.6f+.15f*(i+1)/icons.Length);
            }
            // 배치툴을 처음 열기 전에 모든 유닛 모습을 준비하고 도전 종료까지 캐시 유지.
            int[] visualIds=repository.Units.Values.Select(unit=>unit.VisualId).Distinct().OrderBy(id=>id).ToArray();
            IsLoadingUnitPortraits=true;
            try
            {
                for(int index=0;index<visualIds.Length;index++)
                {
                    int visualId=visualIds[index];
                    if(!unitPortraits.ContainsKey(visualId))
                    {
                        AssetService.Lease<Sprite> lease=await assets.AcquireAsync<Sprite>(catalog.PortraitReference(visualId),token);
                        try
                        {
                            token.ThrowIfCancellationRequested();
                            if(shutdown!=null) throw new ObjectDisposedException(nameof(AssetManager));
                            unitPortraits.Add(visualId,lease);
                        }
                        catch {lease.Dispose();throw;}
                    }
                    progress?.Report(.75f+.15f*(index+1)/visualIds.Length);
                }
            }
            finally {IsLoadingUnitPortraits=false;}
            if(Visuals==null) Visuals=new BattleVisualService(assets,catalog.BattleVisuals,transform,flow,grid);
            await Visuals.PreloadAsync(token); token.ThrowIfCancellationRequested(); Data=repository; progress?.Report(1f);
        }
        public UniTask ShutdownAsync()
        {
            if(shutdown!=null) return shutdown.Task;
            shutdown=new UniTaskCompletionSource(); ShutdownCoreAsync(shutdown).Forget(); return shutdown.Task;
        }
        public async UniTask LoadBackgroundAsync(StageDefinition stage,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(background!=null || stage?.BattleBackground==null || !stage.BattleBackground.RuntimeKeyIsValid()) return;
            AssetService.Lease<Sprite> lease=await assets.AcquireAsync<Sprite>(stage.BattleBackground,token);
            try
            {
                token.ThrowIfCancellationRequested();
                if(shutdown!=null) throw new ObjectDisposedException(nameof(AssetManager));
                background=lease;
            }
            catch { lease.Dispose(); throw; }
        }
        public async UniTask LoadBenchFloorAsync(StageDefinition stage,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(benchFloor!=null || stage?.BenchFloor==null || !stage.BenchFloor.RuntimeKeyIsValid()) return;
            AssetService.Lease<Sprite> lease=await assets.AcquireAsync<Sprite>(stage.BenchFloor,token);
            try
            {
                token.ThrowIfCancellationRequested();
                if(shutdown!=null) throw new ObjectDisposedException(nameof(AssetManager));
                benchFloor=lease;
            }
            catch { lease.Dispose(); throw; }
        }
        private async UniTask ShutdownCoreAsync(UniTaskCompletionSource completion)
        {
            try
            {
                if(Visuals!=null) await Visuals.DisposeAsync();
                if(Units!=null) await Units.DisposeAsync();
                foreach(AssetService.Lease<Sprite> icon in itemIcons.Values) icon.Dispose(); itemIcons.Clear();
                foreach(AssetService.Lease<Sprite> portrait in unitPortraits.Values) portrait.Dispose(); unitPortraits.Clear();
                background?.Dispose(); background=null;
                benchFloor?.Dispose(); benchFloor=null;
                snapshot?.Dispose(); snapshot=null; assets?.Dispose(); Data=null;
                completion.TrySetResult();
            }
            catch(Exception exception) { completion.TrySetException(exception); }
        }
        protected override void OnDestroy() { base.OnDestroy(); ShutdownAsync().Forget(); }
    }
}
