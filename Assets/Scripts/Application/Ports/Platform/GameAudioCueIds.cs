using System.Collections.Generic;

namespace Witch.Application.Ports
{
    public static class GameAudioCueIds
    {
        public const string MainMenuMusic = "BGM_MAIN_MENU";
        public const string ShopMusic = "BGM_SHOP";
        public const string MinigameMusic = "BGM_MINIGAME";
        public const string WitchHunterMusic = "BGM_WITCH_HUNTER";
        public const string GameOverMusic = "BGM_GAME_OVER";
        public const string VictoryMusic = "BGM_GAME_COMPLETE";
        public const string ShopAmbience = "SFX_SHOP_AMBIENCE";
        public const string StakeAmbience = "SFX_STAKE_AMBIENCE";
        public const string CustomerBell = "SFX_CUSTOMER_BELL";
        public const string PageFlip = "SFX_PAGE_FLIP";
        public const string Grind = "SFX_GRINDER";
        public const string HerbPluck = "SFX_HERB_PLUCK";
        public const string BrewSuccess = "SFX_BREW_SUCCESS";
        public const string Congratulations = "SFX_CONGRATULATIONS";
        public const string Boiling = "SFX_BOILING";
        public const string Discard = "SFX_DISCARD";
        public const string Payment = "SFX_PAYMENT_COINS";
        public const string Wrong = "SFX_WRONG";
        public const string CardMatch = "SFX_CARD_MATCH";
        public const string CardMismatch = "SFX_CARD_MISMATCH";
        public const string MinigameComplete = "SFX_MINIGAME_COMPLETE";
        public const string Correct = "SFX_CORRECT";
        public const string UiClick = "SFX_UI_CLICK";

        public static IReadOnlyList<string> Required { get; } = new[]
        {
            MainMenuMusic, ShopMusic, MinigameMusic, WitchHunterMusic, GameOverMusic, VictoryMusic,
            ShopAmbience, CustomerBell, PageFlip, Grind, HerbPluck,
            BrewSuccess, Congratulations, Boiling, Discard, Payment, Wrong,
            CardMatch, CardMismatch,
            MinigameComplete, Correct, UiClick
        };
    }
}
