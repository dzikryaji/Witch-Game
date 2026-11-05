using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Witch.Infrastructure.Content
{
    public enum OrderDifficulty
    {
        Simple = 0,
        Mid = 1,
        Hard = 2
    }

    [CreateAssetMenu(menuName = "Witch/Content/Order", fileName = "Order_")]
    public sealed class OrderDefinition : StableContentDefinition
    {
        [SerializeField] private OrderDifficulty difficulty = OrderDifficulty.Simple;
        [FormerlySerializedAs("orderSentences")]
        [SerializeField, TextArea] private string[] orderBubbles = Array.Empty<string>();
        [SerializeField] private string firstHerbId;
        [SerializeField] private string secondHerbId;

        public OrderDifficulty Difficulty => difficulty;
        public IReadOnlyList<string> OrderBubbles => orderBubbles;
        public string FirstHerbId => firstHerbId;
        public string SecondHerbId => secondHerbId;

        public void Configure(
            string id,
            string[] bubbles,
            string firstHerb,
            string secondHerb,
            OrderDifficulty difficulty = OrderDifficulty.Simple)
        {
            SetStableId(id);
            orderBubbles = bubbles == null ? Array.Empty<string>() : (string[])bubbles.Clone();
            firstHerbId = firstHerb;
            secondHerbId = secondHerb;
            this.difficulty = difficulty;
        }
    }
}
