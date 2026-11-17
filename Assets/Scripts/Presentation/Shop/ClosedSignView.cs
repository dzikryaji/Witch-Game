using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Witch.Application.Ports;

namespace Witch.Presentation.Shop
{
    [DisallowMultipleComponent]
    public sealed class ClosedSignView : MonoBehaviour
    {
        [Tooltip("How long the sign takes to drop into place.")]
        [SerializeField, Min(0f)] private float dropDuration = 0.35f;

        [Tooltip("Anchored Y position where the sign starts before dropping.")]
        [SerializeField] private float startY = 100f;

        [Tooltip("Image alpha at the start of the drop.")]
        [SerializeField, Range(0f, 1f)] private float startAlpha = 0.8f;

        [Tooltip("Image alpha when the sign is fully shown.")]
        [SerializeField, Range(0f, 1f)] private float endAlpha = 1f;

        [Tooltip("Delay before playing the discard sound when the drop starts.")]
        [SerializeField, Min(0f)] private float soundDelaySeconds = 0f;

        private RectTransform rectTransform;
        private Image image;
        private Vector2 restingAnchoredPosition;
        private bool restingPositionCaptured;
        private bool visible;
        private bool entrancePlayedThisDay;
        private Coroutine dropRoutine;
        private Coroutine soundRoutine;

        public Func<bool> IsGameplayPaused { get; set; }
        public IGameAudioService AudioService { get; set; }
        private bool Paused => IsGameplayPaused != null && IsGameplayPaused();

        private void Awake()
        {
            EnsureInitialized();
        }

        public bool IsVisible => visible;
        public bool EntrancePlayedThisDay => entrancePlayedThisDay;

        public void ShowInstant()
        {
            EnsureInitialized();
            StopDropRoutine();
            visible = true;
            gameObject.SetActive(true);
            SetRestingState(endAlpha);
            ApplyRaycastState();
        }

        public void PlayEntrance()
        {
            EnsureInitialized();
            if (entrancePlayedThisDay) return;

            StopDropRoutine();
            visible = true;
            entrancePlayedThisDay = true;
            gameObject.SetActive(true);
            ApplyRaycastState();

            if (dropDuration <= 0f || !UnityEngine.Application.isPlaying || !isActiveAndEnabled)
            {
                SetRestingState(endAlpha);
                dropRoutine = null;
                TryPlayEntranceSound();
                return;
            }

            rectTransform.anchoredPosition = StartPosition();
            SetImageAlpha(startAlpha);
            TryPlayEntranceSound();
            dropRoutine = StartCoroutine(DropRoutine());
        }

        public void HideAndReset()
        {
            EnsureInitialized();
            StopRoutines();
            visible = false;
            entrancePlayedThisDay = false;
            SetRestingState(startAlpha);
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            StopRoutines();
        }

        private IEnumerator DropRoutine()
        {
            Vector2 start = StartPosition();
            float elapsed = 0f;
            while (elapsed < dropDuration)
            {
                if (!Paused) elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / Mathf.Max(dropDuration, 0.0001f));
                float eased = EaseOutCubic(progress);
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(start, restingAnchoredPosition, eased);
                SetImageAlpha(Mathf.LerpUnclamped(startAlpha, endAlpha, eased));
                yield return null;
            }

            SetRestingState(endAlpha);
            dropRoutine = null;
        }

        private void TryPlayEntranceSound()
        {
            StopSoundRoutine();
            if (soundDelaySeconds <= 0f)
            {
                AudioService?.Play(GameAudioCueIds.Discard);
                return;
            }

            if (isActiveAndEnabled)
            {
                soundRoutine = StartCoroutine(PlaySoundAfterDelay());
            }
        }

        private IEnumerator PlaySoundAfterDelay()
        {
            float elapsed = 0f;
            while (elapsed < soundDelaySeconds)
            {
                if (!Paused) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            soundRoutine = null;
            AudioService?.Play(GameAudioCueIds.Discard);
        }

        private void SetRestingState(float alpha)
        {
            rectTransform.anchoredPosition = restingAnchoredPosition;
            SetImageAlpha(alpha);
        }

        private Vector2 StartPosition()
        {
            return new Vector2(restingAnchoredPosition.x, startY);
        }

        private void SetImageAlpha(float alpha)
        {
            if (image == null) return;
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        private static float EaseOutCubic(float x)
        {
            float t = Mathf.Clamp01(x);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        private void EnsureInitialized()
        {
            if (rectTransform == null) rectTransform = transform as RectTransform;
            if (image == null) image = GetComponent<Image>();
            if (rectTransform != null && !restingPositionCaptured)
            {
                restingAnchoredPosition = rectTransform.anchoredPosition;
                restingPositionCaptured = true;
            }

            ApplyRaycastState();
        }

        private void ApplyRaycastState()
        {
            if (image != null) image.raycastTarget = false;
        }

        private void StopDropRoutine()
        {
            if (dropRoutine != null)
            {
                StopCoroutine(dropRoutine);
                dropRoutine = null;
            }
        }

        private void StopSoundRoutine()
        {
            if (soundRoutine != null)
            {
                StopCoroutine(soundRoutine);
                soundRoutine = null;
            }
        }

        private void StopRoutines()
        {
            StopDropRoutine();
            StopSoundRoutine();
        }
    }
}
