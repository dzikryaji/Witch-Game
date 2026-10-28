using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace Witch.Presentation.Rewind
{
    public sealed class RewindCardView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private GameObject hiddenFace;
        [SerializeField] private GameObject revealedFace;
        [SerializeField] private GameObject matchedFace;
        [SerializeField] private Text revealedLabel;
        [SerializeField] private Text matchedLabel;
        private UnityAction boundAction;

        public Button Button => button;
        public bool IsAuthored => button != null
            && hiddenFace != null
            && revealedFace != null
            && matchedFace != null
            && revealedLabel != null
            && matchedLabel != null;

        public void Render(string symbol, bool visible, bool matched, bool interactable)
        {
            revealedLabel.text = symbol;
            matchedLabel.text = symbol;
            hiddenFace.SetActive(!visible && !matched);
            revealedFace.SetActive(visible && !matched);
            matchedFace.SetActive(matched);
            button.interactable = interactable;
        }

        public void Bind(UnityAction action)
        {
            Unbind();
            boundAction = action;
            button.onClick.AddListener(boundAction);
        }

        public void Unbind()
        {
            if (button != null && boundAction != null)
            {
                button.onClick.RemoveListener(boundAction);
            }
            boundAction = null;
        }

        private void OnDestroy() => Unbind();
    }
}
