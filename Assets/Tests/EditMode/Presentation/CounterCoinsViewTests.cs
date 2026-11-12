using NUnit.Framework;
using UnityEngine;
using Witch.Presentation.Shop;

namespace Witch.Presentation.Tests
{
    public sealed class CounterCoinsViewTests
    {
        private GameObject coinsObject;
        private CounterCoinsView coins;

        [SetUp]
        public void SetUp()
        {
            coinsObject = new GameObject("Counter Coins", typeof(RectTransform));
            coins = coinsObject.AddComponent<CounterCoinsView>();
            coins.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(coinsObject);
        }

        [Test]
        public void Add_ShowsCoinsAndStacksUncollectedPayments()
        {
            coins.Add(10);
            coins.Add(10);

            Assert.That(coinsObject.activeSelf, Is.True);
            Assert.That(coins.Amount, Is.EqualTo(20));
            Assert.That(coins.HasCoins, Is.True);
        }

        [Test]
        public void Collect_HidesCoinsAndRaisesCollectedOnce()
        {
            int collected = 0;
            coins.Collected += amount => collected += amount;
            coins.Add(10);

            coins.Collect();
            coins.Collect();

            Assert.That(collected, Is.EqualTo(10));
            Assert.That(coins.Amount, Is.Zero);
            Assert.That(coinsObject.activeSelf, Is.False);
        }

        [Test]
        public void Clear_RemovesCoinsWithoutCollectedEvent()
        {
            int collected = 0;
            coins.Collected += amount => collected += amount;
            coins.Add(10);

            coins.Clear();

            Assert.That(collected, Is.Zero);
            Assert.That(coins.Amount, Is.Zero);
            Assert.That(coinsObject.activeSelf, Is.False);
        }

        [Test]
        public void Add_IgnoresZeroOrNegativePayments()
        {
            coins.Add(0);
            coins.Add(-5);

            Assert.That(coins.HasCoins, Is.False);
            Assert.That(coinsObject.activeSelf, Is.False);
        }
    }
}
