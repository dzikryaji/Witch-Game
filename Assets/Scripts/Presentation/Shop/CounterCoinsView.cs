using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Witch.Presentation.Shop
{

    [DisallowMultipleComponent]
    public sealed class CounterCoinsView : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("Optional label showing the amount waiting on the counter, e.g. +10.")]
        [SerializeField] private TMP_Text amountLabel;

        [Tooltip("Length of the small pop when coins appear.")]
        [SerializeField, Min(0f)] private float popDuration = 0.25f;

        [Tooltip("Seconds before the coins collect themselves. 0 = only collected by tapping.")]
        [SerializeField, Min(0f)] private float autoCollectDelay = 1f;

        [Tooltip("How much the coins pulse while the shop waits for them to be collected before closing.")]
        [SerializeField, Range(0f, 0.3f)] private float waitingPulse = 0.08f;

        private Vector3 baseScale = Vector3.one;
        private bool baseScaleCaptured;
        private bool waiting;
        private Coroutine popRoutine;
        private Coroutine autoCollectRoutine;

        public event Action<int> Collected;

        public int Amount { get; private set; }

        public bool HasCoins => Amount > 0;

        public void Add(int amount)
        {
            if (amount <= 0) return;

            CaptureBaseScale();
            Amount += amount;
            if (amountLabel != null) amountLabel.text = "+" + Amount;
            gameObject.SetActive(true);

            bool canAnimate = UnityEngine.Application.isPlaying && isActiveAndEnabled;

            if (popRoutine != null) StopCoroutine(popRoutine);
            popRoutine = canAnimate ? StartCoroutine(Pop()) : null;

            if (autoCollectRoutine != null) StopCoroutine(autoCollectRoutine);
            autoCollectRoutine = canAnimate && autoCollectDelay > 0f ? StartCoroutine(AutoCollect()) : null;
        }

        public void SetWaiting(bool value)
        {
            if (waiting == value) return;
            waiting = value;
            if (!waiting && baseScaleCaptured && popRoutine == null) transform.localScale = baseScale;
        }

        public void Clear()
        {
            CaptureBaseScale();
            Amount = 0;
            waiting = false;
            if (popRoutine != null)
            {
                StopCoroutine(popRoutine);
                popRoutine = null;
            }
            if (autoCollectRoutine != null)
            {
                StopCoroutine(autoCollectRoutine);
                autoCollectRoutine = null;
            }

            transform.localScale = baseScale;
            if (amountLabel != null) amountLabel.text = string.Empty;
            gameObject.SetActive(false);
        }

        public void Collect()
        {
            if (!HasCoins) return;
            int amount = Amount;
            Clear();
            Collected?.Invoke(amount);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Collect();
        }

        private void Update()
        {
            if (!waiting || popRoutine != null) return;
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            transform.localScale = baseScale * (1f + waitingPulse * wave);
        }

        private IEnumerator Pop()
        {
            float elapsed = 0f;
            while (elapsed < popDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / popDuration);
                transform.localScale = baseScale * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(progress));
                yield return null;
            }

            transform.localScale = baseScale;
            popRoutine = null;
        }

        private IEnumerator AutoCollect()
        {
            float elapsed = 0f;
            while (elapsed < autoCollectDelay)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            autoCollectRoutine = null;
            Collect();
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        private void CaptureBaseScale()
        {
            if (baseScaleCaptured) return;
            baseScale = transform.localScale;
            baseScaleCaptured = true;
        }
    }
}