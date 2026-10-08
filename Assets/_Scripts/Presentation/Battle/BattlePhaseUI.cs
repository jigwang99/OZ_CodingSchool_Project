using AutoBattler.Battle.Flow;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    public sealed class BattlePhaseUI : MonoBehaviour
    {
        [Header("단계 제어")]
        [SerializeField] private BattleFlowController battleFlow;

        [Header("화면 참조")]
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button finishButton;
        [SerializeField] private Button preparationButton;

        private void OnEnable()
        {
            if (battleFlow == null || phaseText == null || startButton == null
                || finishButton == null || preparationButton == null)
            {
                Debug.LogError("전투 단계 UI의 컨트롤러, 상태 텍스트, 버튼 연결 필요.", this);
                return;
            }
            battleFlow.PhaseChanged += Refresh;
            startButton.onClick.AddListener(StartCombat);
            finishButton.onClick.AddListener(FinishCombat);
            preparationButton.onClick.AddListener(BeginPreparation);
            Refresh(battleFlow.CurrentPhase);
        }

        private void OnDisable()
        {
            if (phaseText != null) phaseText.DOKill();
            if (battleFlow != null) battleFlow.PhaseChanged -= Refresh;
            if (startButton != null) startButton.onClick.RemoveListener(StartCombat);
            if (finishButton != null) finishButton.onClick.RemoveListener(FinishCombat);
            if (preparationButton != null) preparationButton.onClick.RemoveListener(BeginPreparation);
        }

        private void StartCombat() => battleFlow.TryStartCombat();

        // 자동 전투 구현 전 단계 전환 확인용. 이후 전투 판정에서 호출.
        private void FinishCombat() => battleFlow.TryFinishCombat();
        private void BeginPreparation() => battleFlow.TryBeginPreparation();

        private void Refresh(BattlePhase phase)
        {
            phaseText.DOKill();
            phaseText.text = phase.ToString();
            Color color = phaseText.color;
            color.a = 0f;
            phaseText.color = color;
            phaseText.DOFade(1f, 0.15f).SetUpdate(true);
            startButton.gameObject.SetActive(phase == BattlePhase.Preparation);
            finishButton.gameObject.SetActive(phase == BattlePhase.Combat);
            preparationButton.gameObject.SetActive(phase == BattlePhase.Result);
        }
    }
}
