using UnityEngine;
using UnityEngine.Serialization;
using System;
using System.Collections.Generic;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Herb", fileName = "Herb_")]
    public sealed class HerbDefinition : StableContentDefinition
    {
        [SerializeField] private string displayName;
        [SerializeField] private string categoryId;
        [SerializeField] private string scientificName;
        [SerializeField, TextArea] private string treatmentText;
        [SerializeField, TextArea] private string warningText;
        [FormerlySerializedAs("guidebookIllustration")]
        [SerializeField] private Sprite herbIllustration;
        [SerializeField] private string accessibleLabel;
        [SerializeField, Min(1)] private int guidebookPage;
        [SerializeField] private string[] clueIds = Array.Empty<string>();

        public string DisplayName => displayName;
        public string CategoryId => categoryId;
        public string ScientificName => scientificName;
        public string TreatmentText => treatmentText;
        public string WarningText => warningText;
        public Sprite HerbIllustration => herbIllustration;
        public string AccessibleLabel => accessibleLabel;
        public int GuidebookPage => guidebookPage;
        public IReadOnlyList<string> ClueIds => clueIds;

        public void Configure(string id, string name, string category)
        {
            SetStableId(id);
            displayName = name;
            categoryId = category;
        }

        public void ConfigureGuidebook(
            string scientific,
            string treatment,
            string warning,
            string accessibilityLabel,
            int page,
            string[] clues = null,
            Sprite illustration = null)
        {
            scientificName = scientific;
            treatmentText = treatment;
            warningText = warning;
            accessibleLabel = accessibilityLabel;
            guidebookPage = page;
            clueIds = clues == null ? Array.Empty<string>() : (string[])clues.Clone();
            herbIllustration = illustration;
        }
    }
}
