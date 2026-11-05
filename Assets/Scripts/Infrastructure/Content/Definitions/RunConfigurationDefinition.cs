using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Run Configuration", fileName = "Run_")]
    public sealed class RunConfigurationDefinition : StableContentDefinition
    {
        [SerializeField, Min(0)] private int startingMoney;
        [SerializeField, Min(1)] private int saveVersion = 1;
        [SerializeField, Min(1)] private int firstDay = 1;
        [SerializeField, Min(0)] private int dailyTimerIncreaseSeconds = 20;
        [SerializeField, Min(0)] private int correctOrderPayment = 10;
        [SerializeField, Min(1)] private int completionDay = 3;
        [SerializeField] private string expenseIdPrefix = "expense.day.";

        public int StartingMoney => startingMoney;
        public int SaveVersion => saveVersion;
        public int FirstDay => firstDay;
        public int DailyTimerIncreaseSeconds => dailyTimerIncreaseSeconds;
        public int CorrectOrderPayment => correctOrderPayment;
        public int CompletionDay => completionDay;
        public string ExpenseIdPrefix => expenseIdPrefix;

        public void Configure(
            string id,
            int money,
            int version,
            int startingDay,
            int timerIncreaseSeconds,
            int payment = 10,
            int finalDay = 3,
            string expensePrefix = "expense.day.")
        {
            SetStableId(id);
            startingMoney = money;
            saveVersion = version;
            firstDay = startingDay;
            dailyTimerIncreaseSeconds = timerIncreaseSeconds;
            correctOrderPayment = payment;
            completionDay = finalDay;
            expenseIdPrefix = expensePrefix;
        }
    }
}
