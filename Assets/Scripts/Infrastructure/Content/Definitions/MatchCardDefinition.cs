using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Match Card", fileName = "MatchCard_")]
    public sealed class MatchCardDefinition : StableContentDefinition
    {
        [SerializeField] private string symbolKey;

        public string SymbolKey => symbolKey;

        public void Configure(string id, string symbol)
        {
            SetStableId(id);
            symbolKey = symbol;
        }
    }
}
