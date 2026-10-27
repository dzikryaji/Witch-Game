using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Witch.Editor
{
    [InitializeOnLoad]
    public static class PlayFromBootstrap
    {
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string TestRunnerApiTypeName =
            "UnityEditor.TestTools.TestRunner.Api.TestRunnerApi, UnityEditor.TestRunner";

        private static SceneAsset bootstrap;
        private static readonly MethodInfo IsTestRunActiveMethod = Type
            .GetType(TestRunnerApiTypeName)
            ?.GetMethod("IsRunActive", BindingFlags.Static | BindingFlags.NonPublic);

        static PlayFromBootstrap()
        {
            EditorApplication.update += RefreshStartScene;
            EditorApplication.playModeStateChanged += _ => RefreshStartScene();
            RefreshStartScene();
        }

        private static void RefreshStartScene()
        {
            if (IsTestRunActive())
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            bootstrap ??= AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
            if (bootstrap != null && EditorSceneManager.playModeStartScene != bootstrap)
            {
                EditorSceneManager.playModeStartScene = bootstrap;
            }
        }

        private static bool IsTestRunActive()
        {
            return IsTestRunActiveMethod != null &&
                   (bool)IsTestRunActiveMethod.Invoke(null, null);
        }
    }
}
