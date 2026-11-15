using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Witch.Presentation.Shop
{
    public static class HerbUnlockFlags
    {
        public const string Prefix = "shop.unlock.announced.";

        public static string For(string categoryId) => Prefix + categoryId;
    }

    [DisallowMultipleComponent]
    public sealed class HerbUnlockAnnouncementView : MonoBehaviour
    {
        [Serializable]
        private sealed class CategoryCard
        {
            public string categoryId;
            public Sprite card;
        }

        [SerializeField] private RectTransform cardTransform;
        [SerializeField] private Image cardImage;
        [SerializeField] private Image dimBackdrop;
        [SerializeField] private Button dismissButton;
        [SerializeField] private CategoryCard[] cards = Array.Empty<CategoryCard>();
        [SerializeField, Range(0f, 1f)] private float backdropAlpha = 0.55f;
        [SerializeField, Min(0f)] private float slideInDuration = 0.6f;
        [SerializeField, Min(0f)] private float slideOutDuration = 0.4f;
        [SerializeField, Min(0f)] private float offscreenOffset = 1200f;

        private Vector2 restPosition;
        private bool restCaptured;
        private bool listenerBound;
        private bool canDismiss;
        private bool dismissRequested;
        private Coroutine routine;
        private TaskCompletionSource<bool> finished;

        public bool IsShowing => finished != null;

        public bool HasCard(string categoryId) => FindCard(categoryId) != null;

        public Task<bool> ShowAsync(string categoryId)
        {
            Sprite sprite = FindCard(categoryId);
            if (sprite == null || cardTransform == null || cardImage == null)
            {
                return Task.FromResult(false);
            }

            if (finished != null) return finished.Task;

            CaptureRest();
            BindListener();
            finished = new TaskCompletionSource<bool>();
            cardImage.sprite = sprite;
            canDismiss = false;
            dismissRequested = false;
            gameObject.SetActive(true);
            routine = StartCoroutine(Run());
            return finished.Task;
        }

        public void Hide()
        {
            CaptureRest();
            if (routine != null) StopCoroutine(routine);
            routine = null;
            ResetVisuals();
            Complete(false);
        }

        private void OnDisable()
        {
            routine = null;
            if (finished != null)
            {
                TaskCompletionSource<bool> signal = finished;
                finished = null;
                signal.TrySetResult(false);
            }
        }

        private void BindListener()
        {
            if (listenerBound || dismissButton == null) return;
            dismissButton.onClick.AddListener(OnDismissClicked);
            listenerBound = true;
        }

        private void OnDismissClicked()
        {
            if (canDismiss) dismissRequested = true;
        }

        private IEnumerator Run()
        {
            Vector2 above = restPosition + Vector2.up * offscreenOffset;
            cardTransform.anchoredPosition = above;
            SetBackdrop(0f);

            yield return Animate(slideInDuration, t =>
            {
                cardTransform.anchoredPosition = Vector2.LerpUnclamped(above, restPosition, EaseOutBack(t));
                SetBackdrop(t);
            });

            canDismiss = true;
            while (!dismissRequested) yield return null;
            canDismiss = false;

            yield return Animate(slideOutDuration, t =>
            {
                cardTransform.anchoredPosition = Vector2.LerpUnclamped(restPosition, above, t * t);
                SetBackdrop(1f - t);
            });

            routine = null;
            ResetVisuals();
            Complete(true);
        }

        private void Complete(bool shown)
        {
            TaskCompletionSource<bool> signal = finished;
            finished = null;
            gameObject.SetActive(false);
            signal?.TrySetResult(shown);
        }

        private static IEnumerator Animate(float duration, Action<float> apply)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                apply(Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            apply(1f);
        }

        private void SetBackdrop(float amount)
        {
            if (dimBackdrop == null) return;
            Color color = dimBackdrop.color;
            color.a = backdropAlpha * amount;
            dimBackdrop.color = color;
        }

        private void ResetVisuals()
        {
            if (cardTransform != null && restCaptured) cardTransform.anchoredPosition = restPosition;
            SetBackdrop(0f);
            canDismiss = false;
            dismissRequested = false;
        }

        private void CaptureRest()
        {
            if (restCaptured || cardTransform == null) return;
            restPosition = cardTransform.anchoredPosition;
            restCaptured = true;
        }

        private Sprite FindCard(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId) || cards == null) return null;
            foreach (CategoryCard entry in cards)
            {
                if (entry != null && entry.card != null
                    && string.Equals(entry.categoryId, categoryId, StringComparison.Ordinal))
                {
                    return entry.card;
                }
            }

            return null;
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
