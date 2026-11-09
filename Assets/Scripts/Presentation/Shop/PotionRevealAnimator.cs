using System;
using System.Collections;
using UnityEngine;

namespace Witch.Presentation.Shop
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PotionRevealAnimator : MonoBehaviour
    {
        [SerializeField] private PreparedPotionDragItem dragItem;
        [SerializeField] private PreparedPotionTapToServe tapToServe;
        [SerializeField] private GameObject dimOverlay;          // boleh kosong

        [Header("Durasi (detik)")]
        [SerializeField] private float popDuration = 0.35f;
        [SerializeField] private float holdDuration = 0.7f;
        [SerializeField] private float moveDuration = 0.6f;

        [Header("Ukuran saat besar")]
        [Tooltip("Tinggi potion saat besar, sebagai bagian dari tinggi layar (0.7 = 70%).")]
        [SerializeField, Range(0.2f, 1f)] private float screenHeightFraction = 0.7f;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Coroutine routine;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            canvasGroup = GetComponent<CanvasGroup>();
        }

        // Dipanggil presenter setelah brew berhasil.
        public void Play()
        {
            if (!isActiveAndEnabled) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Reveal());
        }

        private IEnumerator Reveal()
        {
            RectTransform rootRect = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
            SetLocked(true);

            // 1. Naik ke lapisan paling atas (posisi di layar tetap sama).
            transform.SetParent(rootRect, true);
            if (dimOverlay != null)
            {
                dimOverlay.SetActive(true);
                dimOverlay.transform.SetAsLastSibling();
            }
            transform.SetAsLastSibling();

            // 2. Hitung titik awal & akhir.
            Vector2 restPosition = rectTransform.anchoredPosition;   // meja kasir
            Vector2 center = Vector2.zero;                           // tengah layar (anchor 0.5)
            float bigScale = rootRect.rect.height * screenHeightFraction / rectTransform.rect.height;

            // 3. Pop: muncul di tengah, membesar dari 0.
            rectTransform.anchoredPosition = center;
            yield return Animate(popDuration, t =>
            {
                float e = 1f - Mathf.Pow(1f - t, 3f);                // ease-out: cepat lalu melambat
                transform.localScale = Vector3.one * Mathf.Lerp(0f, bigScale, e);
            });

            // 4. Tahan sebentar 
            yield return new WaitForSecondsRealtime(holdDuration);

            // 5. Terbang ke meja kasir sambil mengecil.
            yield return Animate(moveDuration, t =>
            {
                float e = t * t * (3f - 2f * t);                     // smoothstep: halus di awal & akhir
                rectTransform.anchoredPosition = Vector2.Lerp(center, restPosition, e);
                transform.localScale = Vector3.one * Mathf.Lerp(bigScale, 1f, e);
            });

            // 6. Selesai: kembali ke parent asli.
            Finish();
        }

        private void Finish()
        {
            transform.localScale = Vector3.one;
            if (dragItem != null) dragItem.RestoreHome();            // parent, urutan, posisi asli
            if (dimOverlay != null) dimOverlay.SetActive(false);
            SetLocked(false);
            routine = null;
        }

        private void OnDisable()
        {
            // Kalau potion hilang di tengah animasi (mis. dibuang), rapikan keadaannya.
            if (routine == null) return;
            routine = null;
            transform.localScale = Vector3.one;
            if (dimOverlay != null) dimOverlay.SetActive(false);
            SetLocked(false);
        }

        private void SetLocked(bool locked)
        {
            if (tapToServe != null) tapToServe.IsLocked = locked;
            if (canvasGroup != null) canvasGroup.blocksRaycasts = !locked;
        }

        // Menjalankan step(t) tiap frame, t naik dari 0 ke 1 selama `duration` detik.
        private static IEnumerator Animate(float duration, Action<float> step)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
            {
                step(t);
                yield return null;
            }
            step(1f);
        }
    }
}