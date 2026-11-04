using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>지역 지도 전환, 경로 위 스테이지 선택과 지도 리소스 수명 관리.</summary>
    public sealed class StageSelectionView : MonoBehaviour
    {
        [SerializeField] private GameNavigationSettings settings;
        [SerializeField] private StageMapSettings mapSettings;
        [SerializeField] private StageSelectionCard[] cards = Array.Empty<StageSelectionCard>();
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button retryMapButton;
        [SerializeField] private Image mapImage;
        [SerializeField] private RectTransform mapContent;
        [SerializeField] private CanvasGroup mapCanvasGroup;
        [SerializeField] private RectTransform travelMarker;
        [SerializeField] private TextMeshProUGUI regionTitle;
        [SerializeField] private TextMeshProUGUI regionCounter;
        [SerializeField] private TextMeshProUGUI mapStatus;
        private AssetService maps;
        private AssetService.Lease<Sprite> mapLease;
        private CancellationTokenSource mapCancellation;
        private int mapVersion;
        private bool interactable = true;
        private StageMapRegion currentRegion;
        private Tween markerTween;
        public int Chapter { get; private set; } = 1;
        public StageDefinition Selection { get; private set; }
        public bool IsLoadingMap { get; private set; }
        public int ActiveMapHandleCount => maps?.ActiveHandleCount ?? 0;
        public Sprite CurrentMapSprite => mapImage.sprite;
        public Button PreviousButton => previousButton;
        public Button NextButton => nextButton;
        public RectTransform TravelMarker => travelMarker;
        public IReadOnlyList<StageSelectionCard> Cards => Array.AsReadOnly(cards);
        public event Action<StageDefinition> Selected;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            maps = new AssetService();
            previousButton.onClick.AddListener(Previous);
            nextButton.onClick.AddListener(Next);
            retryMapButton.onClick.AddListener(Retry);
            foreach (StageSelectionCard card in cards) card.Selected += Select;
        }
        public void Open(int chapter)
        {
            interactable = true;
            ShowChapter(mapSettings.TryGetRegion(chapter, out _) ? chapter : 1);
        }
        private void Previous() { if (previousButton.IsInteractable()) ShowChapter(Chapter - 1); }
        private void Next() { if (nextButton.IsInteractable()) ShowChapter(Chapter + 1); }
        private void Retry() { if (retryMapButton.IsInteractable()) ShowChapter(Chapter); }
        public void ShowChapter(int chapter)
        {
            if (!interactable || GameNavigation.IsTransitioning || !isActiveAndEnabled
                || !mapSettings.TryGetRegion(chapter, out StageMapRegion region)) return;
            int direction = chapter.CompareTo(Chapter);
            CancelMapRequest();
            Chapter = chapter;
            Selection = null;
            currentRegion = null;
            markerTween?.Kill();
            travelMarker.gameObject.SetActive(false);
            foreach (StageSelectionCard card in cards) card.gameObject.SetActive(false);
            StageDefinition first = settings.Stages.First(stage => stage != null && stage.Chapter == chapter);
            regionTitle.text = $"{chapter} · {first.RegionName}";
            regionCounter.text = $"{chapter} / {mapSettings.Regions.Count}";
            mapStatus.text = "지도 불러오는 중…";
            retryMapButton.gameObject.SetActive(false);
            Selected?.Invoke(null);
            IsLoadingMap = true;
            UpdateButtons();
            mapCancellation = new CancellationTokenSource();
            LoadMapAsync(region, direction, mapVersion, mapCancellation.Token).Forget();
        }
        private async UniTask LoadMapAsync(StageMapRegion region, int direction, int version, CancellationToken token)
        {
            AssetService.Lease<Sprite> pendingLease = null;
            try
            {
                pendingLease = await maps.AcquireAsync<Sprite>(region.Background, token);
                token.ThrowIfCancellationRequested();
                if (version != mapVersion) return;
                if (mapImage.sprite != null)
                {
                    _ = mapCanvasGroup.DOFade(0f, .12f).SetUpdate(true);
                    _ = mapContent.DOAnchorPosX(-direction * mapContent.rect.width * .08f, .12f).SetUpdate(true);
                    await UniTask.Delay(TimeSpan.FromSeconds(.12), ignoreTimeScale: true, cancellationToken: token);
                }
                mapImage.sprite = pendingLease.Asset;
                mapLease?.Dispose();
                mapLease = pendingLease;
                pendingLease = null;
                currentRegion = region;
                StageDefinition[] stages = settings.Stages.Where(stage => stage != null && stage.Chapter == Chapter)
                    .OrderBy(stage => stage.Number).ToArray();
                for (int i = 0; i < cards.Length; i++) cards[i].Bind(i < stages.Length ? stages[i] : null);
                PositionNodes();
                mapStatus.text = "";
                mapContent.anchoredPosition = new Vector2(direction * mapContent.rect.width * .10f, 0f);
                mapCanvasGroup.alpha = 0f;
                _ = mapContent.DOAnchorPosX(0f, .28f).SetEase(Ease.OutCubic).SetUpdate(true);
                _ = mapCanvasGroup.DOFade(1f, .28f).SetUpdate(true);
                await UniTask.Delay(TimeSpan.FromSeconds(.28), ignoreTimeScale: true, cancellationToken: token);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (version == mapVersion && !token.IsCancellationRequested)
                {
                    mapImage.sprite = null;
                    mapLease?.Dispose();
                    mapLease = null;
                    mapStatus.text = "지도를 불러오지 못했어요.";
                    retryMapButton.gameObject.SetActive(true);
                    Debug.LogWarning("지역 지도 로드 실패: " + exception.Message, this);
                }
            }
            finally
            {
                pendingLease?.Dispose();
                if (version == mapVersion) { IsLoadingMap = false; UpdateButtons(); }
            }
        }
        private void Select(StageDefinition stage)
        {
            if (!interactable || IsLoadingMap || GameNavigation.IsTransitioning
                || stage == null || stage.Chapter != Chapter || !settings.Contains(stage)) return;
            int previous = Selection == null ? 0 : Selection.Number - 1;
            Selection = stage;
            foreach (StageSelectionCard card in cards) card.SetSelected(card.Stage == stage);
            MoveMarker(previous, stage.Number - 1);
            Selected?.Invoke(stage);
        }
        private Vector2 NodePosition(int index)
        {
            Vector2 normalized = currentRegion.NodePositions[index];
            return new Vector2((normalized.x - .5f) * mapContent.rect.width,
                (normalized.y - .5f) * mapContent.rect.height);
        }
        private void PositionNodes()
        {
            if (currentRegion == null) return;
            for (int i = 0; i < cards.Length && i < currentRegion.NodePositions.Count; i++)
            {
                RectTransform node = cards[i].RectTransform;
                node.anchorMin = node.anchorMax = currentRegion.NodePositions[i];
                node.anchoredPosition = Vector2.zero;
            }
        }
        private void MoveMarker(int from, int to)
        {
            markerTween?.Kill();
            Vector2 offset = Vector2.up * 56f;
            if (!travelMarker.gameObject.activeSelf) travelMarker.anchoredPosition = NodePosition(from) + offset;
            travelMarker.gameObject.SetActive(true);
            Sequence route = DOTween.Sequence().SetUpdate(true);
            int direction = from <= to ? 1 : -1;
            for (int index = from; ; index += direction)
            {
                route.Append(travelMarker.DOAnchorPos(NodePosition(index) + offset, .22f).SetEase(Ease.InOutSine));
                if (index == to) break;
            }
            markerTween = route;
        }
        private void OnRectTransformDimensionsChange()
        {
            if (mapContent == null || currentRegion == null) return;
            PositionNodes();
            if (Selection != null)
            {
                markerTween?.Kill();
                travelMarker.anchoredPosition = NodePosition(Selection.Number - 1) + Vector2.up * 56f;
            }
        }
        public void SetInteractable(bool value) { interactable = value; UpdateButtons(); }
        private void UpdateButtons()
        {
            bool ready = interactable && !IsLoadingMap;
            previousButton.interactable = ready && mapSettings.TryGetRegion(Chapter - 1, out _);
            nextButton.interactable = ready && mapSettings.TryGetRegion(Chapter + 1, out _);
            retryMapButton.interactable = ready;
            foreach (StageSelectionCard card in cards)
                card.Button.interactable = ready && currentRegion != null && card.Stage != null;
        }
        private void CancelMapRequest()
        {
            mapVersion++;
            mapCancellation?.Cancel();
            mapCancellation?.Dispose();
            mapCancellation = null;
            mapContent.DOKill();
            mapCanvasGroup.DOKill();
        }
        private void OnDisable()
        {
            previousButton?.onClick.RemoveListener(Previous);
            nextButton?.onClick.RemoveListener(Next);
            retryMapButton?.onClick.RemoveListener(Retry);
            foreach (StageSelectionCard card in cards) card.Selected -= Select;
            CancelMapRequest();
            markerTween?.Kill();
            travelMarker.gameObject.SetActive(false);
            mapImage.sprite = null;
            mapLease?.Dispose();
            mapLease = null;
            maps?.Dispose();
            maps = null;
            currentRegion = null;
            Selection = null;
            IsLoadingMap = false;
            mapContent.anchoredPosition = Vector2.zero;
            mapCanvasGroup.alpha = 1f;
        }
    }
}
