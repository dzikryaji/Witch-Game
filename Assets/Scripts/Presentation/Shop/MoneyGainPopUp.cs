using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Witch.Presentation.Shop
{

    [DisallowMultipleComponent]
    public sealed class MoneyGainPopUp : MonoBehaviour
    {
        [Tooltip("Teks yang menampilkan +N. Kosongkan = pakai teks di objek ini.")]
        [SerializeField] private Graphic label;

        [Tooltip("{0} diganti dengan jumlah koin.")]
        [SerializeField] private string format = "+{0}";

        [Tooltip("Lama tampil (detik).")]
        [SerializeField, Min(0.1f)] private float duration = 1.2f;

        [Tooltip("Seberapa jauh teks naik.")]
        [SerializeField] private float rise = 30f;

        [Tooltip("Bagian durasi saat teks masih terlihat penuh sebelum mulai memudar.")]
        [SerializeField, Range(0f, 1f)] private float holdFraction = 0.5f;

        private RectTransform rectTransform;
        private Vector2 basePosition;
        private Color baseColor = Color.white;
        private bool baseCaptured;
        private Coroutine routine;

        public void Show(int amount)
        {
            if (amount <= 0) return;

            CaptureBase();
            SetText(string.Format(format, amount));
            gameObject.SetActive(true);

            if (routine != null) StopCoroutine(routine);
            if (UnityEngine.Application.isPlaying && isActiveAndEnabled)
            {
                routine = StartCoroutine(Play());
            }
        }

        public void Hide()
        {
            CaptureBase();
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            ResetVisuals();
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            routine = null;
            if (baseCaptured) ResetVisuals();
        }

        private IEnumerator Play()
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);

                float easedRise = 1f - (1f - t) * (1f - t);
                rectTransform.anchoredPosition = basePosition + Vector2.up * (rise * easedRise);

                float fade = t <= holdFraction ? 1f : 1f - Mathf.InverseLerp(holdFraction, 1f, t);
                if (label != null)
                {
                    Color c = baseColor;
                    c.a = baseColor.a * fade;
                    label.color = c;
                }

                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            routine = null;
            Hide();
        }

        private void SetText(string value)
        {
            if (label is Text text) text.text = value;
            else if (label is TMP_Text tmp) tmp.text = value;
        }

        private void ResetVisuals()
        {
            rectTransform.anchoredPosition = basePosition;
            if (label != null) label.color = baseColor;
        }

        private void CaptureBase()
        {
            if (baseCaptured) return;
            rectTransform = (RectTransform)transform;
            if (label == null) label = GetComponent<Graphic>();
            basePosition = rectTransform.anchoredPosition;
            if (label != null) baseColor = label.color;
            baseCaptured = true;
        }
    }
}