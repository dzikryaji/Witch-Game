using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Witch.Presentation.Rewind
{
    public sealed class RewindTimeUpPopupView : MonoBehaviour
    {
        [SerializeField] private GameObject overlay;
        [SerializeField] private GameObject root;
        [SerializeField] private Text messageText;
        [FormerlySerializedAs("retryButton")]
        [SerializeField] private Button menuButton;
        [SerializeField, TextArea(2, 5)]
        private string defaultMessage =
    "The Witch Hunter caught you and burned you at the village market.";

        private bool bound;

        public bool IsAuthored => root != null && messageText != null && menuButton != null;
        public bool IsVisible => root != null && root.activeSelf;

        public void Bind(UnityAction onMenu)
        {
            if (bound || menuButton == null) return;
            menuButton.onClick.AddListener(onMenu);
            bound = true;
        }

        public void Show(string extraMessage = null)
        {
            if (!IsAuthored) return;

            messageText.text = string.IsNullOrEmpty(extraMessage)
                ? defaultMessage
                : defaultMessage + "\n\n" + extraMessage;

            menuButton.interactable = true;

            if (overlay != null)
                overlay.SetActive(true);

            root.SetActive(true);
            menuButton.Select();
        }

        public void Hide()
        {
            if (overlay != null)
                overlay.SetActive(false);

            if (root != null)
                root.SetActive(false);
        }

        public void SetInteractable(bool value)
        {
            if (menuButton != null) menuButton.interactable = value;
        }
    }
}