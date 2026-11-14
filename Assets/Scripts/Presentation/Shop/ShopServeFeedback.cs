using UnityEngine;

namespace Witch.Presentation.Shop
{
    public static class ShopServeFeedback
    {
        private static readonly string[] CorrectLines =
        {
            "Thanks for the potion!",
            "This is exactly what I needed!",
            "Perfect! Just what I asked for.",
            "Wonderful! This should do nicely.",
            "You've made exactly the right potion!"
        };

        private static readonly string[] IncorrectLines =
        {
            "This is not the correct potion.",
            "I'm afraid this isn't what I ordered.",
            "This isn't quite what I needed.",
            "Something seems wrong with this potion.",
            "I don't think this is the right one."
        };

        public static string GetCorrectLine()
        {
            return CorrectLines[Random.Range(0, CorrectLines.Length)];
        }

        public static string GetIncorrectLine()
        {
            return IncorrectLines[Random.Range(0, IncorrectLines.Length)];
        }

        public static string[] GetCorrectLines() => (string[])CorrectLines.Clone();
        public static string[] GetIncorrectLines() => (string[])IncorrectLines.Clone();
    }
}
