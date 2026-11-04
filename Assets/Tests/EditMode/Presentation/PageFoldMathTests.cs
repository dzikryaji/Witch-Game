using NUnit.Framework;
using UnityEngine;
using Witch.Presentation.Shop;

namespace Witch.Presentation.Tests
{
    public sealed class PageFoldMathTests
    {
        // Two 400 x 600 pages side by side, centred on the origin.
        private static readonly Rect Spread = new Rect(-400f, -300f, 800f, 600f);
        private const float Tolerance = 0.01f;

        [Test]
        public void Forward_AtRest_FoldSitsOnOuterEdge()
        {
            FoldPose pose = PageFoldMath.Solve(Spread, FlipDirection.Forward, new Vector2(400f, -300f));
            Assert.That(pose.FoldBase.x, Is.EqualTo(400f).Within(Tolerance));
        }

        [Test]
        public void Forward_Landed_FlapCoversLeftPage()
        {
            FoldPose pose = PageFoldMath.Solve(Spread, FlipDirection.Forward, new Vector2(-400f, -300f));
            Assert.That(pose.FoldBase.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(pose.FlapBottomLeft.x, Is.EqualTo(-400f).Within(Tolerance));
            Assert.That(pose.FlapBottomLeft.y, Is.EqualTo(-300f).Within(Tolerance));
            Assert.That(Mathf.DeltaAngle(0f, pose.FlapAngleDeg), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Backward_Landed_FlapCoversRightPage()
        {
            FoldPose pose = PageFoldMath.Solve(Spread, FlipDirection.Backward, new Vector2(400f, -300f));
            Assert.That(pose.FlapBottomLeft.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(pose.FlapBottomLeft.y, Is.EqualTo(-300f).Within(Tolerance));
            Assert.That(Mathf.DeltaAngle(0f, pose.FlapAngleDeg), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void CornerPulledFarAway_StaysWithinOnePageWidthOfSpine()
        {
            FoldPose pose = PageFoldMath.Solve(Spread, FlipDirection.Forward, new Vector2(2000f, 900f));
            Assert.That((pose.Corner - new Vector2(0f, -300f)).magnitude, Is.LessThanOrEqualTo(400f + Tolerance));
        }

        [Test]
        public void FoldBase_NeverCrossesSpine()
        {
            FoldPose pose = PageFoldMath.Solve(Spread, FlipDirection.Forward, new Vector2(-300f, 250f));
            Assert.That(pose.FoldBase.x, Is.GreaterThanOrEqualTo(-Tolerance));
        }
    }
}
