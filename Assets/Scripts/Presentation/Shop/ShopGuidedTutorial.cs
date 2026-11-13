using System;

namespace Witch.Presentation.Shop
{
    public static class ShopTutorialFlags
    {
        public const string Day1GuidedCompleted = "shop.tutorial.day1.guided";
    }

    public enum ShopGuidedTutorialStep
    {
        ReadOrder = 0,
        OpenGuidebook = 1,
        NavigateToMustardSeed = 2,
        CloseGuidebook = 3,
        LoadHerbs = 4,
        Grind = 5,
        AddToCauldron = 6,
        Serve = 7
    }
}
