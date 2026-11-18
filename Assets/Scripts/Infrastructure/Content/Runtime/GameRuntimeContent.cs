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
            IDictionary<string, HerbContent> herbsById,
            IDictionary<string, OrderContent> ordersById,
            IDictionary<string, CustomerContent> customersById,
            IDictionary<int, DayContent> daysByNumber,
            IDictionary<string, HerbCategoryContent> herbCategoriesById,
            IReadOnlyList<MatchCardContent> matchCards,
            MatchCardDifficultyContent matchCardDifficulty,
            NewRunSetup newRunSetup,
            DayProgressionSetup dayProgressionSetup,
            ExpenseRules expenseRules,
            int correctOrderPayment,
            WitchHunterDialogueContent witchHunterDialogue)
        {
            HerbsById = new ReadOnlyDictionary<string, HerbContent>(
                new Dictionary<string, HerbContent>(herbsById));
            OrdersById = new ReadOnlyDictionary<string, OrderContent>(
                new Dictionary<string, OrderContent>(ordersById));
            CustomersById = new ReadOnlyDictionary<string, CustomerContent>(
                new Dictionary<string, CustomerContent>(customersById));
            DaysByNumber = new ReadOnlyDictionary<int, DayContent>(
                new Dictionary<int, DayContent>(daysByNumber));
            HerbCategoriesById = new ReadOnlyDictionary<string, HerbCategoryContent>(
                new Dictionary<string, HerbCategoryContent>(herbCategoriesById));
            MatchCards = new List<MatchCardContent>(matchCards).AsReadOnly();
            MatchCardDifficulty = matchCardDifficulty;
            NewRunSetup = newRunSetup;
            DayProgressionSetup = dayProgressionSetup;
            ExpenseRules = expenseRules;
            CorrectOrderPayment = correctOrderPayment;
            WitchHunterDialogue = witchHunterDialogue;
        }

        public IReadOnlyDictionary<string, HerbContent> HerbsById { get; }
        public IReadOnlyDictionary<string, OrderContent> OrdersById { get; }
        public IReadOnlyDictionary<string, CustomerContent> CustomersById { get; }
        public IReadOnlyDictionary<int, DayContent> DaysByNumber { get; }
        public IReadOnlyDictionary<string, HerbCategoryContent> HerbCategoriesById { get; }
        public IReadOnlyList<MatchCardContent> MatchCards { get; }
        public IReadOnlyList<string> MatchCardIds
        {
            get
            {
                var ids = new List<string>(MatchCards.Count);
                foreach (MatchCardContent card in MatchCards)
                {
                    ids.Add(card.StableId);
                }

                return ids.AsReadOnly();
            }
        }

        public MatchCardDifficultyContent MatchCardDifficulty { get; }
        public NewRunSetup NewRunSetup { get; }
        public DayProgressionSetup DayProgressionSetup { get; }
        public ExpenseRules ExpenseRules { get; }
        public int CorrectOrderPayment { get; }
        public WitchHunterDialogueContent WitchHunterDialogue { get; }
    }
}
