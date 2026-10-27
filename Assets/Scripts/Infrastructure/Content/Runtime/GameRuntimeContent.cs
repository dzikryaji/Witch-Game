using System.Collections.Generic;
using System.Collections.ObjectModel;
using Witch.Application.Flow;
using Witch.Domain.Orders;
using Witch.Domain.Potions;
using Witch.Domain.Progression;

namespace Witch.Infrastructure.Content
{
    public sealed class GameRuntimeContent
    {
        public GameRuntimeContent(
            RecipeCatalog recipes,
            IDictionary<string, HerbContent> herbsById,
            IDictionary<string, OrderContent> ordersById,
            IDictionary<string, CustomerContent> customersById,
            IDictionary<int, DayContent> daysByNumber,
            IDictionary<string, GuidebookEntryContent> guidebookEntriesById,
            IReadOnlyList<CategoryUnlockContent> categoryUnlocks,
            IReadOnlyList<RewindCardContent> rewindCards,
            RewindDifficultyContent rewindDifficulty,
            NewRunSetup newRunSetup,
            DayProgressionSetup dayProgressionSetup,
            ExpenseRules expenseRules,
            int correctOrderPayment)
        {
            Recipes = recipes;
            HerbsById = new ReadOnlyDictionary<string, HerbContent>(
                new Dictionary<string, HerbContent>(herbsById));
            OrdersById = new ReadOnlyDictionary<string, OrderContent>(
                new Dictionary<string, OrderContent>(ordersById));
            CustomersById = new ReadOnlyDictionary<string, CustomerContent>(
                new Dictionary<string, CustomerContent>(customersById));
            DaysByNumber = new ReadOnlyDictionary<int, DayContent>(
                new Dictionary<int, DayContent>(daysByNumber));
            GuidebookEntriesById = new ReadOnlyDictionary<string, GuidebookEntryContent>(
                new Dictionary<string, GuidebookEntryContent>(guidebookEntriesById));
            CategoryUnlocks = new List<CategoryUnlockContent>(categoryUnlocks).AsReadOnly();
            RewindCards = new List<RewindCardContent>(rewindCards).AsReadOnly();
            RewindDifficulty = rewindDifficulty;
            NewRunSetup = newRunSetup;
            DayProgressionSetup = dayProgressionSetup;
            ExpenseRules = expenseRules;
            CorrectOrderPayment = correctOrderPayment;
        }

        public RecipeCatalog Recipes { get; }
        public IReadOnlyDictionary<string, HerbContent> HerbsById { get; }
        public IReadOnlyDictionary<string, OrderContent> OrdersById { get; }
        public IReadOnlyDictionary<string, CustomerContent> CustomersById { get; }
        public IReadOnlyDictionary<int, DayContent> DaysByNumber { get; }
        public IReadOnlyDictionary<string, GuidebookEntryContent> GuidebookEntriesById { get; }
        public IReadOnlyList<CategoryUnlockContent> CategoryUnlocks { get; }
        public IReadOnlyList<RewindCardContent> RewindCards { get; }
        public IReadOnlyList<string> RewindCardIds
        {
            get
            {
                var ids = new List<string>(RewindCards.Count);
                foreach (RewindCardContent card in RewindCards)
                {
                    ids.Add(card.StableId);
                }

                return ids.AsReadOnly();
            }
        }

        public RewindDifficultyContent RewindDifficulty { get; }
        public NewRunSetup NewRunSetup { get; }
        public DayProgressionSetup DayProgressionSetup { get; }
        public ExpenseRules ExpenseRules { get; }
        public int CorrectOrderPayment { get; }
    }
}
