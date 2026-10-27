using NUnit.Framework;
using Witch.Domain.Progression;

namespace Witch.Domain.Tests
{
    public sealed class RewindBoardTests
    {
        [Test]
        public void Create_SameSeedAndCountProducesSameBoard()
        {
            RewindBoard first = RewindBoard.Create(4242, 2);
            RewindBoard second = RewindBoard.Create(4242, 2);

            Assert.That(first.PairCount, Is.EqualTo(5));
            Assert.That(first.Cards, Is.EqualTo(second.Cards));
            Assert.That(first.Cards.Count, Is.EqualTo(10));
            for (int pair = 0; pair < first.PairCount; pair++)
            {
                Assert.That(System.Linq.Enumerable.Count(first.Cards, card => card == pair), Is.EqualTo(2));
            }
        }
    }
}
