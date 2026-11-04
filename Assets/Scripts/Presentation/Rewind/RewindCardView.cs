using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Witch.Presentation.Rewind
{
    public sealed class RewindCardView : MonoBehaviour
    {
        private enum FaceState { Unset, Hidden, Revealed, Matched }

        private static readonly HashSet<string> warnedKeys = new HashSet<string>();

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
        [Header("Flip Animation")]
        [SerializeField, Min(0.05f)] private float flipDuration = 0.25f;
        [SerializeField] private bool reducedMotion;

        private UnityAction boundAction;
        private FaceState current = FaceState.Unset;
        private Coroutine flipRoutine;

        public Button Button => button;
        public float FlipDuration => reducedMotion ? 0f : flipDuration;
        public bool IsAuthored => button != null
            && hiddenFace != null
            && revealedFace != null
            && matchedFace != null
            && revealedLabel != null
            && matchedLabel != null;

        public void SetReducedMotion(bool value) => reducedMotion = value;

        public void Render(string symbol, bool visible, bool matched, bool interactable)
        {
            Sprite sprite = symbolLibrary != null ? symbolLibrary.Find(symbol) : null;
            if (sprite == null && (visible || matched) && warnedKeys.Add(symbol ?? string.Empty))
            {
                Debug.LogWarning($"No herb image for symbol key '{symbol}'. Add it to the Rewind Symbol Library.", this);
            }

            revealedLabel.text = symbol;
            matchedLabel.text = symbol;
            revealedLabel.gameObject.SetActive(sprite == null);
            matchedLabel.gameObject.SetActive(sprite == null);
            ApplySprite(revealedImage, sprite);
            ApplySprite(matchedImage, sprite);
            button.interactable = interactable;

            FaceState target = matched ? FaceState.Matched : visible ? FaceState.Revealed : FaceState.Hidden;
            if (target == current) return;   // repeated Render calls with the same state do nothing

            FaceState previous = current;
            if (flipRoutine != null)
            {
                StopCoroutine(flipRoutine);   // a new state arrived mid-flip: finish the old one instantly
                flipRoutine = null;
                ShowFace(previous);
                transform.localScale = Vector3.one;
            }
            current = target;

            bool isFlip = (previous == FaceState.Hidden && target == FaceState.Revealed)
                || (previous == FaceState.Revealed && target == FaceState.Hidden);
            if (isFlip && !reducedMotion && isActiveAndEnabled)
            {
                flipRoutine = StartCoroutine(FlipRoutine(target));
            }
            else
            {
                ShowFace(target);
            }
        }

        private IEnumerator FlipRoutine(FaceState target)
        {
            yield return ScaleX(1f, 0f, flipDuration * 0.5f);
            ShowFace(target);                                   // swap the face while the card is edge-on
            yield return ScaleX(0f, 1f, flipDuration * 0.5f);
            flipRoutine = null;
        }

        private IEnumerator ScaleX(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                transform.localScale = new Vector3(Mathf.Lerp(from, to, eased), 1f, 1f);
                yield return null;
            }
            transform.localScale = new Vector3(to, 1f, 1f);
        }

        private void ShowFace(FaceState state)
        {
            hiddenFace.SetActive(state == FaceState.Hidden || state == FaceState.Unset);
            revealedFace.SetActive(state == FaceState.Revealed);
            matchedFace.SetActive(state == FaceState.Matched);
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

        private void OnDisable()
        {
            // Coroutines stop when the object is disabled, so settle the card at its final state.
            flipRoutine = null;
            transform.localScale = Vector3.one;
            if (current != FaceState.Unset && IsAuthored) ShowFace(current);
        }

        private void OnDestroy() => Unbind();
    }
}