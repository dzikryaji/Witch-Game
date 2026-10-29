using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Witch.Presentation.Shop
{
    public sealed class MinigameIntroPopupView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text narrativeText;
        [SerializeField] private Button startButton;
        [SerializeField, TextArea(3, 8)]
        private string defaultNarrative =
            "KNOCK. KNOCK.\nThe Hunters suspect poison. Cast the memory spell before they come in.";

        private bool bound;

        public bool IsAuthored => root != null && narrativeText != null && startButton != null;
        public bool IsVisible => root != null && root.activeSelf;

        public void Bind(UnityAction onStart)
        {
            if (bound || startButton == null) return;
            startButton.onClick.AddListener(onStart);
            bound = true;
        }

        public void Show(string errorMessage = null)
        {
            narrativeText.text = string.IsNullOrEmpty(errorMessage)
                ? defaultNarrative
                : defaultNarrative + "\n\n" + errorMessage;
            startButton.interactable = true;
            root.SetActive(true);
            startButton.Select();
        }

        public void Hide() { if (root != null) root.SetActive(false); }

        public void SetInteractable(bool value) { if (startButton != null) startButton.interactable = value; }
    }
}