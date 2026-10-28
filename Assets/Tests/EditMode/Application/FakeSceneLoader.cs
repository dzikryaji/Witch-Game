using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;

namespace Witch.Application.Tests
{
    internal sealed class FakeSceneLoader : ISceneLoader
    {
        public System.Collections.Generic.IList<string> Operations { get; set; }

        public System.Collections.Generic.Queue<SceneLoadResult> Results { get; }
            = new System.Collections.Generic.Queue<SceneLoadResult>();

        public SceneLoadResult Result { get; set; } = SceneLoadResult.Success();

        public int LoadCallCount { get; private set; }

        public SceneId? LastRequestedScene { get; private set; }

        public Task<SceneLoadResult> LoadAsync(
            SceneId sceneId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCallCount++;
            LastRequestedScene = sceneId;
            Operations?.Add("scene:" + sceneId);
            return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : Result);
        }
    }
}
