using UnityEngine;

namespace Witch.Infrastructure.Content
{
    public abstract class StableContentDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;

        public string StableId => stableId;

        protected void SetStableId(string value)
        {
            stableId = value;
        }
    }
}
