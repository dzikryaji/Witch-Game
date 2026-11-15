#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Witch.Bootstrap;

namespace Witch.Editor
{
    public static class WitchDebugMenu
    {
        private const string MenuRoot = "Witch/Debug/";

        [MenuItem(MenuRoot + "Start Scene/Main Menu")]
        private static void SetStartMainMenu() { WitchDebugSession.StartScene = WitchDebugSession.DefaultStartScene; LogState(); }

        [MenuItem(MenuRoot + "Start Scene/Shop")]
        private static void SetStartShop() { WitchDebugSession.StartScene = WitchDebugSession.StartSceneShop; LogState(); }

        [MenuItem(MenuRoot + "Start Scene/Rewind Minigame")]
        private static void SetStartRewind() { WitchDebugSession.StartScene = WitchDebugSession.StartSceneRewind; LogState(); }

        [MenuItem(MenuRoot + "Start Day/Day 1")] private static void Day1() { SetDay(1); }
        [MenuItem(MenuRoot + "Start Day/Day 2")] private static void Day2() { SetDay(2); }
        [MenuItem(MenuRoot + "Start Day/Day 3")] private static void Day3() { SetDay(3); }
        [MenuItem(MenuRoot + "Start Day/Day 4")] private static void Day4() { SetDay(4); }
        [MenuItem(MenuRoot + "Start Day/Day 5")] private static void Day5() { SetDay(5); }
        [MenuItem(MenuRoot + "Start Day/Day 6")] private static void Day6() { SetDay(6); }
        [MenuItem(MenuRoot + "Start Day/Day 7")] private static void Day7() { SetDay(7); }
        [MenuItem(MenuRoot + "Start Day/Day 8")] private static void Day8() { SetDay(8); }
        [MenuItem(MenuRoot + "Start Day/Day 9")] private static void Day9() { SetDay(9); }
        [MenuItem(MenuRoot + "Start Day/Day 10")] private static void Day10() { SetDay(10); }
        [MenuItem(MenuRoot + "Start Day/Day 11")] private static void Day11() { SetDay(11); }
        [MenuItem(MenuRoot + "Start Day/Day 12")] private static void Day12() { SetDay(12); }
        [MenuItem(MenuRoot + "Start Day/Day 13")] private static void Day13() { SetDay(13); }
        [MenuItem(MenuRoot + "Start Day/Day 14")] private static void Day14() { SetDay(14); }

        [MenuItem(MenuRoot + "Reset to Defaults (Main Menu, Day 1)")]
        private static void ResetDefaults()
        {
            WitchDebugSession.ResetToDefaults();
            LogState();
        }

        [MenuItem(MenuRoot + "Jump/Previous Day", true)]
        private static bool ValidatePrev() => EditorApplication.isPlaying && GameCompositionRoot.Instance != null;
        [MenuItem(MenuRoot + "Jump/Previous Day")]
        private static void JumpPrev() { GameCompositionRoot.Instance.DebugJumpToDayRelative(-1); }

        [MenuItem(MenuRoot + "Jump/Next Day", true)]
        private static bool ValidateNext() => EditorApplication.isPlaying && GameCompositionRoot.Instance != null;
        [MenuItem(MenuRoot + "Jump/Next Day")]
        private static void JumpNext() { GameCompositionRoot.Instance.DebugJumpToDayRelative(+1); }

        [MenuItem(MenuRoot + "Jump/To Start Day Setting", true)]
        private static bool ValidateJumpSetting() => EditorApplication.isPlaying && GameCompositionRoot.Instance != null;
        [MenuItem(MenuRoot + "Jump/To Start Day Setting")]
        private static void JumpToSetting() { GameCompositionRoot.Instance.DebugJumpToDay(WitchDebugSession.StartDay); }

        private static void SetDay(int day)
        {
            WitchDebugSession.StartDay = day;
            LogState();
        }

        private static void LogState()
        {
            Debug.Log($"[WitchDebug] StartScene={WitchDebugSession.StartScene}, StartDay={WitchDebugSession.StartDay}, ActiveJump={WitchDebugSession.IsDebugJumpActive}");
        }
    }
}
#endif
