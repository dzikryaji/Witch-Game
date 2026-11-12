using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Witch.Presentation.MainMenu;
using Object = UnityEngine.Object;

namespace Witch.Bootstrap.PlayModeTests
{
    public sealed class GameCenterPlayModeTests
    {
        [UnityTest]
        public IEnumerator UnavailableGameCenterDoesNotBlockStartupOrNewGame()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Bootstrap");
            while (!load.isDone)
            {
                yield return null;
            }

            float deadline = Time.realtimeSinceStartup + 10f;
            while (SceneManager.GetActiveScene().name != "MainMenu"
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            MainMenuPresenter menu = Object.FindAnyObjectByType<MainMenuPresenter>();
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.IsInitialized, Is.True);

            Task<bool> newGame = menu.NewGameAsync();
            while (!newGame.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(newGame.IsCompleted, Is.True, "New Game did not complete after Game Center initialization.");
            Assert.That(newGame.Result, Is.True, menu.LastErrorMessage);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Shop"));
        }
    }
}
