using System.Threading;
using System.Threading.Tasks;

namespace Witch.Application.Ports
{
    public interface ISceneLoader
    {
        Task<SceneLoadResult> LoadAsync(
            SceneId sceneId,
            CancellationToken cancellationToken = default);
    }
}
