using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Herb", fileName = "Herb_")]
    public sealed class HerbDefinition : StableContentDefinition
    {
        [SerializeField] private string displayName;
        [SerializeField] private string categoryId;

        public string DisplayName => displayName;
        public string CategoryId => categoryId;

        public void Configure(string id, string name, string category)
        {
            SetStableId(id);
            displayName = name;
            categoryId = category;
        }
    }
}
