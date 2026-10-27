using System;
using System.Collections.Generic;
using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Order", fileName = "Order_")]
    public sealed class OrderDefinition : StableContentDefinition
    {
        [SerializeField, TextArea] private string clueText;
        [SerializeField] private string[] clueIds = Array.Empty<string>();
        [SerializeField] private string firstHerbId;
        [SerializeField] private string secondHerbId;

        public string ClueText => clueText;
        public IReadOnlyList<string> ClueIds => clueIds;
        public string FirstHerbId => firstHerbId;
        public string SecondHerbId => secondHerbId;

        public void Configure(string id, string text, string[] clues, string firstHerb, string secondHerb)
        {
            SetStableId(id);
            clueText = text;
            clueIds = clues == null ? Array.Empty<string>() : (string[])clues.Clone();
            firstHerbId = firstHerb;
            secondHerbId = secondHerb;
        }
    }
}
