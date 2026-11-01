using System;
using System.Collections.Generic;
using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Order", fileName = "Order_")]
    public sealed class OrderDefinition : StableContentDefinition
    {
        [SerializeField, TextArea] private string[] orderSentences = Array.Empty<string>();
        [SerializeField] private string[] clueIds = Array.Empty<string>();
        [SerializeField] private string firstHerbId;
        [SerializeField] private string secondHerbId;

        public IReadOnlyList<string> OrderSentences => orderSentences;
        public IReadOnlyList<string> ClueIds => clueIds;
        public string FirstHerbId => firstHerbId;
        public string SecondHerbId => secondHerbId;

        public void Configure(string id, string[] sentences, string[] clues, string firstHerb, string secondHerb)
        {
            SetStableId(id);
            orderSentences = sentences == null ? Array.Empty<string>() : (string[])sentences.Clone();
            clueIds = clues == null ? Array.Empty<string>() : (string[])clues.Clone();
            firstHerbId = firstHerb;
            secondHerbId = secondHerb;
        }
    }
}
