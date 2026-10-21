namespace Witch.Application.Ports
{
    public enum LoadStateError
    {
        None,
        NotFound,
        CorruptData,
        UnsupportedVersion,
        ReadFailed
    }
}
