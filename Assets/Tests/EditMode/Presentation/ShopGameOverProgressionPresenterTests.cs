using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Orders;
using Witch.Domain.Potions;
using Witch.Domain.Progression;
using Witch.Presentation.Shop;

namespace Witch.Presentation.Tests
{
    public sealed class ShopGameOverProgressionPresenterTests
    {
        [Test]
        public void RenderProgression_GameOver_ShowsFailedSummaryAndRetryLabel()
        {
            var root = new GameObject("ShopShellPresenter.GameOverTest");
            try
            {
                var presenter = root.AddComponent<ShopShellPresenter>();
                var panel = new GameObject("ProgressionPanel", typeof(RectTransform));
                panel.transform.SetParent(root.transform, false);

                Text progressionText = CreateText("ProgressionText", panel.transform, "OLD");
                progressionText.gameObject.SetActive(true);
                Text dayTitle = CreateText("DayTitle", panel.transform);
                GameObject correctRow = CreateRow(panel.transform, "Correct", out Text correctValue);
                GameObject wrongRow = CreateRow(panel.transform, "Wrong", out Text wrongValue);
                GameObject coinsRow = CreateRow(panel.transform, "Coins", out Text coinsValue);
                GameObject taxRow = CreateRow(panel.transform, "Tax", out Text taxValue);
                GameObject remainRow = CreateRow(panel.transform, "Remaining", out Text remainValue);

                var buttonGo = new GameObject("Next Day", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonGo.transform.SetParent(panel.transform, false);
                var label = CreateText("Label", buttonGo.transform, "Continue");
                Button continueButton = buttonGo.GetComponent<Button>();

                SetPrivate(presenter, "progressionPanel", panel);
                SetPrivate(presenter, "progressionText", progressionText);
                SetPrivate(presenter, "dayTitleText", dayTitle);
                SetPrivate(presenter, "correctOrdersRow", correctRow);
                SetPrivate(presenter, "correctOrdersValueText", correctValue);
                SetPrivate(presenter, "wrongOrdersRow", wrongRow);
                SetPrivate(presenter, "wrongOrdersValueText", wrongValue);
                SetPrivate(presenter, "coinsEarnedRow", coinsRow);
                SetPrivate(presenter, "coinsEarnedValueText", coinsValue);
                SetPrivate(presenter, "taxPaidRow", taxRow);
                SetPrivate(presenter, "taxPaidValueText", taxValue);
                SetPrivate(presenter, "coinsRemainingRow", remainRow);
                SetPrivate(presenter, "coinsRemainingValueText", remainValue);
                var resolveExpense = new ResolveExpenseUseCase(
                    new ExpenseRules(14),
                    new DayProgressionSetup(
                        new Dictionary<int, TimeSpan>
                        {
                            [1] = TimeSpan.FromSeconds(1),
                            [2] = TimeSpan.FromSeconds(1)
                        },
                        new Dictionary<int, int> { [1] = 20, [2] = 25 },
                        1),
                    new NoopSaveRepository(),
                    new FixedRandomSource(),
                    new FixedClock());
                SetPrivate(presenter, "continueButton", continueButton);
                SetPrivate(presenter, "resolveExpense", resolveExpense);

                GameSession session = new StartNewRunUseCase().Execute(new StartNewRunCommand(
                    "run.gameover.panel",
                    7,
                    new[] { "category.warm" },
                    new[] { "herb.ginger", "herb.mint", "herb.sage" },
                    new[] { "guidebook.warm" },
                    100,
                    TimeSpan.FromSeconds(1),
                    "expense.day.2",
                    25,
                    1,
                    DateTimeOffset.UtcNow)).Session;

                RunState day2State = session.RunState.WithProgress(2, 7, 0, 0);
                var day2Snapshot = new DayStartSnapshot(
                    day2State,
                    session.DayStartSnapshot.OrderSeed,
                    session.DayStartSnapshot.TimerDuration,
                    "expense.day.2",
                    25,
                    session.DayStartSnapshot.SaveVersion,
                    session.DayStartSnapshot.CapturedAtUtc);
                var day2Metadata = new ActiveAttemptMetadata(
                    session.AttemptMetadata.RunId,
                    2,
                    0,
                    0,
                    false,
                    0,
                    session.AttemptMetadata.ActiveOrderSeed,
                    1,
                    4,
                    false,
                    3,
                    12,
                    1);

                SetSessionState(session, "RunState", day2State);
                SetSessionState(session, "DayStartSnapshot", day2Snapshot);
                SetSessionState(session, "AttemptMetadata", day2Metadata);

                // Expense resolution requires ClosingEncounter phase (real flow reaches it after day closes).
                SetSessionState(session, "CurrentPhase", GamePhase.ClosingEncounter);

                ExpenseResolutionResult result = resolveExpense.ExecuteAsync(session).GetAwaiter().GetResult();
                Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
                Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.GameOver));
                Assert.That(session.LastDaySummary, Is.Not.Null);

                typeof(ShopShellPresenter)
                    .GetMethod("RenderProgression", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(presenter, new object[] { session });

                Assert.That(panel.activeSelf, Is.True);
                Assert.That(continueButton.gameObject.activeSelf, Is.True);
                Assert.That(label.text, Is.EqualTo("Retry"));
                Assert.That(dayTitle.gameObject.activeSelf, Is.True);
                Assert.That(dayTitle.text, Is.EqualTo("DAY 2 FAILED"));
                Assert.That(correctValue.text, Is.EqualTo(session.LastDaySummary.CorrectOrders.ToString()));
                Assert.That(wrongValue.text, Is.EqualTo(session.LastDaySummary.WrongOrders.ToString()));
                Assert.That(coinsValue.text, Is.EqualTo("$" + session.LastDaySummary.CoinsEarned));
                Assert.That(taxValue.text, Is.EqualTo("$25"));
                Assert.That(remainValue.text, Is.EqualTo("Failed"));
                Assert.That(session.LastDaySummary.CoinsRemaining, Is.EqualTo(7));
                Assert.That(progressionText.gameObject.activeSelf, Is.False);
                Assert.That(correctRow.activeSelf && wrongRow.activeSelf && coinsRow.activeSelf && taxRow.activeSelf && remainRow.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Text CreateText(string name, Transform parent, string value = "")
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.text = value;
            return text;
        }

        private static GameObject CreateRow(Transform parent, string name, out Text value)
        {
            var row = new GameObject(name + "Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            value = CreateText(name + "Value", row.transform);
            return row;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, "Missing field: " + field);
            info.SetValue(target, value);
        }

        private static void SetSessionState(object session, string propertyName, object value)
        {
            PropertyInfo property = session.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, "Missing property: " + propertyName);
            MethodInfo setter = property.GetSetMethod(true);
            if (setter != null)
            {
                setter.Invoke(session, new[] { value });
                return;
            }

            FieldInfo backing = session.GetType().GetField("<" + propertyName + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(backing, Is.Not.Null, "Missing setter/backing field: " + propertyName);
            backing.SetValue(session, value);
        }

        private sealed class FixedClock : IClock
        {
            public DateTimeOffset UtcNow { get; } = DateTimeOffset.UtcNow;
        }

        private sealed class FixedRandomSource : IRandomSource
        {
            public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
        }

        private sealed class NoopSaveRepository : IRunStateTransactionRepository
        {
            public Task<SaveStateResult> SaveRunStateAsync(DayStartSnapshot snapshot, ActiveAttemptMetadata metadata, CancellationToken cancellationToken = default) =>
                Task.FromResult(SaveStateResult.Success());
        }
    }
}
