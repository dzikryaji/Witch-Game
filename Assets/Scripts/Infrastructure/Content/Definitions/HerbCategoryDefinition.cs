using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Herb Category", fileName = "Category_")]
    public sealed class HerbCategoryDefinition : StableContentDefinition
    {
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;

        public string DisplayName => displayName;
        public string Description => description;

        public void Configure(string id, string name, string categoryDescription)
        {
            SetStableId(id);
            displayName = name;
            description = categoryDescription;
        }
    }
}
