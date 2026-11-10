using System.Collections.Generic;

namespace Witch.Application.Ports
{
    public static class GameAudioCueIds
    {
        public const string MainMenuMusic = "BGM_MAIN_MENU";
        public const string ShopMusic = "BGM_SHOP";
        public const string RewindMusic = "BGM_REWIND_MINIGAME";
        public const string GameOverMusic = "BGM_GAME_OVER";
        public const string VictoryMusic = "BGM_GAME_COMPLETE";
        public const string ShopAmbience = "SFX_SHOP_AMBIENCE";
        public const string CustomerBell = "SFX_CUSTOMER_BELL";
        public const string PageFlip = "SFX_PAGE_FLIP";
        public const string Grind = "SFX_GRINDER";
        public const string HerbPluck = "SFX_HERB_PLUCK";
        public const string BrewSuccess = "SFX_BREW_SUCCESS";
        public const string Discard = "SFX_DISCARD";
        public const string OrderCorrect = "SFX_ORDER_CORRECT";
        public const string Payment = "SFX_PAYMENT_COINS";
        public const string OrderWrong = "SFX_ORDER_WRONG";
        public const string CardMatch = "SFX_CARD_MATCH";
        public const string CardMismatch = "SFX_CARD_MISMATCH";
        public const string MinigameComplete = "SFX_MINIGAME_COMPLETE";
        public const string ExpensePay = "SFX_EXPENSE_PAY";
        public const string ExpenseFail = "SFX_EXPENSE_FAIL";
        public const string UiClick = "SFX_UI_CLICK";

        public static IReadOnlyList<string> Required { get; } = new[]
        {
            MainMenuMusic, ShopMusic, RewindMusic, GameOverMusic, VictoryMusic,
            ShopAmbience, CustomerBell, PageFlip, Grind, HerbPluck,
            BrewSuccess, Discard, OrderCorrect, Payment, OrderWrong,
            CardMatch, CardMismatch,
            MinigameComplete, ExpensePay, ExpenseFail, UiClick
        };
    }
}
