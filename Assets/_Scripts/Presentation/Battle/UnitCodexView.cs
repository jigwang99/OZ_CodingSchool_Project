using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Data.Items;
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
        [SerializeField] private Button unitTabButton, itemTabButton;
        [SerializeField] private RectTransform itemDetail;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI itemName, itemStats;
        private readonly Dictionary<int,UnitCodexEntry> entries=new Dictionary<int,UnitCodexEntry>();
        private readonly Dictionary<int,UnitCodexEntry> itemEntries=new Dictionary<int,UnitCodexEntry>();
        private readonly Dictionary<int,AssetService.Lease<Sprite>> itemIcons=new Dictionary<int,AssetService.Lease<Sprite>>();
        private AssetService assets;
        private AssetService.Lease<Sprite> portrait;
        private CancellationTokenSource openLifetime, selectionLifetime;
        private DataRepository data;
        private SaveManager saves;
        private UnitDefinition[] units;
        private ItemDefinition[] items;
        private UnitCodexEntry selectedEntry;
        private ItemDefinition selectedItem;
        private bool showingItems, itemIconsRequested;

        public Button CloseButton=>closeButton;
        public bool IsOpen=>gameObject.activeSelf;
        public void Bind(SaveManager progress)=>saves=progress;

        private void Awake()
        {
            unitTabButton.onClick.AddListener(ShowUnits);
            itemTabButton.onClick.AddListener(ShowItems);
        }

        public void Open()
        {
            if(IsOpen) return;
            assets=new AssetService();
            openLifetime=new CancellationTokenSource();
            showingItems=false;
            gameObject.SetActive(true);
            UpdateTabs();
            LoadAsync(assets,openLifetime.Token).Forget();
        }
        public void Close()=>gameObject.SetActive(false);

        private async UniTask LoadAsync(AssetService source,CancellationToken token)
        {
            status.text="도감 정보를 불러오는 중…";
            try
            {
                if(data==null)
                {
                    using AssetService.Lease<TextAsset> lease=await source.AcquireAsync<TextAsset>(catalog.Snapshot,token);
                    token.ThrowIfCancellationRequested();
                    data=new DataRepository(JsonUtility.FromJson<GameSnapshot>(lease.Asset.text));
                    units=data.Units.Values.OrderBy(unit=>unit.Cost).ThenBy(unit=>unit.Id).ToArray();
                    HashSet<int> recipeItems=new HashSet<int>(data.ItemRecipes
                        .SelectMany(recipe=>new[]{recipe.FirstItemId,recipe.SecondItemId,recipe.ResultItemId}));
                    items=data.Items.Values.Where(item=>item.DropWeight>0 || recipeItems.Contains(item.Id))
                        .OrderBy(item=>item.Id).ToArray();
                }
                token.ThrowIfCancellationRequested();
                ShowActiveTab();
            }
            catch(OperationCanceledException) { }
            catch(Exception exception)
            {
                if(token.IsCancellationRequested) return;
                status.text="도감 정보를 불러올 수 없습니다.";
                Debug.LogWarning(exception.Message,this);
            }
        }
        private void ShowUnits()=>SwitchTab(false);
        private void ShowItems()=>SwitchTab(true);

        private void SwitchTab(bool itemsTab)
        {
            if(!IsOpen || showingItems==itemsTab) return;
            showingItems=itemsTab;
            UpdateTabs();
            if(data!=null) ShowActiveTab();
        }

        private void UpdateTabs()
        {
            Color selected=new Color(.55f,.85f,.81f);
            unitTabButton.targetGraphic.color=showingItems?Color.white:selected;
            itemTabButton.targetGraphic.color=showingItems?selected:Color.white;
        }

        private void ShowActiveTab()
        {
            ClearSelection();
            if(showingItems)
            {
                if(itemEntries.Count==0)
                    foreach(ItemDefinition item in items)
                    {
                        UnitCodexEntry row=Instantiate(rowPrefab,roster.content);
                        row.Bind(item.Name,null,()=>SelectItem(item));
                        itemEntries.Add(item.Id,row);
                    }
                if(!itemIconsRequested && items.Length>0)
                {
                    itemIconsRequested=true;
                    LoadItemIconsAsync(assets,openLifetime.Token).Forget();
                }
            }
            else if(entries.Count==0)
                foreach(UnitDefinition unit in units)
                {
                    UnitCodexEntry row=Instantiate(rowPrefab,roster.content);
                    row.Bind($"{unit.Name}  <color=#FFD159>{unit.Cost}G</color>",catalog.Icon(unit.IconId),()=>Select(unit));
                    entries.Add(unit.Id,row);
                }

            foreach(UnitCodexEntry row in entries.Values) row.gameObject.SetActive(!showingItems);
            foreach(UnitCodexEntry row in itemEntries.Values) row.gameObject.SetActive(showingItems);
            status.text=showingItems && items.Length==0?"등록된 아이템이 없습니다.":string.Empty;
            LayoutRebuilder.ForceRebuildLayoutImmediate(roster.content);
            roster.StopMovement();
            roster.verticalNormalizedPosition=1f;
            if(showingItems) { if(items.Length>0) SelectItem(items[0]); }
            else if(units.Length>0) Select(units[0]);
        }

        private bool SelectEntry(UnitCodexEntry row)
        {
            if(selectedEntry==row) return false;
            ClearSelection();
            selectedEntry=row;
            selectedEntry.SetSelected(true);
            return true;
        }

        private void Select(UnitDefinition unit)
        {
            if(!SelectEntry(entries[unit.Id])) return;
            detail.Show(unit,data,catalog.BattleVisuals.Skill(unit.SkillId)?.icon);
            detail.SetUnlocked(saves!=null && saves.IsUnitUnlocked(unit.Id));
            selectionLifetime=CancellationTokenSource.CreateLinkedTokenSource(openLifetime.Token);
            LoadPortraitAsync(unit,assets,selectionLifetime.Token).Forget();
        }

        private void SelectItem(ItemDefinition item)
        {
            if(!SelectEntry(itemEntries[item.Id])) return;
            selectedItem=item;
            itemName.text=item.Name;
            itemStats.text=ItemText.Details(item);
            SetItemIcon(itemIcons.TryGetValue(item.Id,out AssetService.Lease<Sprite> iconLease)?iconLease.Asset:null);
            itemDetail.gameObject.SetActive(true);
        }

        private void SetItemIcon(Sprite sprite) { itemIcon.sprite=sprite; itemIcon.enabled=sprite!=null; }

        private async UniTask LoadItemIconsAsync(AssetService source,CancellationToken token)
        {
            foreach(ItemDefinition item in items)
            {
                AssetService.Lease<Sprite> lease=null;
                try
                {
                    lease=await source.AcquireAsync<Sprite>(catalog.ItemIconReference(item.IconId),token);
                    token.ThrowIfCancellationRequested();
                    Sprite sprite=lease.Asset;
                    itemIcons.Add(item.Id,lease); lease=null;
                    itemEntries[item.Id].SetIcon(sprite);
                    if(selectedItem==item) SetItemIcon(sprite);
                }
                catch(OperationCanceledException) { return; }
                catch(Exception exception) { if(token.IsCancellationRequested) return; Debug.LogWarning(exception.Message,this); }
                finally {lease?.Dispose();}
            }
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
        private void ClearSelection()
        {
            if(selectedEntry!=null) selectedEntry.SetSelected(false);
            selectedEntry=null; selectedItem=null;
            selectionLifetime?.Cancel(); selectionLifetime?.Dispose(); selectionLifetime=null;
            detail.Clear();
            portrait?.Dispose(); portrait=null;
            itemDetail.gameObject.SetActive(false);
            SetItemIcon(null);
        }
        private void OnDisable()
        {
            openLifetime?.Cancel(); openLifetime?.Dispose(); openLifetime=null;
            ClearSelection();
            foreach(UnitCodexEntry row in itemEntries.Values) { row.SetIcon(null); row.gameObject.SetActive(false); }
            foreach(AssetService.Lease<Sprite> iconLease in itemIcons.Values) iconLease.Dispose();
            itemIcons.Clear();
            itemIconsRequested=false;
            assets?.Dispose(); assets=null;
        }
    }
}
