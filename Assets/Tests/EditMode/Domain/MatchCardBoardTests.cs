using NUnit.Framework;
using Witch.Domain.Progression;

namespace Witch.Domain.Tests
{
    public sealed class MatchCardBoardTests
    {
        [Test]
        public void Create_SameSeedAndCountProducesSameBoard()
        {
            MatchCardBoard first = MatchCardBoard.Create(4242, 2);
            MatchCardBoard second = MatchCardBoard.Create(4242, 2);

            Assert.That(first.PairCount, Is.EqualTo(10));
            Assert.That(first.Cards, Is.EqualTo(second.Cards));
            Assert.That(first.Cards.Count, Is.EqualTo(20));
            for (int pair = 0; pair < first.PairCount; pair++)
            {
                Assert.That(System.Linq.Enumerable.Count(first.Cards, card => card == pair), Is.EqualTo(2));
            }
        }

        [Test]
        public void Create_UsesAuthoredDifficultyRange()
        {
            Assert.That(MatchCardBoard.Create(42, 0, 8, 10).PairCount, Is.EqualTo(8));
            Assert.That(MatchCardBoard.Create(42, 9, 8, 10).PairCount, Is.EqualTo(10));
        }
    }
}
