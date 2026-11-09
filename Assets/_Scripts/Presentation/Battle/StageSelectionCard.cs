using System;
using AutoBattler.Run;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>길 위 스테이지 지점의 번호·장소와 선택 상태 표시.</summary>
    public sealed class StageSelectionCard : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Graphic frame;
        [SerializeField] private TextMeshProUGUI numberText;
        [SerializeField] private TextMeshProUGUI locationText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private GameObject progressRoot;
        public Button Button=>button;
        public StageDefinition Stage { get; private set; }
        public RectTransform RectTransform=>(RectTransform)transform;
        public bool IsSelected { get; private set; }
        public bool IsUnlocked { get; private set; }=true;
        public bool IsCleared { get; private set; }
        public TextMeshProUGUI ProgressText=>progressText;
        public event Action<StageDefinition> Selected;
        private void OnEnable() { if(Application.isPlaying) button.onClick.AddListener(Choose); }
        private void OnDisable() { if(button!=null) button.onClick.RemoveListener(Choose); transform.DOKill(); }
        private void Choose() { if(Stage!=null && button.IsInteractable()) Selected?.Invoke(Stage); }
        public void Bind(StageDefinition stage,bool unlocked=true,bool cleared=false)
        {
            Stage=stage; numberText.text=stage!=null?stage.DisplayName:"";
            locationText.text=stage!=null?stage.LocationName:"";
            IsSelected=false; SetProgress(unlocked,cleared); SetSelected(false,false); button.interactable=stage!=null;
            gameObject.SetActive(stage!=null);
        }
        public void SetProgress(bool unlocked,bool cleared)
        {
            IsUnlocked=unlocked; IsCleared=cleared;
            if(progressText!=null)
            {
                progressText.text=cleared?"완료":unlocked?string.Empty:"잠김";
                progressText.color=cleared?new Color(.45f,.94f,.72f):new Color(.7f,.7f,.7f);
                if(progressRoot!=null) progressRoot.SetActive(cleared || !unlocked);
                else progressText.gameObject.SetActive(cleared || !unlocked);
            }
            locationText.color=unlocked?Color.white:new Color(.6f,.6f,.6f);
            SetSelected(IsSelected,false);
        }
        public void SetSelected(bool selected,bool animate=true)
        {
            IsSelected=selected;
            frame.color=!IsUnlocked?(selected?new Color(.32f,.36f,.36f):new Color(.2f,.23f,.23f)):selected?new Color(.96f,.68f,.18f):IsCleared?new Color(.12f,.5f,.3f):new Color(.08f,.48f,.54f);
            numberText.color=!IsUnlocked?(selected?new Color(.85f,.85f,.85f):new Color(.6f,.6f,.6f)):selected?new Color(.10f,.16f,.19f):Color.white;
            transform.DOKill();
            Vector3 scale=Vector3.one*(selected?1.12f:1f);
            if(animate && Application.isPlaying) transform.DOScale(scale,.14f).SetEase(Ease.OutCubic).SetUpdate(true);
            else transform.localScale=scale;
        }
    }
}
