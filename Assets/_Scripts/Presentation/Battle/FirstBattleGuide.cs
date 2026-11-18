using System.Linq;
using AutoBattler.Battle.Flow;
using AutoBattler.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    /// <summary>첫 도전 안내와 완료 상태의 PlayerPrefs 저장.</summary>
    [DisallowMultipleComponent]
    public sealed class FirstBattleGuide : MonoBehaviour
    {
        internal const string PreferenceKey = "PixelTactics.FirstBattleGuide";
        private enum Progress { Unseen, Started, Completed }

        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField] private Button skipButton;
        private Progress progress;

        private void Awake()
        {
            progress = (Progress)PlayerPrefs.GetInt(PreferenceKey);
            skipButton.onClick.AddListener(Complete);
        }

        internal void Refresh(int stageId, RunState state, BattlePhase phase)
        {
            if (progress == Progress.Completed || stageId != 1 || state == null || state.IsOver
                || progress == Progress.Unseen && (state.Round != 1 || state.IsSettled))
            {
                Hide();
                return;
            }

            bool fought = state.Round > 1 || state.IsSettled;
            if (fought && state.Items.All.Any(item => item.OwnerUnitId != 0))
            {
                Complete();
                return;
            }
            if (state.Augments.HasPending || phase != BattlePhase.Preparation && phase != BattlePhase.Result)
            {
                Hide();
                return;
            }

            if (progress == Progress.Unseen) Save(Progress.Started);
            if (fought)
            {
                Show(3, state.Items.Stored.Any()
                    ? "왼쪽 아래 보관함을 열고 아이템을 아군에게 드래그하세요."
                    : "전투에서 승리하면 보관함에 아이템이 들어옵니다.");
            }
            else if (state.Board.DeployedCount == 0)
                Show(1, "대기석 기물을 드래그해 아군 전장에 놓으세요.");
            else
                Show(2, "오른쪽 위의 전투 시작 버튼을 누르세요.");
        }

        private void Show(int step, string instruction)
        {
            instructionText.text = $"<b>첫 전투 안내  {step}/3</b>\n{instruction}";
            if (!panel.activeSelf) panel.SetActive(true);
        }

        internal void Hide() => panel.SetActive(false);

        private void Complete()
        {
            Save(Progress.Completed);
            Hide();
        }

        private void Save(Progress value)
        {
            progress = value;
            PlayerPrefs.SetInt(PreferenceKey, (int)value);
            PlayerPrefs.Save();
        }

        private void OnDestroy() => skipButton.onClick.RemoveListener(Complete);
    }
}
