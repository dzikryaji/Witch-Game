namespace Witch.Application.Ports
{
    public interface IAudioService
    {
        ServiceResult Play(string cueId);
    }
}
