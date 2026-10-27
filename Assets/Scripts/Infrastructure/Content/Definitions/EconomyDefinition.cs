using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Economy", fileName = "Economy_")]
    public sealed class EconomyDefinition : StableContentDefinition
    {
        [SerializeField, Min(0)] private int correctOrderPayment = 10;
        [SerializeField, Min(0)] private int dailyExpense = 20;
        [SerializeField, Min(1)] private int completionDay = 3;
        [SerializeField] private string expenseIdPrefix = "expense.day.";

        public int CorrectOrderPayment => correctOrderPayment;
        public int DailyExpense => dailyExpense;
        public int CompletionDay => completionDay;
        public string ExpenseIdPrefix => expenseIdPrefix;

        public void Configure(
            string id,
            int payment,
            int expense,
            int finalDay,
            string expensePrefix)
        {
            SetStableId(id);
            correctOrderPayment = payment;
            dailyExpense = expense;
            completionDay = finalDay;
            expenseIdPrefix = expensePrefix;
        }
    }
}
