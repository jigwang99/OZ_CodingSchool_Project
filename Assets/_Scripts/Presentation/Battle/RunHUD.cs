using System;
using AutoBattler.Run;
using AutoBattler.Battle.Flow;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>현재 구현 범위의 상점, 경제, 전투와 결과 조작 UI.</summary>
    public sealed class RunHUD : MonoBehaviour
    {
        [SerializeField] private RunController run;
        private TextMeshProUGUI status,message,selection;
        private Button start,refresh,xp,sell,next,retry;
        private readonly Button[] shop=new Button[5];
        private readonly TextMeshProUGUI[] shopLabels=new TextMeshProUGUI[5];
        private CanvasGroup panelGroup;
        private BattlePhase displayedPhase;
        public void ConfigureScene(RunController controller)=>run=controller;
        private void Start()
        {
            Build(); run.DisplayChanged+=Refresh; Refresh();
        }
        private void Build()
        {
            var canvasObj=new GameObject("RunCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasObj.transform.SetParent(transform,false);
            canvasObj.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasObj.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
            RectTransform root=canvasObj.GetComponent<RectTransform>();
            RectTransform top=Panel(root,"Header",new Vector2(.5f,1f),new Vector2(0,-53),new Vector2(1920,106));
            status=Label(top,"Status",new Vector2(-440,23),new Vector2(970,42),28);
            message=Label(top,"Message",new Vector2(-440,-24),new Vector2(970,38),21);
            start=Button(top,"Start battle",new Vector2(365,0),new Vector2(180,55),()=>run.StartBattle());
            next=Button(top,"Next round",new Vector2(565,0),new Vector2(180,55),()=>run.Continue());
            retry=Button(top,"Retry load",new Vector2(765,0),new Vector2(180,55),()=>run.RetryLoad());
            RectTransform bottom=Panel(root,"Preparation",new Vector2(.5f,0),new Vector2(0,75),new Vector2(1920,150));
            panelGroup=bottom.gameObject.AddComponent<CanvasGroup>();
            for(int i=0;i<5;i++)
            {
                int slot=i;
                shop[i]=Button(bottom,"Shop",new Vector2(-625+i*244,0),new Vector2(230,112),()=>run.Buy(slot));
                shopLabels[i]=shop[i].GetComponentInChildren<TextMeshProUGUI>(); shopLabels[i].fontSize=21;
            }
            refresh=Button(bottom,"Refresh (2G)",new Vector2(665,36),new Vector2(180,52),()=>run.RefreshShop());
            xp=Button(bottom,"XP (4G)",new Vector2(855,36),new Vector2(170,52),()=>run.BuyXp());
            sell=Button(bottom,"Sell selected",new Vector2(665,-28),new Vector2(180,52),()=>run.SellSelected());
            selection=Label(bottom,"Selection",new Vector2(855,-28),new Vector2(170,62),18);
        }
        private static RectTransform Panel(RectTransform parent,string name,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(Image)); obj.transform.SetParent(parent,false);
            var rect=obj.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=anchor; rect.anchoredPosition=position; rect.sizeDelta=size;
            obj.GetComponent<Image>().color=new Color(.075f,.11f,.17f,.95f); return rect;
        }
        private static TextMeshProUGUI Label(RectTransform parent,string name,Vector2 position,Vector2 size,int fontSize)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); obj.transform.SetParent(parent,false);
            var label=obj.GetComponent<TextMeshProUGUI>(); label.fontSize=fontSize; label.alignment=TextAlignmentOptions.Center;
            label.color=Color.white; label.raycastTarget=false; label.rectTransform.anchoredPosition=position; label.rectTransform.sizeDelta=size;
            return label;
        }
        private static Button Button(RectTransform parent,string text,Vector2 position,Vector2 size,Action action)
        {
            RectTransform rect=Panel(parent,text,new Vector2(.5f,.5f),position,size);
            rect.GetComponent<Image>().color=new Color(.17f,.28f,.4f);
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=rect.GetComponent<Image>();
            button.onClick.AddListener(()=>action());
            var label=Label(rect,"Label",Vector2.zero,size-new Vector2(12,6),23); label.text=text;
            return button;
        }
        private void Update()
        {
            if(status==null) return;
            if(run.IsReady && run.Flow.CurrentPhase==BattlePhase.Combat)
                message.text=$"Automatic battle   {Mathf.CeilToInt(run.RemainingSeconds)}s remaining";
            var selected=run.Drag.SelectedUnit;
            selection.text=selected!=null && selected.State!=null?selected.GetComponent<AutoBattler.Battle.Units.Unit>()?.Definition?.Name:"Select a unit";
            sell.interactable=CanPrepare() && selected!=null && selected.State!=null;
        }
        private bool CanPrepare()=>run.IsReady && run.State.CanPrepare && !run.State.IsOver && run.Flow.CurrentPhase==BattlePhase.Preparation;
        private void Refresh()
        {
            if(status==null) return;
            message.text=run.Message;
            status.text=run.IsReady ? $"Round {(run.State.Round-1)/4+1}-{(run.State.Round-1)%4+1}   Lives {run.State.Lives}   Gold {run.State.Gold}   Level {run.State.Level}   XP {run.State.Xp}/{(run.State.Level==10?0:run.State.Data.RequiredXp(run.State.Level))}   Deployed {run.State.Board.DeployedCount}/{run.State.Level}" : "PixelTactics   Loading";
            bool prepare=CanPrepare();
            for(int i=0;i<5;i++)
            {
                int id=run.IsReady?run.State.Shop[i]:0;
                shopLabels[i].text=id==0?"Empty":$"{run.State.Data.Units[id].Name}\n{run.State.Data.Units[id].Cost} gold\nPool {run.State.Stock(id)}";
                shop[i].interactable=prepare && id!=0;
            }
            start.interactable=prepare && run.State.Board.DeployedCount>0;
            refresh.interactable=prepare; xp.interactable=prepare && run.State.Level<10;
            next.interactable=run.IsReady && run.Flow.CurrentPhase==BattlePhase.Result && !run.State.IsOver;
            retry.gameObject.SetActive(!run.IsReady && !run.IsLoading);
            panelGroup.interactable=prepare;
            if(run.IsReady && run.State.IsOver) message.text=run.State.WonRun?"Challenge complete!":"Challenge ended. Restart Play to try again.";
            if(displayedPhase!=run.Flow.CurrentPhase)
            {
                displayedPhase=run.Flow.CurrentPhase; panelGroup.DOKill();
                panelGroup.DOFade(prepare?1f:.45f,.2f).SetUpdate(true);
            }
        }
        private void OnDestroy()
        {
            if(run!=null) run.DisplayChanged-=Refresh;
            if(panelGroup!=null) panelGroup.DOKill();
        }
    }
}
