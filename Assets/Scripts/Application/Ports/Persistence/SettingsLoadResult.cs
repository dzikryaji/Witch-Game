using System;

namespace Witch.Application.Ports
{
    public sealed class SettingsLoadResult
    {
        private SettingsLoadResult(GameSettingsState settings, LoadStateError error, string message)
        {
            Settings = settings;
            Error = error;
            ErrorMessage = message;
        }

        public bool Succeeded => Error == LoadStateError.None;
        public GameSettingsState Settings { get; }
        public LoadStateError Error { get; }
        public string ErrorMessage { get; }

        public static SettingsLoadResult Success(GameSettingsState settings)
        {
            return new SettingsLoadResult(
                settings ?? throw new ArgumentNullException(nameof(settings)),
                LoadStateError.None,
                null);
        }

        public static SettingsLoadResult Failure(LoadStateError error, string message)
        {
            return new SettingsLoadResult(null, error, message);
        }
    }
}
