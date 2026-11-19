#if UNITY_EDITOR
using UnityEditor;

namespace Witch.Editor
{
    /// <summary>
    /// Editor-only debug session settings. Stored in EditorPrefs but reset on domain reload
    /// and cleared when the game starts normally from Main Menu without an override so
    /// forgotten test values never leak into normal runs.
    /// </summary>
    [InitializeOnLoad]
    public static class WitchDebugSession
    {
        public const int MinDay = 1;
        public const int MaxDay = 14;
        public const int DefaultDay = 1;
        public const string DefaultStartScene = "MainMenu";
        public const string StartSceneMinigame = "MatchCardMinigame";
        public const string StartSceneShop = "Shop";

        private const string StartSceneKey = "Witch.Debug.StartScene";
        private const string StartDayKey = "Witch.Debug.StartDay";
        private const string ActiveJumpKey = "Witch.Debug.ActiveJump";

        static WitchDebugSession()
        {
            ResetToDefaults();
        }

        public static string StartScene
        {
            get => NormalizeStartScene(EditorPrefs.GetString(StartSceneKey, DefaultStartScene));
            set => EditorPrefs.SetString(StartSceneKey, NormalizeStartScene(value));
        }

        public static int StartDay
        {
            get => ClampDay(EditorPrefs.GetInt(StartDayKey, DefaultDay));
            set => EditorPrefs.SetInt(StartDayKey, ClampDay(value));
        }

        /// <summary>
        /// Set while the current play session was entered via a debug jump/start-day override.
        /// Runtime must never persist saves or report achievements while this is true.
        /// </summary>
        public static bool IsDebugJumpActive
        {
            get => SessionState.GetBool(ActiveJumpKey, false);
            set => SessionState.SetBool(ActiveJumpKey, value);
        }

        public static bool HasActiveStartupOverride =>
            !string.Equals(StartScene, DefaultStartScene, System.StringComparison.Ordinal)
            || StartDay != DefaultDay;

        public static void ResetToDefaults()
        {
            EditorPrefs.SetString(StartSceneKey, DefaultStartScene);
            EditorPrefs.SetInt(StartDayKey, DefaultDay);
            SessionState.SetBool(ActiveJumpKey, false);
        }

        public static void ClearActiveJump()
        {
            SessionState.SetBool(ActiveJumpKey, false);
        }

        public static string NormalizeStartScene(string value)
        {
            if (string.Equals(value, StartSceneShop, System.StringComparison.Ordinal))
            {
                return StartSceneShop;
            }

            if (string.Equals(value, StartSceneMinigame, System.StringComparison.Ordinal))
            {
                return StartSceneMinigame;
            }

            return DefaultStartScene;
        }

        public static int ClampDay(int day)
        {
            if (day < MinDay) return MinDay;
            if (day > MaxDay) return MaxDay;
            return day;
        }
    }
}
#endif
