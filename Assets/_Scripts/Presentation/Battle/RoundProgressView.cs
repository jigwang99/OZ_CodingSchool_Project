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
    /// <summary>한 도전의 전투 순서, 현재 지점과 저장된 승패 이력 표시.</summary>
    public sealed class RoundProgressView : MonoBehaviour
    {
        [SerializeField] private RoundProgressSettings settings;
        [SerializeField] private TextMeshProUGUI stageText;
        [SerializeField] private Image[] markers=Array.Empty<Image>();
        [SerializeField] private RectTransform currentIndicator;
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
            stageText.text=length>0?$"{(state.Round-1)/length+1}-{(state.Round-1)%length+1}":stage;
            int current=-1;
            int first=Mathf.Clamp(Array.IndexOf(roundIds,state.Round)-markers.Length/2,0,Math.Max(0,roundIds.Length-markers.Length));
            for(int i=0;i<markers.Length;i++)
            {
                bool available=first+i<roundIds.Length;
                markers[i].gameObject.SetActive(available);
                if(!available) continue;
                int id=roundIds[first+i];
                RoundProgressSprites sprites=state.Data.Rounds[id].IsBoss?settings.Boss:settings.Combat;
                markers[i].sprite=state.TryGetRoundOutcome(id,out bool won)?won?sprites.Win:sprites.Loss
                    :id==state.Round?sprites.Current:sprites.Pending;
                if(id==state.Round) current=i;
            }
            currentIndicator.gameObject.SetActive(current>=0);
            if(current>=0) currentIndicator.anchoredPosition=new Vector2(markers[current].rectTransform.anchoredPosition.x,-18f);
        }
    }
}
