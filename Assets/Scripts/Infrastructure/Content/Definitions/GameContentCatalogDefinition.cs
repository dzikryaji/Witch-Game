using System;
using System.Collections.Generic;
using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Game Catalog", fileName = "GameContentCatalog")]
    public sealed class GameContentCatalogDefinition : StableContentDefinition
    {
        [SerializeField] private HerbDefinition[] herbs = Array.Empty<HerbDefinition>();
        [SerializeField] private OrderDefinition[] orders = Array.Empty<OrderDefinition>();
        [SerializeField] private CustomerDefinition[] customers = Array.Empty<CustomerDefinition>();
        [SerializeField] private DayDefinition[] days = Array.Empty<DayDefinition>();
        [SerializeField] private GuidebookEntryDefinition[] guidebookEntries = Array.Empty<GuidebookEntryDefinition>();
        [SerializeField] private RewindCardDefinition[] rewindCards = Array.Empty<RewindCardDefinition>();
        [SerializeField] private RewindDifficultyDefinition rewindDifficulty;
        [SerializeField] private RunConfigurationDefinition runConfiguration;

        public IReadOnlyList<HerbDefinition> Herbs => herbs;
        public IReadOnlyList<OrderDefinition> Orders => orders;
        public IReadOnlyList<CustomerDefinition> Customers => customers;
        public IReadOnlyList<DayDefinition> Days => days;
        public IReadOnlyList<GuidebookEntryDefinition> GuidebookEntries => guidebookEntries;
        public IReadOnlyList<RewindCardDefinition> RewindCards => rewindCards;
        public RewindDifficultyDefinition RewindDifficulty => rewindDifficulty;
        public RunConfigurationDefinition RunConfiguration => runConfiguration;

        public void Configure(
            IReadOnlyList<HerbDefinition> herbValues,
            IReadOnlyList<OrderDefinition> orderValues,
            IReadOnlyList<CustomerDefinition> customerValues,
            IReadOnlyList<DayDefinition> dayValues,
            IReadOnlyList<GuidebookEntryDefinition> guidebookValues,
            IReadOnlyList<RewindCardDefinition> cardValues,
            RewindDifficultyDefinition difficulty,
            RunConfigurationDefinition run)
        {
            SetStableId("catalog.game");
            herbs = Copy(herbValues);
            orders = Copy(orderValues);
            customers = Copy(customerValues);
            days = Copy(dayValues);
            guidebookEntries = Copy(guidebookValues);
            rewindCards = Copy(cardValues);
            rewindDifficulty = difficulty;
            runConfiguration = run;
        }

        [ContextMenu("Validate Game Content")]
        private void ValidateContent()
        {
            ContentValidationReport report = new GameContentValidator().Validate(this);
            if (report.IsValid)
            {
                Debug.Log("Game content validation passed.", this);
                return;
            }

            foreach (string error in report.Errors)
            {
                Debug.LogError("Game content: " + error, this);
            }
        }

        private static T[] Copy<T>(IReadOnlyList<T> values)
        {
            if (values == null)
            {
                return Array.Empty<T>();
            }

            var copy = new T[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                copy[index] = values[index];
            }

            return copy;
        }
    }
}
