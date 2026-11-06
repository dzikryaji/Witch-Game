using System;
using System.Collections.Generic;
using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Day", fileName = "Day_")]
    public sealed class DayDefinition : StableContentDefinition
    {
        [SerializeField, Min(1)] private int dayNumber = 1;
        [SerializeField, Min(1)] private int timerSeconds = 240;
        [SerializeField, Min(0)] private int dailyExpense = 20;
        [SerializeField] private string[] unlockedCategoryIds = Array.Empty<string>();
        [SerializeField] private OrderDifficulty[] orderDifficulties = { OrderDifficulty.Simple };
        [SerializeField] private string[] customerIds = Array.Empty<string>();

        public int DayNumber => dayNumber;
        public int TimerSeconds => timerSeconds;
        public int DailyExpense => dailyExpense;
        public IReadOnlyList<string> UnlockedCategoryIds => unlockedCategoryIds;
        public IReadOnlyList<OrderDifficulty> OrderDifficulties => orderDifficulties;
        public IReadOnlyList<string> CustomerIds => customerIds;

        public void Configure(
            string id,
            int day,
            int seconds,
            string[] customers,
            int expense = 20,
            string[] categoryUnlocks = null,
            OrderDifficulty[] difficulties = null)
        {
            SetStableId(id);
            dayNumber = day;
            timerSeconds = seconds;
            dailyExpense = expense;
            unlockedCategoryIds = categoryUnlocks == null ? Array.Empty<string>() : (string[])categoryUnlocks.Clone();
            orderDifficulties = difficulties == null
                ? new[] { OrderDifficulty.Simple }
                : (OrderDifficulty[])difficulties.Clone();
            customerIds = customers == null ? Array.Empty<string>() : (string[])customers.Clone();
        }
    }
}
