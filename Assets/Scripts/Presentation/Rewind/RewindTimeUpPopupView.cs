using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Witch.Presentation.Rewind
{
    public sealed class RewindTimeUpPopupView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text messageText;
        [SerializeField] private Button retryButton;
        [SerializeField, TextArea(2, 5)]
        private string defaultMessage =
            "Time's up!\nThe memory slipped away. Try again.";

        private bool bound;

        public bool IsAuthored => root != null && messageText != null && retryButton != null;
        public bool IsVisible => root != null && root.activeSelf;

        public void Bind(UnityAction onRetry)
        {
            if (bound || retryButton == null) return;
            retryButton.onClick.AddListener(onRetry);
            bound = true;
        }

        public void Show()
        {
            if (!IsAuthored) return;
            messageText.text = defaultMessage;
            retryButton.interactable = true;
            root.SetActive(true);
            retryButton.Select();
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}