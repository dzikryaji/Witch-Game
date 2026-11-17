using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Witch.Presentation.Shop
{
    [DisallowMultipleComponent]
    public sealed class BrewingAnimationView : MonoBehaviour
    {
        [SerializeField] private Image baseImage;
        [SerializeField] private GameObject animationRoot;
        [SerializeField] private RectTransform stirrer;
        [SerializeField] private Image steam;
        [SerializeField, Min(0.1f)] private float loopSeconds = 1.5f;

        private static readonly float[] StirrerRotationFrames = { 0f, 8f, 18f, 29f, 60f };
        private static readonly float[] StirrerRotationValues = { 0f, 6f, 26f, 53f, 0f };
        private static readonly float[] StirrerMoveFrames = { 0f, 8f, 18f, 39f, 60f };
        private static readonly float[] StirrerMoveX = { 0f, 0f, -0.6f, 15.7f, 0f };
        private static readonly float[] StirrerMoveY = { 0f, 0f, -1.1f, -3.4f, 0f };
        private static readonly float[] SteamRiseFrames = { 0f, 7f, 16f, 23f, 26f, 36f, 60f };
        private static readonly float[] SteamRiseValues = { 0f, -0.9f, 15.2f, 46.4f, 79f, 87.8f, 87.8f };
        private static readonly float[] SteamAlphaFrames = { 0f, 7f, 16f, 26f, 36f, 44f, 54f, 60f };
        private static readonly float[] SteamAlphaValues = { 0f, 0.5f, 0.7f, 0.6f, 0.5f, 0.3f, 0.1f, 0f };

        private Color baseColor;
        private Vector2 stirrerRestPosition;
        private float stirrerRestRotation;
        private Vector2 steamRestPosition;
        private bool restCaptured;

        public Func<bool> IsGameplayPaused { get; set; }
        public bool IsPlaying { get; private set; }

        private bool Paused => IsGameplayPaused != null && IsGameplayPaused();

        public async Task PlayAsync(float duration, CancellationToken cancellationToken)
        {
            if (IsPlaying || animationRoot == null) return;
            CaptureRest();
            IsPlaying = true;
            Show(true);
            try
            {
                float elapsed = 0f;
                while (IsPlaying && elapsed < duration)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Apply(Mathf.Repeat(elapsed / loopSeconds, 1f) * 60f);
                    await Task.Yield();
                    if (this == null) return;
                    if (!Paused) elapsed += Time.unscaledDeltaTime;
                }
            }
            finally
            {
                if (this != null) Stop();
            }
        }

        public void Stop()
        {
            IsPlaying = false;
            if (restCaptured)
            {
                Apply(0f);
                Show(false);
            }
        }

        private void CaptureRest()
        {
            if (restCaptured) return;
            if (baseImage != null) baseColor = baseImage.color;
            if (stirrer != null)
            {
                stirrerRestPosition = stirrer.anchoredPosition;
                stirrerRestRotation = stirrer.localEulerAngles.z;
            }
            if (steam != null) steamRestPosition = steam.rectTransform.anchoredPosition;
            restCaptured = true;
        }

        private void Show(bool playing)
        {
            if (baseImage != null)
            {
                Color color = baseColor;
                if (playing) color.a = 0f;
                baseImage.color = color;
            }
            if (animationRoot != null) animationRoot.SetActive(playing);
        }

        private void Apply(float frame)
        {
            if (stirrer != null)
            {
                stirrer.anchoredPosition = stirrerRestPosition + new Vector2(
                    Evaluate(StirrerMoveFrames, StirrerMoveX, frame),
                    Evaluate(StirrerMoveFrames, StirrerMoveY, frame));
                stirrer.localRotation = Quaternion.Euler(
                    0f, 0f, stirrerRestRotation + Evaluate(StirrerRotationFrames, StirrerRotationValues, frame));
            }

            if (steam != null)
            {
                steam.rectTransform.anchoredPosition = steamRestPosition
                    + Vector2.up * Evaluate(SteamRiseFrames, SteamRiseValues, frame);
                Color color = steam.color;
                color.a = Evaluate(SteamAlphaFrames, SteamAlphaValues, frame);
                steam.color = color;
            }
        }

        private static float Evaluate(float[] frames, float[] values, float frame)
        {
            if (frame <= frames[0]) return values[0];
            for (int index = 1; index < frames.Length; index++)
            {
                if (frame > frames[index]) continue;
                float t = Mathf.InverseLerp(frames[index - 1], frames[index], frame);
                return Mathf.Lerp(values[index - 1], values[index], t);
            }

            return values[values.Length - 1];
        }
    }
}
