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

        public int StartingMoney => startingMoney;
        public int SaveVersion => saveVersion;
        public int FirstDay => firstDay;
        public int DailyTimerIncreaseSeconds => dailyTimerIncreaseSeconds;

        public void Configure(
            string id,
            int money,
            int version,
            int startingDay,
            int timerIncreaseSeconds)
        {
            SetStableId(id);
            startingMoney = money;
            saveVersion = version;
            firstDay = startingDay;
            dailyTimerIncreaseSeconds = timerIncreaseSeconds;
        }
    }
}
