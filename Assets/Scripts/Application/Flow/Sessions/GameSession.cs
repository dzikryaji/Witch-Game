using System;
using Witch.Domain;
using Witch.Domain.Progression;
using Witch.Domain.Orders;
using Witch.Domain.Potions;

namespace Witch.Application.Flow
{
    public sealed class GameSession
    {
        internal GameSession(RunState runState, GamePhase initialPhase)
            : this(
                new DayStartSnapshot(
                    runState,
                    1,
                    TimeSpan.FromMinutes(4),
                    "expense.unspecified",
                    0,
                    1,
                    DateTimeOffset.UnixEpoch),
                new ActiveAttemptMetadata(
                    runState.RunId,
                    runState.CurrentDayNumber,
                    runState.Suspicion,
                    runState.RewindCount,
                    false,
                    0,
                    1),
                initialPhase)
        {
        }

        internal GameSession(
            DayStartSnapshot dayStartSnapshot,
            ActiveAttemptMetadata attemptMetadata,
            GamePhase initialPhase)
        {
            DayStartSnapshot = dayStartSnapshot ?? throw new ArgumentNullException(nameof(dayStartSnapshot));
            AttemptMetadata = attemptMetadata ?? throw new ArgumentNullException(nameof(attemptMetadata));
            if (!string.Equals(
                    dayStartSnapshot.RunState.RunId,
                    attemptMetadata.RunId,
                    StringComparison.Ordinal)
                || dayStartSnapshot.RunState.CurrentDayNumber != attemptMetadata.DayNumber)
            {
                throw new ArgumentException("Session snapshot and attempt metadata must identify the same run and day.");
            }

            RunState = dayStartSnapshot.RunState.WithProgress(
                attemptMetadata.DayNumber,
                dayStartSnapshot.RunState.Money,
                attemptMetadata.Suspicion,
                attemptMetadata.RewindCount);
            AttemptState = new DayAttemptState(
                attemptMetadata.ActiveOrderSeed,
                dayStartSnapshot.TimerDuration,
                null,
                null,
                null,
                0,
                new PotionDraft(),
                null);
            TimerState = new ShopTimerState(dayStartSnapshot.TimerDuration, false);
            PotionDraft = new PotionDraft();
            CurrentPhase = initialPhase;
        }

        public RunState RunState { get; private set; }

        public DayStartSnapshot DayStartSnapshot { get; private set; }

        public ActiveAttemptMetadata AttemptMetadata { get; private set; }

        public DayAttemptState AttemptState { get; private set; }

        public ShopTimerState TimerState { get; private set; }

        public DateTimeOffset? TimerStartedAtUtc { get; private set; }

        public bool TimerPaused { get; private set; }

        public bool TimerStopped { get; private set; }

        public string GroundHerbId { get; private set; }

        public PotionDraft PotionDraft { get; private set; }

        public Recipe PreparedRecipe { get; private set; }

        public bool ServiceCommitted { get; private set; }

        internal bool RewindCompletionApplied { get; private set; }

        private DateTimeOffset? lastTimerSampleUtc;

        public GamePhase CurrentPhase { get; private set; }

        internal PhaseTransitionResult TryTransition(GamePhase nextPhase)
        {
            if (!GamePhaseTransitionRules.CanTransition(CurrentPhase, nextPhase))
            {
                return PhaseTransitionResult.Failure(PhaseTransitionError.InvalidTransition, CurrentPhase);
            }

            CurrentPhase = nextPhase;
            return PhaseTransitionResult.Success(CurrentPhase);
        }

        internal void BeginCustomerEncounter(
            string customerId,
            OrderRequest order,
            DateTimeOffset nowUtc)
        {
            PhaseTransitionResult transition = TryTransition(GamePhase.CustomerEncounter);
            if (!transition.Succeeded)
            {
                throw new InvalidOperationException("Customer encounter cannot begin from the current phase.");
            }

            if (!TimerStartedAtUtc.HasValue)
            {
                TimerStartedAtUtc = nowUtc;
                lastTimerSampleUtc = nowUtc;
            }

            ServiceCommitted = false;
            ClearWorkbench();

            var usedOrderIds = new System.Collections.Generic.List<string>(AttemptState.UsedOrderIds);
            if (!usedOrderIds.Contains(order.OrderId))
            {
                usedOrderIds.Add(order.OrderId);
            }

            AttemptState = new DayAttemptState(
                AttemptState.AttemptSeed,
                TimerState.RemainingTime,
                usedOrderIds,
                customerId,
                order,
                AttemptState.EarnedMoney,
                AttemptState.PotionDraft,
                AttemptState.PreparedRecipe);
            TimerState = new ShopTimerState(TimerState.RemainingTime, true);
        }

        internal void RefreshTimer(DateTimeOffset nowUtc)
        {
            if (!TimerStartedAtUtc.HasValue || TimerStopped || TimerPaused || !lastTimerSampleUtc.HasValue)
            {
                return;
            }

            if (nowUtc <= lastTimerSampleUtc.Value)
            {
                return;
            }

            TimeSpan elapsed = nowUtc - lastTimerSampleUtc.Value;
            TimerState = TimerState.Elapse(elapsed);
            lastTimerSampleUtc = nowUtc;

            if (!TimerState.QueueOpen && CurrentPhase == GamePhase.CustomerArrival)
            {
                TryTransition(GamePhase.ClosingEncounter);
            }
        }

        internal void SetTimerPaused(bool paused, DateTimeOffset nowUtc)
        {
            if (TimerStopped || TimerPaused == paused)
            {
                return;
            }

            if (paused)
            {
                RefreshTimer(nowUtc);
                TimerPaused = true;
                return;
            }

            TimerPaused = false;
            lastTimerSampleUtc = nowUtc;
        }

        internal void StopTimer(DateTimeOffset nowUtc)
        {
            if (TimerStopped)
            {
                return;
            }

            RefreshTimer(nowUtc);
            TimerStopped = true;
            TimerPaused = false;
        }

        internal void SetGroundHerb(string herbId)
        {
            GroundHerbId = herbId;
        }

        internal void ClearGroundHerb()
        {
            GroundHerbId = null;
        }

        internal void AddGroundHerbToPot()
        {
            if (string.IsNullOrWhiteSpace(GroundHerbId))
            {
                throw new InvalidOperationException("Grind a herb before adding it to the pot.");
            }

            var ingredients = new System.Collections.Generic.List<PotIngredient>(PotionDraft.Ingredients)
            {
                new PotIngredient(GroundHerbId)
            };
            PotionDraft = new PotionDraft(ingredients);
            GroundHerbId = null;
            PreparedRecipe = null;
        }

        internal void SetPreparedRecipe(Recipe recipe)
        {
            PreparedRecipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        }

        internal void RemoveLastPotIngredient()
        {
            if (PotionDraft.Ingredients.Count == 0)
            {
                throw new InvalidOperationException("The cauldron is already empty.");
            }

            var ingredients = new System.Collections.Generic.List<PotIngredient>(PotionDraft.Ingredients);
            ingredients.RemoveAt(ingredients.Count - 1);
            PotionDraft = new PotionDraft(ingredients);
            PreparedRecipe = null;
        }

        internal void ClearWorkbench()
        {
            GroundHerbId = null;
            PotionDraft = new PotionDraft();
            PreparedRecipe = null;
        }

        internal void CommitService(ServiceOutcome outcome)
        {
            if (outcome == null)
            {
                throw new ArgumentNullException(nameof(outcome));
            }

            if (ServiceCommitted)
            {
                throw new InvalidOperationException("Service has already been committed.");
            }

            RunState = outcome.UpdatedRunState;
            AttemptMetadata = new ActiveAttemptMetadata(
                RunState.RunId,
                RunState.CurrentDayNumber,
                RunState.Suspicion,
                RunState.RewindCount,
                AttemptMetadata.PendingRewind,
                AttemptMetadata.MinigameBoardSeed,
                AttemptMetadata.ActiveOrderSeed);
            ServiceCommitted = true;
            ClearWorkbench();
            TimerState = new ShopTimerState(TimerState.RemainingTime, false);

            GamePhase next = RunState.Suspicion >= RunState.RewindSuspicionThreshold
                ? GamePhase.RewindPending
                : TimerState.QueueOpen
                    ? GamePhase.CustomerArrival
                    : GamePhase.ClosingEncounter;
            TryTransition(next);
        }

        internal void EnterRewind(ActiveAttemptMetadata attemptMetadata)
        {
            AttemptMetadata = attemptMetadata ?? throw new ArgumentNullException(nameof(attemptMetadata));
            RewindCompletionApplied = false;
            TimerStopped = true;
            TimerPaused = false;
            TimerState = new ShopTimerState(TimerState.RemainingTime, false);
            TryTransition(GamePhase.RewindMinigame);
        }

        internal void CompleteRewind(
            DayStartSnapshot dayStartSnapshot,
            ActiveAttemptMetadata attemptMetadata)
        {
            TimeSpan remaining = TimerState.RemainingTime; // time left when the minigame started

            ReplaceAttempt(dayStartSnapshot, attemptMetadata);

            TimerState = new ShopTimerState(remaining, false);
            RewindCompletionApplied = true;
            TryTransition(GamePhase.ShopPreparation);
        }

        internal void BeginExpenseCheck()
        {
            if (CurrentPhase == GamePhase.ClosingEncounter)
            {
                TryTransition(GamePhase.ExpenseCheck);
            }
        }

        internal void ApplyExpenseFailure(RunState runState)
        {
            RunState = runState ?? throw new ArgumentNullException(nameof(runState));
            TryTransition(GamePhase.GameOver);
        }

        internal void ApplyVictory(RunState runState)
        {
            RunState = runState ?? throw new ArgumentNullException(nameof(runState));
            TryTransition(GamePhase.Victory);
        }

        internal void AdvanceDay(
            DayStartSnapshot dayStartSnapshot,
            ActiveAttemptMetadata attemptMetadata)
        {
            ReplaceAttempt(dayStartSnapshot, attemptMetadata);
            TryTransition(GamePhase.DaySummary);
        }

        internal void ContinueFromDaySummary()
        {
            TryTransition(GamePhase.ShopPreparation);
        }

        internal void RetryDay(
            DayStartSnapshot dayStartSnapshot,
            ActiveAttemptMetadata attemptMetadata)
        {
            ReplaceAttempt(dayStartSnapshot, attemptMetadata);
            TryTransition(GamePhase.ShopPreparation);
        }

        private void ReplaceAttempt(
            DayStartSnapshot dayStartSnapshot,
            ActiveAttemptMetadata attemptMetadata)
        {
            DayStartSnapshot = dayStartSnapshot ?? throw new ArgumentNullException(nameof(dayStartSnapshot));
            AttemptMetadata = attemptMetadata ?? throw new ArgumentNullException(nameof(attemptMetadata));
            RunState = dayStartSnapshot.RunState.WithProgress(
                attemptMetadata.DayNumber,
                dayStartSnapshot.RunState.Money,
                attemptMetadata.Suspicion,
                attemptMetadata.RewindCount);
            AttemptState = new DayAttemptState(
                attemptMetadata.ActiveOrderSeed,
                dayStartSnapshot.TimerDuration,
                null,
                null,
                null,
                0,
                new PotionDraft(),
                null);
            TimerState = new ShopTimerState(dayStartSnapshot.TimerDuration, false);
            TimerStartedAtUtc = null;
            lastTimerSampleUtc = null;
            TimerPaused = false;
            TimerStopped = false;
            ServiceCommitted = false;
            RewindCompletionApplied = false;
            ClearWorkbench();
        }

    }
}
