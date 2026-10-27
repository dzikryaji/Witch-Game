using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Rewind Card", fileName = "RewindCard_")]
    public sealed class RewindCardDefinition : StableContentDefinition
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
