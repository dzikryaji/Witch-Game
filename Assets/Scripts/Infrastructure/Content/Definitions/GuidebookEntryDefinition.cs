using System;
using System.Collections.Generic;
using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Guidebook Entry", fileName = "Guidebook_")]
    public sealed class GuidebookEntryDefinition : StableContentDefinition
    {
        [SerializeField] private string title;
        [SerializeField] private string categoryId;
        [SerializeField, TextArea] private string bodyText;
        [SerializeField] private string[] herbIds = Array.Empty<string>();
        [SerializeField] private string[] clueIds = Array.Empty<string>();

        public string Title => title;
        public string CategoryId => categoryId;
        public string BodyText => bodyText;
        public IReadOnlyList<string> HerbIds => herbIds;
        public IReadOnlyList<string> ClueIds => clueIds;

        public void Configure(string id, string entryTitle, string category, string[] herbs, string[] clues, string body = "")
        {
            SetStableId(id);
            title = entryTitle;
            categoryId = category;
            bodyText = body;
            herbIds = herbs == null ? Array.Empty<string>() : (string[])herbs.Clone();
            clueIds = clues == null ? Array.Empty<string>() : (string[])clues.Clone();
        }
    }
}
