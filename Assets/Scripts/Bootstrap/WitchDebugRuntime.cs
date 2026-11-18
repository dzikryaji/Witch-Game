#if UNITY_EDITOR
using UnityEditor;

namespace Witch.Bootstrap
{
    /// <summary>
    /// Runtime-accessible bridge to editor-only debug settings (EditorPrefs/SessionState).
    /// Lives in a runtime assembly but compiled only in the Editor; never included in player builds.
    /// Keys must stay in sync with Witch.Editor.WitchDebugSession.
    /// </summary>
    public static class WitchDebugRuntime
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

        public static string StartScene => NormalizeStartScene(EditorPrefs.GetString(StartSceneKey, DefaultStartScene));
        public static int StartDay => ClampDay(EditorPrefs.GetInt(StartDayKey, DefaultDay));

        public static bool IsDebugJumpActive
        {
            get => SessionState.GetBool(ActiveJumpKey, false);
            set => SessionState.SetBool(ActiveJumpKey, value);
        }

        public static bool HasActiveStartupOverride =>
            !string.Equals(StartScene, DefaultStartScene, System.StringComparison.Ordinal)
            || StartDay != DefaultDay;

        public static void ClearActiveJump() => SessionState.SetBool(ActiveJumpKey, false);

        public static string NormalizeStartScene(string value)
        {
            if (string.Equals(value, StartSceneShop, System.StringComparison.Ordinal)) return StartSceneShop;
            if (string.Equals(value, StartSceneMinigame, System.StringComparison.Ordinal)) return StartSceneMinigame;
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
