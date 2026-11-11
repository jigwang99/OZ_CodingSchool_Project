using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AutoBattler.Presentation.Battle
{
    public sealed class ControlHelpView : MonoBehaviour
    {
        [Serializable]
        private sealed class Topic
        {
            public Button button;
            public string title;
            [TextArea(4, 12)] public string instructions;
        }

        [SerializeField] private Topic[] topics;
        [SerializeField] private TextMeshProUGUI topicTitle;
        [SerializeField] private TextMeshProUGUI instructions;
        [SerializeField] private Button backButton, closeButton;
        private UnityAction[] selectActions;

        public Button BackButton => backButton;
        public Button CloseButton => closeButton;

        private void Awake()
        {
            selectActions = new UnityAction[topics.Length];
            for (int i = 0; i < topics.Length; i++)
            {
                int index = i;
                selectActions[i] = () => Select(index);
                topics[i].button.onClick.AddListener(selectActions[i]);
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            Select(0);
        }

        private void Select(int index)
        {
            for (int i = 0; i < topics.Length; i++)
                topics[i].button.interactable = i != index;

            topicTitle.text = topics[index].title;
            instructions.text = topics[index].instructions;
        }

        private void OnDestroy()
        {
            if (selectActions == null) return;
            for (int i = 0; i < selectActions.Length; i++)
                topics[i].button.onClick.RemoveListener(selectActions[i]);
        }
    }
}
