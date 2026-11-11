using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Data.Snapshot;
using AutoBattler.Managers;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    public sealed class UnitCodexView : MonoBehaviour
    {
        [SerializeField] private AssetCatalog catalog;
        [SerializeField] private Button closeButton;
        [SerializeField] private ScrollRect roster;
        [SerializeField] private UnitCodexEntry rowPrefab;
        [SerializeField] private StageUnlockUnitView detail;
        [SerializeField] private TextMeshProUGUI status;
        private readonly Dictionary<int,UnitCodexEntry> entries=new Dictionary<int,UnitCodexEntry>();
        private AssetService assets;
        private AssetService.Lease<Sprite> portrait;
        private CancellationTokenSource openLifetime, selectionLifetime;
        private DataRepository data;
        private SaveManager saves;
        private int selectedId;

        public Button CloseButton=>closeButton;
        public bool IsOpen=>gameObject.activeSelf;
        public void Bind(SaveManager progress)=>saves=progress;

        public void Open()
        {
            if(IsOpen) return;
            gameObject.SetActive(true);
            assets=new AssetService();
            openLifetime=new CancellationTokenSource();
            LoadAsync(assets,openLifetime.Token).Forget();
        }
        public void Close()=>gameObject.SetActive(false);

        private async UniTask LoadAsync(AssetService source,CancellationToken token)
        {
            status.text="유닛 정보를 불러오는 중…";
            try
            {
                if(data==null)
                {
                    using AssetService.Lease<TextAsset> lease=await source.AcquireAsync<TextAsset>(catalog.Snapshot,token);
                    token.ThrowIfCancellationRequested();
                    data=new DataRepository(JsonUtility.FromJson<GameSnapshot>(lease.Asset.text));
                }
                token.ThrowIfCancellationRequested();
                UnitDefinition[] definitions=data.Units.Values.OrderBy(unit=>unit.Cost).ThenBy(unit=>unit.Id).ToArray();
                if(entries.Count==0)
                    foreach(UnitDefinition unit in definitions)
                    {
                        UnitCodexEntry row=Instantiate(rowPrefab,roster.content);
                        row.Bind(unit,catalog.Icon(unit.IconId),Select);
                        entries.Add(unit.Id,row);
                    }
                status.text="";
                LayoutRebuilder.ForceRebuildLayoutImmediate(roster.content);
                roster.verticalNormalizedPosition=1f;
                if(definitions.Length>0) Select(definitions[0]);
            }
            catch(OperationCanceledException) { }
            catch(Exception exception)
            {
                if(token.IsCancellationRequested) return;
                status.text="유닛 정보를 불러올 수 없습니다.";
                Debug.LogWarning(exception.Message,this);
            }
        }
        private void Select(UnitDefinition unit)
        {
            if(selectedId==unit.Id) return;
            CancelSelection();
            if(entries.TryGetValue(selectedId,out UnitCodexEntry previous)) previous.SetSelected(false);
            selectedId=unit.Id; entries[selectedId].SetSelected(true);
            detail.Show(unit,data,catalog.BattleVisuals.Skill(unit.SkillId)?.icon);
            detail.SetUnlocked(saves!=null && saves.IsUnitUnlocked(unit.Id));
            selectionLifetime=CancellationTokenSource.CreateLinkedTokenSource(openLifetime.Token);
            LoadPortraitAsync(unit,assets,selectionLifetime.Token).Forget();
        }
        private async UniTask LoadPortraitAsync(UnitDefinition unit,AssetService source,CancellationToken token)
        {
            AssetService.Lease<Sprite> lease=null;
            try
            {
                lease=await source.AcquireAsync<Sprite>(catalog.PortraitReference(unit.VisualId),token);
                token.ThrowIfCancellationRequested();
                portrait=lease; lease=null;
                detail.SetPortrait(portrait.Asset);
            }
            catch(OperationCanceledException) { }
            catch(Exception exception) { if(!token.IsCancellationRequested) Debug.LogWarning(exception.Message,this); }
            finally {lease?.Dispose();}
        }
        private void CancelSelection()
        {
            selectionLifetime?.Cancel(); selectionLifetime?.Dispose(); selectionLifetime=null;
            detail.Clear();
            portrait?.Dispose(); portrait=null;
        }
        private void OnDisable()
        {
            CancelSelection();
            if(entries.TryGetValue(selectedId,out UnitCodexEntry row)) row.SetSelected(false);
            selectedId=0;
            openLifetime?.Cancel(); openLifetime?.Dispose(); openLifetime=null;
            assets?.Dispose(); assets=null;
        }
    }
}
