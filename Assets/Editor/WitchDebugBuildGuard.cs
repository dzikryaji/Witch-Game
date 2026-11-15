#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Witch.Editor
{
    /// <summary>
    /// macOS-only build guard: prevents shipping with a debug start override active or with
    /// an incorrect first build scene. Debug overrides are editor-only and must never leak.
    /// </summary>
    public sealed class WitchDebugBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Scope: macOS only (global scene list + macOS build profile).
            if (report.summary.platform != BuildTarget.StandaloneOSX)
            {
                return;
            }

            if (WitchDebugSession.HasActiveStartupOverride)
            {
                throw new BuildFailedException(
                    $"[WitchDebugBuildGuard] Refusing macOS build: debug start override is active " +
                    $"(StartScene={WitchDebugSession.StartScene}, StartDay={WitchDebugSession.StartDay}). " +
                    "Reset to defaults via Witch/Debug/Reset to Defaults (Main Menu, Day 1) before building.");
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
            if (scenes.Count == 0 || scenes[0] != "Assets/Scenes/Bootstrap.unity")
            {
                string first = scenes.Count == 0 ? "<none>" : scenes[0];
                throw new BuildFailedException(
                    $"[WitchDebugBuildGuard] Refusing macOS build: first enabled build scene must be Assets/Scenes/Bootstrap.unity (got '{first}'). " +
                    "Check ProjectSettings/EditorBuildSettings.asset and Assets/Settings/Build Profiles/macOS.asset (override disabled).");
            }

            // macOS profile must not override global scene list in a conflicting way.
            var macProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.ScriptableObject>(
                "Assets/Settings/Build Profiles/macOS.asset");
            if (macProfile != null)
            {
                var so = new SerializedObject(macProfile);
                SerializedProperty overrideProp = so.FindProperty("m_OverrideGlobalSceneList");
                if (overrideProp != null && overrideProp.boolValue)
                {
                    SerializedProperty list = so.FindProperty("m_Scenes");
                    string firstOverride = "<none>";
                    if (list != null && list.isArray && list.arraySize > 0)
                    {
                        SerializedProperty elem = list.GetArrayElementAtIndex(0);
                        SerializedProperty pathProp = elem.FindPropertyRelative("path") ?? elem.FindPropertyRelative("m_Path");
                        if (pathProp != null) firstOverride = pathProp.stringValue;
                    }
                    if (firstOverride != "Assets/Scenes/Bootstrap.unity")
                    {
                        throw new BuildFailedException(
                            $"[WitchDebugBuildGuard] Refusing macOS build: macOS build profile overrides scene list and first scene is '{firstOverride}', expected Assets/Scenes/Bootstrap.unity.");
                    }
                }
            }

            Debug.Log("[WitchDebugBuildGuard] macOS build preflight OK: no debug override, Bootstrap is first scene.");
        }
    }
}
#endif
