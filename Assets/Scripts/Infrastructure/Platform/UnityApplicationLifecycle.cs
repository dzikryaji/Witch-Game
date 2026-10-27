using UnityEngine;
using Witch.Application.Ports;

namespace Witch.Infrastructure.Platform
{
    public sealed class UnityApplicationLifecycle : IApplicationLifecycle
    {
        public ServiceResult RequestQuit()
        {
            UnityEngine.Application.Quit();
            return ServiceResult.Success();
        }
    }
}
