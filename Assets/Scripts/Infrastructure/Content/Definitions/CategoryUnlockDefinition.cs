using System;
using System.Collections.Generic;
using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Category Unlock", fileName = "Unlock_")]
    public sealed class CategoryUnlockDefinition : StableContentDefinition
    {
        [SerializeField] private string categoryId;
        [SerializeField, Min(1)] private int firstDay = 1;
        [SerializeField] private string[] herbIds = Array.Empty<string>();
        [SerializeField] private string[] recipeIds = Array.Empty<string>();

        public string CategoryId => categoryId;
        public int FirstDay => firstDay;
        public IReadOnlyList<string> HerbIds => herbIds;
        public IReadOnlyList<string> RecipeIds => recipeIds;

        public void Configure(
            string id,
            string category,
            int day,
            string[] herbs,
            string[] recipes)
        {
            SetStableId(id);
            categoryId = category;
            firstDay = day;
            herbIds = herbs == null ? Array.Empty<string>() : (string[])herbs.Clone();
            recipeIds = recipes == null ? Array.Empty<string>() : (string[])recipes.Clone();
        }
    }
}
