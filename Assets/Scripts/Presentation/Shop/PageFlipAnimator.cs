using System;
using System.Collections;
using UnityEngine;

namespace Witch.Presentation.Shop
{
    /// <summary>
    /// Plays the page-curl animation on the guidebook. It owns no page index and fills no
    /// content: ShopShellPresenter renders FlipFront / FlipBack / the page underneath, then
    /// calls <see cref="Play"/>. All objects are authored in GuidebookBook.prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PageFlipAnimator : MonoBehaviour
    {
        // Fraction of TurnClip's height that sits below the bottom of the book.
        private const float ClipBelowFold = 0.35f;

        [Header("Authored objects")]
        [SerializeField] private RectTransform spread;
        [SerializeField] private RectTransform turnClip;
        [SerializeField] private RectTransform flipFront;
        [SerializeField] private RectTransform flipBack;
        [SerializeField] private GameObject shadowForward;
        [SerializeField] private GameObject shadowBackward;

        [Header("Feel")]
        [SerializeField, Min(0.05f)] private float duration = 0.6f;
        [Tooltip("How high the page corner lifts mid-turn, as a fraction of the page height.")]
        [SerializeField, Range(0f, 1f)] private float arcHeight = 0.4f;
        [Tooltip("Keep on so pages still turn while the game is paused (timeScale = 0).")]
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Coroutine routine;

        public bool IsPlaying => routine != null;

        public bool IsAuthored => spread != null && turnClip != null && flipFront != null && flipBack != null;

        private void Awake()
        {
            if (turnClip != null) turnClip.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            // The book was hidden mid-turn: drop the animation. The presenter re-renders on open.
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (turnClip != null) turnClip.gameObject.SetActive(false);
        }

        /// <summary>
        /// Starts a page turn. Render the pages in the same frame, before or after this call.
        /// <paramref name="onComplete"/> runs when the page lands. Returns false when a turn is
        /// already playing or the animator cannot run.
        /// </summary>
        public bool Play(FlipDirection direction, Action onComplete)
        {
            if (IsPlaying || !IsAuthored || !isActiveAndEnabled) return false;
            routine = StartCoroutine(Run(direction, onComplete));
            return true;
        }

        private IEnumerator Run(FlipDirection direction, Action onComplete)
        {
            Rect area = spread.rect;
            float pageWidth = area.width * 0.5f;
            float spineX = area.center.x;
            bool forward = direction == FlipDirection.Forward;
            float startX = forward ? spineX + pageWidth : spineX - pageWidth;
            float endX = forward ? spineX - pageWidth : spineX + pageWidth;

            turnClip.gameObject.SetActive(true);
            if (shadowForward != null) shadowForward.SetActive(forward);
            if (shadowBackward != null) shadowBackward.SetActive(!forward);

            float elapsed = 0f;
            while (true)
            {
                float progress = easing.Evaluate(Mathf.Clamp01(elapsed / duration));
                float x = Mathf.Lerp(startX, endX, progress);
                float across = (x - spineX) / pageWidth;                                 // -1 … 1
                float y = area.yMin + area.height * arcHeight * (1f - across * across); // lands flat
                Apply(direction, new Vector2(x, y), area);

                if (elapsed >= duration) break;
                yield return null;
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            }

            turnClip.gameObject.SetActive(false);
            routine = null;
            onComplete?.Invoke();
        }

        private void Apply(FlipDirection direction, Vector2 follow, Rect area)
        {
            bool forward = direction == FlipDirection.Forward;
            FoldPose pose = PageFoldMath.Solve(area, direction, follow);

            // The mask: one edge lies on the fold line, the body covers the spine side.
            float foldAngle = Mathf.Atan2(pose.FoldUp.y, pose.FoldUp.x) * Mathf.Rad2Deg - 90f;
            var clipRight = new Vector2(pose.FoldUp.y, -pose.FoldUp.x);
            Vector2 clipSize = turnClip.rect.size;
            Vector2 edgeBottom = pose.FoldBase - pose.FoldUp * (clipSize.y * ClipBelowFold);
            Vector2 clipBottomLeft = forward ? edgeBottom - clipRight * clipSize.x : edgeBottom;
            Place(turnClip, clipBottomLeft, foldAngle);

            // The part not turned yet stays exactly where the page rests.
            Vector2 pageBottomLeft = forward
                ? new Vector2(area.center.x, area.yMin)
                : new Vector2(area.xMin, area.yMin);
            Place(flipFront, pageBottomLeft, 0f);

            // The turned-over part.
            Place(flipBack, pose.FlapBottomLeft, pose.FlapAngleDeg);
        }

        // Puts a rect's bottom-left corner at a point in spread space, rotated, whatever its pivot.
        private void Place(RectTransform target, Vector2 bottomLeft, float angleDeg)
        {
            Quaternion rotation = Quaternion.Euler(0f, 0f, angleDeg);
            Vector2 size = target.rect.size;
            Vector3 pivotOffset = rotation * new Vector3(target.pivot.x * size.x, target.pivot.y * size.y, 0f);
            target.position = spread.TransformPoint((Vector3)bottomLeft + pivotOffset);
            target.rotation = spread.rotation * rotation;
        }
    }
}
