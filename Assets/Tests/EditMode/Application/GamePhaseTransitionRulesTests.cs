using NUnit.Framework;
using Witch.Application.Flow;
using Witch.Domain;

namespace Witch.Application.Tests
{
    public sealed class GamePhaseTransitionRulesTests
    {
        [TestCase(GamePhase.ShopPreparation, GamePhase.CustomerArrival)]
        [TestCase(GamePhase.CustomerArrival, GamePhase.CustomerEncounter)]
        [TestCase(GamePhase.CustomerArrival, GamePhase.ClosingEncounter)]
        [TestCase(GamePhase.CustomerEncounter, GamePhase.CustomerArrival)]
        [TestCase(GamePhase.CustomerEncounter, GamePhase.ClosingEncounter)]
        [TestCase(GamePhase.CustomerEncounter, GamePhase.RewindPending)]
        [TestCase(GamePhase.ClosingEncounter, GamePhase.ExpenseCheck)]
        [TestCase(GamePhase.ClosingEncounter, GamePhase.RewindPending)]
        [TestCase(GamePhase.ExpenseCheck, GamePhase.DaySummary)]
        [TestCase(GamePhase.ExpenseCheck, GamePhase.Victory)]
        [TestCase(GamePhase.ExpenseCheck, GamePhase.GameOver)]
        [TestCase(GamePhase.RewindPending, GamePhase.RewindMinigame)]
        [TestCase(GamePhase.RewindMinigame, GamePhase.ShopPreparation)]
        [TestCase(GamePhase.DaySummary, GamePhase.ShopPreparation)]
        [TestCase(GamePhase.GameOver, GamePhase.ShopPreparation)]
        public void RequiredTransition_IsLegal(GamePhase from, GamePhase to)
        {
            Assert.That(GamePhaseTransitionRules.CanTransition(from, to), Is.True);
        }

        [TestCase(GamePhase.ShopPreparation, GamePhase.ExpenseCheck)]
        [TestCase(GamePhase.CustomerArrival, GamePhase.Victory)]
        [TestCase(GamePhase.CustomerEncounter, GamePhase.ExpenseCheck)]
        [TestCase(GamePhase.RewindPending, GamePhase.ShopPreparation)]
        [TestCase(GamePhase.Victory, GamePhase.ShopPreparation)]
        public void SkippedOrTerminalTransition_IsIllegal(GamePhase from, GamePhase to)
        {
            Assert.That(GamePhaseTransitionRules.CanTransition(from, to), Is.False);
        }
    }
}
