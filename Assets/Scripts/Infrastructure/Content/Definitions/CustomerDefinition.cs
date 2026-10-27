using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Customer", fileName = "Customer_")]
    public sealed class CustomerDefinition : StableContentDefinition
    {
        [SerializeField] private string displayName;

        public string DisplayName => displayName;

        public void Configure(string id, string name)
        {
            SetStableId(id);
            displayName = name;
        }
    }
}
