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

        public HerbPair GroundMixture { get; private set; }

        public PotionDraft PotionDraft { get; private set; }

        public PreparedPotion PreparedPotion { get; private set; }

        public bool ServiceCommitted { get; private set; }

        internal bool RewindCompletionApplied { get; private set; }

        public DaySummaryData LastDaySummary { get; private set; }

        private RunState resumeRunState;   // run state as it was before the last customer was served
        private int resumeCorrectServiceCountToday;
        private int resumeWrongServiceCountToday;
        private int resumeServiceCountToday;
        private int resumeCoinsEarnedToday;
        private int resumeWrongOrdersForSummary;

        public bool CanResumeInPlace =>
            resumeRunState != null
            && AttemptState.ActiveOrder != null
            && TimerState.RemainingTime > TimeSpan.Zero;

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

            RunState = RunState.MarkOrderSeen(order.OrderId);
            resumeRunState = RunState;
            resumeCorrectServiceCountToday = AttemptMetadata.CorrectServiceCountToday;
            resumeWrongServiceCountToday = AttemptMetadata.WrongServiceCountToday;
            resumeServiceCountToday = AttemptMetadata.ServiceCountToday;
            resumeCoinsEarnedToday = AttemptMetadata.CoinsEarnedToday;
            resumeWrongOrdersForSummary = AttemptMetadata.WrongOrdersForSummary;

            var usedOrderIds = new System.Collections.Generic.List<string>(AttemptState.UsedOrderIds);
            usedOrderIds.Add(order.OrderId);

            AttemptState = new DayAttemptState(
                AttemptState.AttemptSeed,
                TimerState.RemainingTime,
                usedOrderIds,
                customerId,
                order,
                AttemptState.EarnedMoney,
                AttemptState.PotionDraft,
                AttemptState.PreparedPotion);
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

        internal void SetGroundMixture(HerbPair mixture)
        {
            GroundMixture = mixture ?? throw new ArgumentNullException(nameof(mixture));
        }


        internal void ClearGroundMixture()
        {
            GroundMixture = null;
        }

        internal void AddGroundHerbToPot()
        {
            if (GroundMixture == null)
            {
                throw new InvalidOperationException("Grind a herb before adding it to the pot.");
            }

            if (PotionDraft.Ingredients.Count > 0)
            {
                throw new InvalidOperationException("The cauldron already has ingredients.");
            }

            PotionDraft = new PotionDraft(new[]
            {
                new PotIngredient(GroundMixture.FirstHerbId),
                new PotIngredient(GroundMixture.SecondHerbId)
            });

            GroundMixture = null;
            PreparedPotion = null;
        }

        internal void SetPreparedPotion(PreparedPotion potion)
        {
            PreparedPotion = potion ?? throw new ArgumentNullException(nameof(potion));
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
            PreparedPotion = null;
        }

        internal void ClearWorkbench()
        {
            GroundMixture = null;
            PotionDraft = new PotionDraft();
            PreparedPotion = null;
        }

        internal void ClearPotion()
        {
            PotionDraft = new PotionDraft();   
            PreparedPotion = null;             
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
            bool wrongValid = outcome.Kind == ServiceOutcomeKind.WrongValid;
            AttemptMetadata = new ActiveAttemptMetadata(
                RunState.RunId,
                RunState.CurrentDayNumber,
                RunState.Suspicion,
                RunState.RewindCount,
                AttemptMetadata.PendingRewind,
                AttemptMetadata.MinigameBoardSeed,
                AttemptMetadata.ActiveOrderSeed,
                wrongValid
                    ? AttemptMetadata.WrongServiceCountToday + 1
                    : AttemptMetadata.WrongServiceCountToday,
                AttemptMetadata.ServiceCountToday + 1,
                AttemptMetadata.MinigamePlayedToday,
                outcome.Kind == ServiceOutcomeKind.Correct
                    ? AttemptMetadata.CorrectServiceCountToday + 1
                    : AttemptMetadata.CorrectServiceCountToday,
                AttemptMetadata.CoinsEarnedToday + outcome.Payment,
                wrongValid
                    ? AttemptMetadata.WrongOrdersForSummary + 1
                    : AttemptMetadata.WrongOrdersForSummary);
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

        internal void MarkCategoryServed(string categoryId)
        {
            RunState = RunState.MarkCategoryServed(categoryId);
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
            ReplaceAttempt(dayStartSnapshot, attemptMetadata);
            RewindCompletionApplied = true;
            TryTransition(GamePhase.ShopPreparation);
        }

        internal void CompleteRewindInPlace(
            DayStartSnapshot dayStartSnapshot,
            ActiveAttemptMetadata attemptMetadata)
        {
            RunState failedAttemptRunState = RunState;
            DayStartSnapshot = dayStartSnapshot ?? throw new ArgumentNullException(nameof(dayStartSnapshot));
            AttemptMetadata = attemptMetadata ?? throw new ArgumentNullException(nameof(attemptMetadata));
            // Money and per-day new counters roll back to the pre-service snapshot.
            // WrongServiceCountToday/ServiceCountToday carry forward as provided (Clean Ledger depends on them).
            AttemptMetadata = new ActiveAttemptMetadata(
                AttemptMetadata.RunId,
                AttemptMetadata.DayNumber,
                AttemptMetadata.Suspicion,
                AttemptMetadata.RewindCount,
                AttemptMetadata.PendingRewind,
                AttemptMetadata.MinigameBoardSeed,
                AttemptMetadata.ActiveOrderSeed,
                AttemptMetadata.WrongServiceCountToday,
                AttemptMetadata.ServiceCountToday,
                AttemptMetadata.MinigamePlayedToday,
                resumeCorrectServiceCountToday,
                resumeCoinsEarnedToday,
                resumeWrongOrdersForSummary);
            RunState baseResumeRunState = resumeRunState ?? failedAttemptRunState;
            RunState = baseResumeRunState.WithProgress(
                attemptMetadata.DayNumber,
                baseResumeRunState.Money,
                AttemptMetadata.Suspicion,       // reset by the use case
                AttemptMetadata.RewindCount)     // incremented once by the use case
                .WithTutorialFlagsFrom(failedAttemptRunState);

            ClearWorkbench();
            ServiceCommitted = false;            // the same customer can be served again
            RewindCompletionApplied = true;

            TimerStopped = false;
            TimerPaused = true;                  // frozen until the Shop scene un-pauses it
            TimerState = new ShopTimerState(TimerState.RemainingTime, true);
            TryTransition(GamePhase.CustomerEncounter);
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

        internal void ApplyVictory(
            DayStartSnapshot dayStartSnapshot,
            ActiveAttemptMetadata attemptMetadata)
        {
            DayStartSnapshot = dayStartSnapshot ?? throw new ArgumentNullException(nameof(dayStartSnapshot));
            AttemptMetadata = attemptMetadata ?? throw new ArgumentNullException(nameof(attemptMetadata));
            RunState = dayStartSnapshot.RunState;
            TryTransition(GamePhase.Victory);
        }

        internal void SetLastDaySummary(DaySummaryData summary)
        {
            LastDaySummary = summary;
        }

        internal void AdvanceDay(
            DayStartSnapshot dayStartSnapshot,
            ActiveAttemptMetadata attemptMetadata,
            DaySummaryData summary)
        {
            ReplaceAttempt(dayStartSnapshot, attemptMetadata);
            LastDaySummary = summary;
            TryTransition(GamePhase.DaySummary);
        }

        internal void ContinueFromDaySummary()
        {
            LastDaySummary = null;
            TryTransition(GamePhase.ShopPreparation);
        }

        public void ApplyTutorialFlag(string flagId, bool value)
        {
            RunState = RunState.WithTutorialFlag(flagId, value);
            if (resumeRunState != null)
            {
                resumeRunState = resumeRunState.WithTutorialFlag(flagId, value);
            }

            DayStartSnapshot = new DayStartSnapshot(
                DayStartSnapshot.RunState.WithTutorialFlag(flagId, value),
                DayStartSnapshot.OrderSeed,
                DayStartSnapshot.TimerDuration,
                DayStartSnapshot.ExpenseId,
                DayStartSnapshot.ExpenseValue,
                DayStartSnapshot.SaveVersion,
                DayStartSnapshot.CapturedAtUtc);
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
            resumeRunState = null;
            resumeCorrectServiceCountToday = 0;
            resumeWrongServiceCountToday = 0;
            resumeServiceCountToday = 0;
            resumeCoinsEarnedToday = 0;
            resumeWrongOrdersForSummary = 0;
            ClearWorkbench();
        }

    }
}
