using System;
using NUnit.Framework;
using Witch.Domain.Progression;

namespace Witch.Domain.Tests
{
    public sealed class WitchHunterStageRulesTests
    {
        [Test]
        public void NoCompletedMinigameWins_ResolvesFirstStage()
        {
            Assert.That(WitchHunterStageRules.ResolveStageIndex(0, 5), Is.Zero);
        }

        [Test]
        public void OneCompletedMinigameWin_ResolvesSecondStage()
        {
            Assert.That(WitchHunterStageRules.ResolveStageIndex(1, 5), Is.EqualTo(1));
        }

        [Test]
        public void LastCompletedMinigameWin_ResolvesLastStage()
        {
            Assert.That(WitchHunterStageRules.ResolveStageIndex(4, 5), Is.EqualTo(4));
        }

        [Test]
        public void MinigameWinsAboveLastStage_StayOnLastStage()
        {
            Assert.That(WitchHunterStageRules.ResolveStageIndex(5, 5), Is.EqualTo(4));
            Assert.That(WitchHunterStageRules.ResolveStageIndex(99, 5), Is.EqualTo(4));
        }

        [Test]
        public void SingleStage_AlwaysResolvesZero()
        {
            Assert.That(WitchHunterStageRules.ResolveStageIndex(0, 1), Is.Zero);
            Assert.That(WitchHunterStageRules.ResolveStageIndex(7, 1), Is.Zero);
        }

        [Test]
        public void NonPositiveStageCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WitchHunterStageRules.ResolveStageIndex(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WitchHunterStageRules.ResolveStageIndex(0, -3));
        }

        [Test]
        public void NegativeCompletedMinigameWins_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WitchHunterStageRules.ResolveStageIndex(-1, 5));
        }
    }
}
