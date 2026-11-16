
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Witch.Presentation
{
    public sealed class ConfirmPopupView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text titleText;
        [SerializeField] private Text messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private Action onConfirm;
        private bool bound;

        public bool IsVisible => root != null && root.activeSelf;

        public void Show(string title, string message, Action confirmed)
        {
            if (root == null || confirmButton == null || cancelButton == null) return;

            EnsureBound();

            onConfirm = confirmed;

            if (titleText != null) titleText.text = title;
            if (messageText != null) messageText.text = message;

            root.SetActive(true);
            cancelButton.Select();
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
            onConfirm = null;
        }

        private void EnsureBound()
        {
            if (bound) return;
            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(Hide);
            bound = true;
        }

        private void Confirm()
        {
            Action action = onConfirm;
            Hide();
            action?.Invoke();
        }
    }
}
