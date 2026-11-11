using System;
using System.Collections.Generic;
using System.Linq;
using AutoBattler.Data.Snapshot;
using AutoBattler.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>현재 보스 구간의 전투 순서와 저장된 승패 표시.</summary>
    public sealed class RoundProgressView : MonoBehaviour
    {
        [SerializeField] private RoundProgressSettings settings;
        [SerializeField] private TextMeshProUGUI stageText;
        [SerializeField] private Image[] markers=Array.Empty<Image>();
        [SerializeField] private RectTransform currentIndicator;
        [SerializeField] private LayoutElement panelLayout;
        [SerializeField] private RectTransform bossDivider;
        [SerializeField, Min(0f)] private float horizontalPadding=10f;
        [SerializeField, Min(0f)] private float markerSpacing=8f;
        [SerializeField, Min(0f)] private float sectionSpacing=12f;
        [SerializeField, Min(1f)] private float combatMarkerSize=28f;
        [SerializeField, Min(1f)] private float bossMarkerSize=32f;
        private DataRepository displayedData;
        private int[] roundIds=Array.Empty<int>();
        public IReadOnlyList<Image> Markers => Array.AsReadOnly(markers);
        public RectTransform CurrentIndicator => currentIndicator;
        public TextMeshProUGUI StageText => stageText;
        public RoundProgressSettings Settings => settings;

        public void Show(RunState state,string stage)
        {
            gameObject.SetActive(state!=null);
            if(state==null) return;
            if(!ReferenceEquals(displayedData,state.Data))
            {
                displayedData=state.Data;
                roundIds=state.Data.Rounds.Keys.OrderBy(id=>id).ToArray();
            }
            int length=state.Data.RoundsPerSegment;
            int currentIndex=Array.IndexOf(roundIds,state.Round);
            int segmentStart=length>0 && currentIndex>=0 ? currentIndex/length*length : 0;
            int segmentEnd=length>0 ? Math.Min(segmentStart+length,roundIds.Length) : roundIds.Length;
            stageText.text=RoundLabel(state.Data,state.Round,stage);
            int current=-1;
            int first=Mathf.Clamp(currentIndex-markers.Length/2,segmentStart,Math.Max(segmentStart,segmentEnd-markers.Length));
            int count=Math.Min(markers.Length,segmentEnd-first);
            int boss=-1;
            for(int i=0;i<markers.Length;i++)
            {
                bool available=i<count;
                markers[i].gameObject.SetActive(available);
                if(!available) continue;
                int id=roundIds[first+i];
                bool isBoss=state.Data.Rounds[id].IsBoss;
                if(isBoss) boss=i;
                RoundProgressSprites sprites=isBoss?settings.Boss:settings.Combat;
                markers[i].sprite=state.TryGetRoundOutcome(id,out bool won)?won?sprites.Win:sprites.Loss
                    :id==state.Round?sprites.Current:sprites.Pending;
                if(id==state.Round) current=i;
            }
            LayoutMarkers(count,boss);
            currentIndicator.gameObject.SetActive(current>=0);
            if(current>=0)
                currentIndicator.anchoredPosition=new Vector2(markers[current].rectTransform.anchoredPosition.x,currentIndicator.anchoredPosition.y);
        }

        private void LayoutMarkers(int count,int boss)
        {
            RectTransform label=stageText.rectTransform;
            label.anchoredPosition=new Vector2(horizontalPadding,label.anchoredPosition.y);
            float x=horizontalPadding+label.rect.width+sectionSpacing;
            bossDivider.gameObject.SetActive(boss>0);
            for(int i=0;i<count;i++)
            {
                if(i>0) x+=markerSpacing;
                if(i==boss && i>0)
                {
                    bossDivider.anchoredPosition=new Vector2(x+(sectionSpacing-markerSpacing)*.5f,0f);
                    x+=sectionSpacing;
                }
                float size=i==boss?bossMarkerSize:combatMarkerSize;
                RectTransform marker=markers[i].rectTransform;
                marker.sizeDelta=new Vector2(size,size);
                marker.anchoredPosition=new Vector2(x+size*.5f,marker.anchoredPosition.y);
                x+=size;
            }
            panelLayout.preferredWidth=x+horizontalPadding;
        }

        internal static string RoundLabel(DataRepository data,int id,string fallback="--")
        {
            int length=data.RoundsPerSegment;
            if(length<=0 || !data.Rounds.TryGetValue(id,out RoundDefinition round)) return fallback;
            int segment=(id-1)/length+1;
            return round.IsBoss ? $"{segment}보스" : $"{segment}-{(id-1)%length+1}";
        }
    }
}
