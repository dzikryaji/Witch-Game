namespace Witch.Application.Flow
{
    public enum StartNewGameError
    {
        None,
        MissingCommand,
        InvalidInitialState,
        SaveFailed,
        SceneLoadFailed
    }
}
