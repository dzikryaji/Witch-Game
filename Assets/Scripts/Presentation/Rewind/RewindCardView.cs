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
        [Header("Herb Images (optional)")]
        [SerializeField] private Image revealedImage;
        [SerializeField] private Image matchedImage;
        [SerializeField] private RewindSymbolLibrary symbolLibrary;
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
            Sprite sprite = symbolLibrary != null ? symbolLibrary.Find(symbol) : null;
            if (sprite == null && (visible || matched))
            {
                Debug.LogWarning($"No herb image for symbol key '{symbol}'. Add it to the Rewind Symbol Library.", this);
            }

            revealedLabel.text = symbol;
            matchedLabel.text = symbol;
            // Text only appears as a safety net when a picture is missing.
            revealedLabel.gameObject.SetActive(sprite == null);
            matchedLabel.gameObject.SetActive(sprite == null);
            ApplySprite(revealedImage, sprite);
            ApplySprite(matchedImage, sprite);

            hiddenFace.SetActive(!visible && !matched);
            revealedFace.SetActive(visible && !matched);
            matchedFace.SetActive(matched);
            button.interactable = interactable;
        }

        private static void ApplySprite(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.preserveAspect = true;
            image.enabled = sprite != null;
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