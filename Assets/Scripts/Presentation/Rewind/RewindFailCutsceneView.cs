using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Witch.Presentation.Rewind
{
    public sealed class RewindFailCutsceneView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup blackout;   // full-screen black Image
        [SerializeField] private CanvasGroup picture;    // full-screen cutscene picture, above the blackout
        [SerializeField, Min(0.1f)] private float fadeSeconds = 2f;
        [SerializeField, Min(0f)] private float blackHoldSeconds = 2f;
        [SerializeField, Min(0f)] private float holdSeconds = 5f;   // how long the picture shows before the popup appears

        public bool IsAuthored => root != null && blackout != null && picture != null;

        public async Task PlayAsync(CancellationToken cancellationToken, Action onPictureRevealed = null)
        {
            root.SetActive(true);
            blackout.alpha = 0f;
            picture.alpha = 0f;
            blackout.blocksRaycasts = true;                  // nothing behind it can be clicked

            // Fade the game screen to black.
            await Fade(blackout, 0f, 1f, cancellationToken);

            // Keep the screen completely black for a few seconds.
            if (blackHoldSeconds > 0f)
            {
                await Task.Delay(
                    Mathf.RoundToInt(blackHoldSeconds * 1000f),
                    cancellationToken
                );
            }

            // Fade in the witch picture.
            await Fade(picture, 0f, 1f, cancellationToken);
            onPictureRevealed?.Invoke();
            if (holdSeconds > 0f)
            {
                await Task.Delay(Mathf.RoundToInt(holdSeconds * 1000f), cancellationToken);
            }
            // The picture stays on screen. The game over popup appears on top of it.
        }

        private async Task Fade(CanvasGroup group, float from, float to, CancellationToken cancellationToken)
        {
            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeSeconds));
                await Task.Yield();
            }
            group.alpha = to;
        }
    }
}
