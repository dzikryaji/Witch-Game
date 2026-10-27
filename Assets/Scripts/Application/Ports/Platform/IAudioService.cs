namespace Witch.Application.Ports
{
    public interface IAudioService
    {
        ServiceResult Play(string cueId);
    }

    public interface IGameAudioService : IAudioService
    {
        ServiceResult PlayMusic(string cueId);
        ServiceResult PlayAmbience(string cueId);
        void StopMusic();
        void StopAmbience();
        void ApplySettings(GameSettingsState settings);
    }
}
