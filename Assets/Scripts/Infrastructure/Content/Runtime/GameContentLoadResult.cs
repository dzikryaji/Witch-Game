using System.Collections.Generic;

namespace Witch.Infrastructure.Content
{
    public sealed class GameContentLoadResult
    {
        private GameContentLoadResult(GameRuntimeContent content, IReadOnlyList<string> errors)
        {
            Content = content;
            Errors = new List<string>(errors).AsReadOnly();
        }

        public bool Succeeded => Content != null && Errors.Count == 0;
        public GameRuntimeContent Content { get; }
        public IReadOnlyList<string> Errors { get; }

        public static GameContentLoadResult Success(GameRuntimeContent content)
        {
            return new GameContentLoadResult(content, new string[0]);
        }

        public static GameContentLoadResult Failure(IReadOnlyList<string> errors)
        {
            return new GameContentLoadResult(null, errors);
        }
    }
}
