using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Witch.Infrastructure.Tests
{
    public sealed class BuildSettingsTests
    {
        [Test]
        public void EnabledScenesMatchProductionOrderExactly()
        {
            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            Assert.That(enabledScenes, Is.EqualTo(new[]
            {
                "Assets/Scenes/Bootstrap.unity",
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/Shop.unity",
                "Assets/Scenes/RewindMinigame.unity"
            }));
        }

        [Test]
        public void MacOSBuildProfileDoesNotShipDevelopmentSettings()
        {
            string yaml = System.IO.File.ReadAllText("Assets/Settings/Build Profiles/macOS.asset");
            foreach (string flag in new[]
            {
                "m_Development",
                "m_ConnectProfiler",
                "m_BuildWithDeepProfilingSupport",
                "m_BuildWithCodeCoverage",
                "m_AllowDebugging",
                "m_WaitForManagedDebugger"
            })
            {
                StringAssert.Contains(flag + ": 0", yaml);
            }
        }
    }
}
