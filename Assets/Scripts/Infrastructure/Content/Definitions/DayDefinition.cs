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
        [SerializeField] private string[] orderIds = Array.Empty<string>();
        [SerializeField] private string[] customerIds = Array.Empty<string>();

        public int DayNumber => dayNumber;
        public int TimerSeconds => timerSeconds;
        public IReadOnlyList<string> OrderIds => orderIds;
        public IReadOnlyList<string> CustomerIds => customerIds;

        public void Configure(
            string id,
            int day,
            int seconds,
            string[] orders,
            string[] customers)
        {
            SetStableId(id);
            dayNumber = day;
            timerSeconds = seconds;
            orderIds = orders == null ? Array.Empty<string>() : (string[])orders.Clone();
            customerIds = customers == null ? Array.Empty<string>() : (string[])customers.Clone();
        }
    }
}
