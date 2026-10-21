namespace Witch.Application.Flow
{
    public interface IStartNewRunUseCase
    {
        StartNewRunResult Execute(StartNewRunCommand command);
    }
}
