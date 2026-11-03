using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>지역별 카드 5개를 재사용하고 현재 지역의 배경 미리보기만 로드.</summary>
    public sealed class StageSelectionView : MonoBehaviour
    {
        [SerializeField] private GameNavigationSettings settings;
        [SerializeField] private Button[] chapterButtons=Array.Empty<Button>();
        [SerializeField] private TextMeshProUGUI[] chapterLabels=Array.Empty<TextMeshProUGUI>();
        [SerializeField] private StageSelectionCard[] cards=Array.Empty<StageSelectionCard>();
        private UnityAction[] chapterActions;
        private AssetService previews;
        private CancellationTokenSource previewCancellation;
        private readonly List<AssetService.Lease<Sprite>> leases=new List<AssetService.Lease<Sprite>>();
        private int previewVersion;
        private bool interactable=true;
        public int Chapter { get; private set; }=1;
        public StageDefinition Selection { get; private set; }
        public bool IsLoadingPreviews { get; private set; }
        public int ActivePreviewHandleCount=>previews?.ActiveHandleCount ?? 0;
        public IReadOnlyList<Button> ChapterButtons=>Array.AsReadOnly(chapterButtons);
        public IReadOnlyList<StageSelectionCard> Cards=>Array.AsReadOnly(cards);
        public event Action<StageDefinition> Selected;
        private void OnEnable()
        {
            if(!Application.isPlaying) return;
            chapterActions=new UnityAction[chapterButtons.Length];
            for(int i=0;i<chapterButtons.Length;i++)
            {
                int chapter=i+1;
                Button chapterButton=chapterButtons[i];
                chapterActions[i]=()=> { if(chapterButton.IsInteractable()) ShowChapter(chapter); };
                chapterButtons[i].onClick.AddListener(chapterActions[i]);
                StageDefinition first=settings.Stages.FirstOrDefault(stage=>stage!=null && stage.Chapter==chapter);
                chapterLabels[i].text=first?.RegionName ?? chapter.ToString();
            }
            foreach(StageSelectionCard card in cards) card.Selected+=Select;
        }
        public void Open(int chapter)
        {
            Selection=null; interactable=true; ShowChapter(chapter);
        }
        public void ShowChapter(int chapter)
        {
            if(!interactable || GameNavigation.IsTransitioning || !isActiveAndEnabled) return;
            CancelPreviews(); Chapter=chapter; Selection=null;
            StageDefinition[] stages=settings.Stages.Where(stage=>stage!=null && stage.Chapter==chapter).OrderBy(stage=>stage.Number).ToArray();
            for(int i=0;i<cards.Length;i++) cards[i].Bind(i<stages.Length?stages[i]:null);
            for(int i=0;i<chapterButtons.Length;i++)
                chapterButtons[i].targetGraphic.color=i+1==chapter?new Color(.14f,.42f,.43f):new Color(.12f,.22f,.3f);
            Selected?.Invoke(null);
            previews=new AssetService(); previewCancellation=new CancellationTokenSource();
            IsLoadingPreviews=true;
            LoadPreviewsAsync(previews,stages,previewVersion,previewCancellation.Token).Forget();
        }
        private async UniTask LoadPreviewsAsync(AssetService service,StageDefinition[] stages,int version,CancellationToken token)
        {
            try
            {
                for(int i=0;i<stages.Length && i<cards.Length;i++)
                {
                    AssetService.Lease<Sprite> lease=await service.AcquireAsync<Sprite>(stages[i].BattleBackground,token);
                    if(token.IsCancellationRequested || version!=previewVersion || !isActiveAndEnabled) { lease.Dispose(); return; }
                    leases.Add(lease); cards[i].SetPreview(lease.Asset);
                }
            }
            catch(OperationCanceledException) { }
            catch(Exception exception) { if(!token.IsCancellationRequested) Debug.LogWarning("배경 미리보기를 불러오지 못했습니다. "+exception.Message,this); }
            finally { if(version==previewVersion) IsLoadingPreviews=false; }
        }
        private void Select(StageDefinition stage)
        {
            if(!interactable || GameNavigation.IsTransitioning || !settings.Contains(stage)) return;
            Selection=stage;
            foreach(StageSelectionCard card in cards) card.SetSelected(card.Stage==stage);
            Selected?.Invoke(stage);
        }
        public void SetInteractable(bool value)
        {
            interactable=value;
            foreach(Button button in chapterButtons) button.interactable=value;
            foreach(StageSelectionCard card in cards) card.Button.interactable=value && card.Stage!=null;
        }
        private void CancelPreviews()
        {
            previewVersion++; previewCancellation?.Cancel(); previewCancellation?.Dispose(); previewCancellation=null;
            foreach(StageSelectionCard card in cards) card.SetPreview(null);
            foreach(AssetService.Lease<Sprite> lease in leases) lease.Dispose(); leases.Clear();
            previews?.Dispose(); previews=null; IsLoadingPreviews=false;
        }
        private void OnDisable()
        {
            if(chapterActions!=null) for(int i=0;i<chapterActions.Length;i++) chapterButtons[i].onClick.RemoveListener(chapterActions[i]);
            foreach(StageSelectionCard card in cards) card.Selected-=Select;
            CancelPreviews();
        }
    }
}
