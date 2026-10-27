using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Recipe", fileName = "Recipe_")]
    public sealed class RecipeDefinition : StableContentDefinition
    {
        [SerializeField] private string firstHerbId;
        [SerializeField] private string secondHerbId;

        public string FirstHerbId => firstHerbId;
        public string SecondHerbId => secondHerbId;

        public void Configure(string id, string firstHerb, string secondHerb)
        {
            SetStableId(id);
            firstHerbId = firstHerb;
            secondHerbId = secondHerb;
        }
    }
}
