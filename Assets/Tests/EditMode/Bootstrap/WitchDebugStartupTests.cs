using NUnit.Framework;
using Witch.Bootstrap;

namespace Witch.Tests.EditMode.Bootstrap
{
    public sealed class WitchDebugStartupTests
    {
        [Test]
        public void Defaults_AreMainMenuAndDay1()
        {
            Assert.That(WitchDebugRuntime.DefaultStartScene, Is.EqualTo("MainMenu"));
            Assert.That(WitchDebugRuntime.DefaultDay, Is.EqualTo(1));
            Assert.That(WitchDebugRuntime.NormalizeStartScene(null), Is.EqualTo("MainMenu"));
            Assert.That(WitchDebugRuntime.NormalizeStartScene("Bogus"), Is.EqualTo("MainMenu"));
            Assert.That(WitchDebugRuntime.NormalizeStartScene("Shop"), Is.EqualTo("Shop"));
            Assert.That(WitchDebugRuntime.NormalizeStartScene("MatchCardMinigame"), Is.EqualTo("MatchCardMinigame"));
            Assert.That(WitchDebugRuntime.ClampDay(0), Is.EqualTo(1));
            Assert.That(WitchDebugRuntime.ClampDay(15), Is.EqualTo(14));
            Assert.That(WitchDebugRuntime.ClampDay(5), Is.EqualTo(5));
        }

        [Test]
        public void RuntimeStartPath_IgnoresOverride_InPlayerBuilds()
        {
            // Editor-only override is consulted only when UNITY_EDITOR startup path is allowed.
            Assert.That(GameCompositionRoot.ShouldUseEditorDebugStartup(isEditorBuild: false, hasActiveOverride: true), Is.False);
            Assert.That(GameCompositionRoot.ShouldUseEditorDebugStartup(isEditorBuild: false, hasActiveOverride: false), Is.False);
            Assert.That(GameCompositionRoot.ShouldUseEditorDebugStartup(isEditorBuild: true, hasActiveOverride: false), Is.False);
            Assert.That(GameCompositionRoot.ShouldUseEditorDebugStartup(isEditorBuild: true, hasActiveOverride: true), Is.True);
        }
    }
}
