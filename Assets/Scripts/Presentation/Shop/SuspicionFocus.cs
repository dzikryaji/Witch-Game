using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Witch.Presentation.Shop
{
    [DisallowMultipleComponent]
    public sealed class SuspicionFocusView : MonoBehaviour
    {
        [Tooltip("Bar yang dibesarkan. Kosong = objek ini.")]
        [SerializeField] private RectTransform bar;
        [Tooltip("Canvas di bar, supaya bar tampil di atas backdrop. Kosong = Canvas di objek ini.")]
        [SerializeField] private Canvas barCanvas;
        [Tooltip("Image hitam selayar penuh, urutan paling bawah di Shop Canvas.")]
        [SerializeField] private Image backdrop;

        [SerializeField] private Vector2 focusSize = new Vector2(368f, 30f);
        [SerializeField, Range(0f, 1f)] private float backdropAlpha = 0.6f;
        [SerializeField, Min(0f)] private float growDuration = 0.25f;
        [SerializeField, Min(0f)] private float holdDuration = 1.2f;
        [SerializeField, Min(0f)] private float shrinkDuration = 0.3f;
        [SerializeField] private int focusSortingOrder = 50;

        private Vector2 baseSizeDelta;
        private bool captured;
        private Coroutine routine;

        public void Play()
        {
            Capture();
            if (!isActiveAndEnabled) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run());
        }

        public void Hide()
        {
            Capture();
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            ResetVisuals();
        }

        private void OnDisable()
        {
            routine = null;
            if (captured) ResetVisuals();
        }

        private IEnumerator Run()
        {
            // Hitung dari ukuran normal, supaya hasilnya tepat focusSize.
            bar.sizeDelta = baseSizeDelta;
            Vector2 bigDelta = baseSizeDelta + (focusSize - bar.rect.size);

            if (barCanvas != null)
            {
                barCanvas.overrideSorting = true;
                barCanvas.sortingOrder = focusSortingOrder;
            }
            if (backdrop != null)
            {
                backdrop.gameObject.SetActive(true);
                backdrop.transform.SetAsLastSibling();
            }

            yield return Animate(0f, 1f, growDuration, bigDelta);

            float waited = 0f;
            while (waited < holdDuration)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return Animate(1f, 0f, shrinkDuration, bigDelta);

            routine = null;
            ResetVisuals();
        }

        private IEnumerator Animate(float from, float to, float duration, Vector2 bigDelta)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                Apply(Mathf.SmoothStep(from, to, Mathf.Clamp01(elapsed / duration)), bigDelta);
                yield return null;
            }
            Apply(to, bigDelta);
        }

        // amount 0 = normal, 1 = fokus penuh.
        private void Apply(float amount, Vector2 bigDelta)
        {
            bar.sizeDelta = Vector2.LerpUnclamped(baseSizeDelta, bigDelta, amount);
            if (backdrop != null)
            {
                Color c = backdrop.color;
                c.a = backdropAlpha * amount;
                backdrop.color = c;
            }
        }

        private void ResetVisuals()
        {
            bar.sizeDelta = baseSizeDelta;
            if (barCanvas != null) barCanvas.overrideSorting = false;
            if (backdrop != null)
            {
                Color c = backdrop.color;
                c.a = 0f;
                backdrop.color = c;
                backdrop.gameObject.SetActive(false);
            }
        }

        private void Capture()
        {
            if (captured) return;
            if (bar == null) bar = (RectTransform)transform;
            if (barCanvas == null) barCanvas = bar.GetComponent<Canvas>();
            baseSizeDelta = bar.sizeDelta;
            captured = true;
        }
    }
}