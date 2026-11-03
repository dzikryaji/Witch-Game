using System;
using NUnit.Framework;
using Witch.Presentation.Shop;

namespace Witch.Bootstrap.PlayModeTests
{
    public sealed class ShopDayCycleViewTests
    {
        [TestCase(240, 240, 0f)]
        [TestCase(120, 240, 0.5f)]
        [TestCase(0, 240, 1f)]
        [TestCase(300, 240, 0f)]
        public void ProgressTracksRemainingFraction(int remainingSeconds, int durationSeconds, float expected)
        {
            float progress = ShopDayCycleView.CalculateProgress(
                TimeSpan.FromSeconds(remainingSeconds),
                TimeSpan.FromSeconds(durationSeconds));

            Assert.That(progress, Is.EqualTo(expected).Within(0.001f));
        }
    }
}
