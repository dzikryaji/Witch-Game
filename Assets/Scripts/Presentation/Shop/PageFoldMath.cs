// Page-curl geometry. The technique (two masks that follow the fold line) comes from the
// "Book - Page Curl" asset by Dandarawy, itself based on rbarraza's HTML5 canvas page flip.
// Rewritten here as pure math with no state, so it can be tested in EditMode.
using UnityEngine;

namespace Witch.Presentation.Shop
{
    /// <summary>Forward = the right page turns over to the left (Next). Backward = the left page turns to the right (Previous).</summary>
    public enum FlipDirection { Forward, Backward }

    /// <summary>Everything the animator needs for one frame, in the spread's local space.</summary>
    public readonly struct FoldPose
    {
        public FoldPose(Vector2 corner, Vector2 foldBase, Vector2 foldUp, Vector2 flapBottomLeft, float flapAngleDeg)
        {
            Corner = corner;
            FoldBase = foldBase;
            FoldUp = foldUp;
            FlapBottomLeft = flapBottomLeft;
            FlapAngleDeg = flapAngleDeg;
        }

        /// <summary>Constrained position of the page corner being pulled.</summary>
        public Vector2 Corner { get; }

        /// <summary>Where the fold line meets the bottom edge of the book.</summary>
        public Vector2 FoldBase { get; }

        /// <summary>Unit direction of the fold line, pointing upward.</summary>
        public Vector2 FoldUp { get; }

        /// <summary>Bottom-left corner of the turned-over back side.</summary>
        public Vector2 FlapBottomLeft { get; }

        /// <summary>Rotation of the turned-over back side, in degrees.</summary>
        public float FlapAngleDeg { get; }
    }

    public static class PageFoldMath
    {
        /// <param name="spread">The whole two-page spread, in its own local space.</param>
        /// <param name="direction">Which page is being turned.</param>
        /// <param name="follow">Where the page corner is being pulled to, in spread space.</param>
        public static FoldPose Solve(Rect spread, FlipDirection direction, Vector2 follow)
        {
            float pageWidth = spread.width * 0.5f;
            float pageHeight = spread.height;
            var spineBottom = new Vector2(spread.center.x, spread.yMin);
            var spineTop = new Vector2(spread.center.x, spread.yMax);
            bool forward = direction == FlipDirection.Forward;
            var bookCorner = forward
                ? new Vector2(spread.xMax, spread.yMin)
                : new Vector2(spread.xMin, spread.yMin);

            // 1. Paper does not stretch: the corner stays within one page width of the
            //    spine bottom, and within one page diagonal of the spine top.
            Vector2 corner = follow;
            if ((corner - spineBottom).magnitude > pageWidth)
            {
                corner = spineBottom + (corner - spineBottom).normalized * pageWidth;
            }

            float diagonal = Mathf.Sqrt(pageWidth * pageWidth + pageHeight * pageHeight);
            if ((corner - spineTop).magnitude > diagonal)
            {
                corner = spineTop + (corner - spineTop).normalized * diagonal;
            }

            // 2. The fold is the perpendicular bisector between the corner's resting place and where it is now.
            Vector2 mid = (corner + bookCorner) * 0.5f;
            Vector2 toRest = bookCorner - corner;
            Vector2 up = toRest.sqrMagnitude < 1e-8f ? Vector2.up : new Vector2(-toRest.y, toRest.x).normalized;
            if (up.y < 0f) up = -up;

            // 3. Where the fold meets the bottom edge. It never crosses the spine.
            float baseX = Mathf.Abs(up.y) < 1e-4f
                ? spineBottom.x
                : mid.x + (spread.yMin - mid.y) / up.y * up.x;
            float clampedX = forward
                ? Mathf.Clamp(baseX, spineBottom.x, spread.xMax)
                : Mathf.Clamp(baseX, spread.xMin, spineBottom.x);
            var foldBase = new Vector2(clampedX, spread.yMin);
            if (!Mathf.Approximately(clampedX, baseX))
            {
                Vector2 alternative = mid - foldBase;
                if (alternative.sqrMagnitude > 1e-8f)
                {
                    up = alternative.normalized;
                    if (up.y < 0f) up = -up;
                }
            }

            // 4. The turned-over flap is the page mirrored across the fold line. Its back side is
            //    drawn un-mirrored, so only its bottom edge is needed.
            Vector2 mirroredCorner = Reflect(bookCorner, foldBase, up);
            Vector2 mirroredSpine = Reflect(spineBottom, foldBase, up);
            Vector2 flapLeft = forward ? mirroredCorner : mirroredSpine;
            Vector2 flapRight = forward ? mirroredSpine : mirroredCorner;
            Vector2 axis = flapRight - flapLeft;
            float flapAngle = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;

            return new FoldPose(corner, foldBase, up, flapLeft, flapAngle);
        }

        /// <summary>Mirror a point across the line through <paramref name="origin"/> with unit direction <paramref name="direction"/>.</summary>
        public static Vector2 Reflect(Vector2 point, Vector2 origin, Vector2 direction)
        {
            Vector2 offset = point - origin;
            return origin + 2f * Vector2.Dot(offset, direction) * direction - offset;
        }
    }
}
