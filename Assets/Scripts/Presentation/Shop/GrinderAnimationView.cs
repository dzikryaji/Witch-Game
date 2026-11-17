using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Witch.Presentation.Shop
{
    [DisallowMultipleComponent]
    public sealed class GrinderAnimationView : MonoBehaviour
    {
        [SerializeField] private Image baseImage;
        [SerializeField] private Image frameImage;
        [SerializeField] private Sprite restSprite;
        [SerializeField] private Sprite downSprite;
        [SerializeField] private Sprite upSprite;
        [SerializeField, Min(0.01f)] private float frameSeconds = 0.2333f;

        private Color baseColor;
        private bool baseColorCaptured;

        public Func<bool> IsGameplayPaused { get; set; }
        public bool IsPlaying { get; private set; }

        private bool Paused => IsGameplayPaused != null && IsGameplayPaused();

        public async Task PlayAsync(float duration, CancellationToken cancellationToken)
        {
            if (IsPlaying || frameImage == null || restSprite == null) return;
            IsPlaying = true;
            Show(true);
            try
            {
                float elapsed = 0f;
                while (IsPlaying && elapsed < duration)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int step = Mathf.FloorToInt(elapsed / frameSeconds) % 4;
                    Sprite frame = step == 1 ? downSprite : step == 3 ? upSprite : restSprite;
                    frameImage.sprite = frame != null ? frame : restSprite;
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
            Show(false);
        }

        private void Show(bool playing)
        {
            if (baseImage != null)
            {
                if (!baseColorCaptured)
                {
                    baseColor = baseImage.color;
                    baseColorCaptured = true;
                }
                Color color = baseColor;
                if (playing) color.a = 0f;
                baseImage.color = color;
            }

            if (frameImage != null)
            {
                frameImage.sprite = restSprite;
                frameImage.gameObject.SetActive(playing);
            }
        }
    }
}
