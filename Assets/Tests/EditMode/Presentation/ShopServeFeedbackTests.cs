using System.Linq;
using NUnit.Framework;
using Witch.Presentation.Shop;

namespace Witch.Presentation.Tests
{
    public sealed class ShopServeFeedbackTests
    {
        [Test]
        public void GetCorrectLine_ReturnsLineFromCorrectGroup()
        {
            string[] pool = ShopServeFeedback.GetCorrectLines();
            for (int i = 0; i < 50; i++)
            {
                string line = ShopServeFeedback.GetCorrectLine();
                Assert.That(pool, Does.Contain(line));
            }
        }

        [Test]
        public void GetIncorrectLine_ReturnsLineFromIncorrectGroup()
        {
            string[] pool = ShopServeFeedback.GetIncorrectLines();
            for (int i = 0; i < 50; i++)
            {
                string line = ShopServeFeedback.GetIncorrectLine();
                Assert.That(pool, Does.Contain(line));
            }
        }

        [Test]
        public void CorrectAndIncorrectPools_HaveFiveExpectedLines()
        {
            CollectionAssert.AreEqual(new[]
            {
                "Thanks for the potion!",
                "This is exactly what I needed!",
                "Perfect! Just what I asked for.",
                "Wonderful! This should do nicely.",
                "You've made exactly the right potion!"
            }, ShopServeFeedback.GetCorrectLines());

            CollectionAssert.AreEqual(new[]
            {
                "This is not the correct potion.",
                "I'm afraid this isn't what I ordered.",
                "This isn't quite what I needed.",
                "Something seems wrong with this potion.",
                "I don't think this is the right one."
            }, ShopServeFeedback.GetIncorrectLines());
        }

        [Test]
        public void FeedbackLines_HaveFewerThanTenWords()
        {
            foreach (string line in ShopServeFeedback.GetCorrectLines().Concat(ShopServeFeedback.GetIncorrectLines()))
            {
                int words = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
                Assert.That(words, Is.LessThan(10), line);
            }
        }
    }
}
