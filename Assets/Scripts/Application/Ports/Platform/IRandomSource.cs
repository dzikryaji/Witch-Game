namespace Witch.Application.Ports
{
    public interface IRandomSource
    {
        int NextInt(int minimumInclusive, int maximumExclusive);
    }
}
