using System;
using System.Collections.Generic;
using UnityEngine;

namespace Witch.Infrastructure.Content
{
    /// <summary>One row of the guidebook Contents page, and the label shown on herb pages of that category.</summary>
    [Serializable]
    public sealed class GuidebookContentsItemDefinition
    {
        [SerializeField] private string categoryId;
        [SerializeField] private string name;
        [SerializeField, TextArea] private string description;

        public string CategoryId => categoryId;
        public string Name => name;
        public string Description => description;

        public static GuidebookContentsItemDefinition Create(string category, string itemName, string itemDescription)
        {
            return new GuidebookContentsItemDefinition
            {
                categoryId = category,
                name = itemName,
                description = itemDescription
            };
        }
    }

    [CreateAssetMenu(menuName = "Witch/Content/Guidebook Entry", fileName = "Guidebook_")]
    public sealed class GuidebookEntryDefinition : StableContentDefinition
    {
        [SerializeField] private string title;
        [SerializeField] private string categoryId;
        [SerializeField, TextArea] private string bodyText;
        [SerializeField] private string[] clueIds = Array.Empty<string>();
        [SerializeField] private GuidebookContentsItemDefinition[] contentsItems = Array.Empty<GuidebookContentsItemDefinition>();

        public string Title => title;
        public string CategoryId => categoryId;
        public string BodyText => bodyText;
        public IReadOnlyList<string> ClueIds => clueIds;
        public IReadOnlyList<GuidebookContentsItemDefinition> ContentsItems => contentsItems;

        public void Configure(string id, string entryTitle, string category, string[] clues, string body = "")
        {
            SetStableId(id);
            title = entryTitle;
            categoryId = category;
            bodyText = body;
            clueIds = clues == null ? Array.Empty<string>() : (string[])clues.Clone();
        }

        public void ConfigureContents(params GuidebookContentsItemDefinition[] items)
        {
            contentsItems = items == null
                ? Array.Empty<GuidebookContentsItemDefinition>()
                : (GuidebookContentsItemDefinition[])items.Clone();
        }
    }
}
