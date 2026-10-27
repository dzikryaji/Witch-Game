using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Witch.Application.Ports;

namespace Witch.Infrastructure.Scenes
{
    public sealed class UnitySceneLoader : ISceneLoader
    {
        public async Task<SceneLoadResult> LoadAsync(
            SceneId sceneId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                AsyncOperation operation = SceneManager.LoadSceneAsync(sceneId.ToString(), LoadSceneMode.Single);
                if (operation == null)
                {
                    return SceneLoadResult.Failure(SceneLoadError.LoadFailed, "Unity did not create a scene-load operation.");
                }

                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                return SceneLoadResult.Success();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return SceneLoadResult.Failure(SceneLoadError.LoadFailed, exception.Message);
            }
        }
    }
}
