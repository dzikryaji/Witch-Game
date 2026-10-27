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
        public const string GuidebookOpen = "SFX_GUIDEBOOK_OPEN";
        public const string GuidebookClose = "SFX_GUIDEBOOK_CLOSE";
        public const string Grind = "SFX_GRINDER_COMPLETE";
        public const string Pot = "SFX_POT_ADD_HERB";
        public const string BrewSuccess = "SFX_BREW_SUCCESS";
        public const string BrewInvalid = "SFX_BREW_INVALID";
        public const string Discard = "SFX_DISCARD";
        public const string OrderCorrect = "SFX_ORDER_CORRECT";
        public const string Payment = "SFX_PAYMENT_COINS";
        public const string OrderWrong = "SFX_ORDER_WRONG";
        public const string HunterWarning = "SFX_HUNTER_WARNING";
        public const string TimeStop = "SFX_TIME_STOP";
        public const string CardFlip = "SFX_CARD_FLIP";
        public const string CardMatch = "SFX_CARD_MATCH";
        public const string CardMismatch = "SFX_CARD_MISMATCH";
        public const string MinigameComplete = "SFX_MINIGAME_COMPLETE";
        public const string ExpensePay = "SFX_EXPENSE_PAY";
        public const string ExpenseFail = "SFX_EXPENSE_FAIL";
        public const string UiConfirm = "SFX_UI_CONFIRM";
        public const string UiBack = "SFX_UI_BACK";

        public static IReadOnlyList<string> Required { get; } = new[]
        {
            MainMenuMusic, ShopMusic, RewindMusic, GameOverMusic, VictoryMusic,
            ShopAmbience, CustomerBell, GuidebookOpen, GuidebookClose, Grind, Pot,
            BrewSuccess, BrewInvalid, Discard, OrderCorrect, Payment, OrderWrong,
            HunterWarning, TimeStop, CardFlip, CardMatch, CardMismatch,
            MinigameComplete, ExpensePay, ExpenseFail, UiConfirm, UiBack
        };
    }
}
