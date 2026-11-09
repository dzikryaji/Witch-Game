using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Customer", fileName = "Customer_")]
    public sealed class CustomerDefinition : StableContentDefinition
    {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite illustration;

        public string DisplayName => displayName;
        public Sprite Illustration => illustration;

        public void Configure(string id, string name, Sprite customerIllustration = null)
        {
            SetStableId(id);
            displayName = name;
            illustration = customerIllustration;
        }
    }
}
