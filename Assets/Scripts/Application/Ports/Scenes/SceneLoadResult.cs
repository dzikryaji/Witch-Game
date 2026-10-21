using System;

namespace Witch.Application.Ports
{
    public sealed class SceneLoadResult
    {
        private SceneLoadResult(SceneLoadError error, string errorMessage)
        {
            Error = error;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded => Error == SceneLoadError.None;

        public SceneLoadError Error { get; }

        public string ErrorMessage { get; }

        public static SceneLoadResult Success()
        {
            return new SceneLoadResult(SceneLoadError.None, null);
        }

        public static SceneLoadResult Failure(SceneLoadError error, string errorMessage)
        {
            if (error == SceneLoadError.None)
            {
                throw new ArgumentException("A failed scene-load result requires an error.", nameof(error));
            }

            return new SceneLoadResult(error, errorMessage);
        }
    }
}
