namespace HockeySim.Management.Saves;

/// <summary>
/// One place a game can be saved to and loaded from, such as a file.
/// </summary>
public interface IGameSaveStore
{
    /// <summary>
    /// Replaces the stored game. A failed save leaves any previously stored game intact.
    /// </summary>
    /// <exception cref="GameSaveStorageException">The game cannot be written.</exception>
    void Save(GameSave save);

    /// <summary>
    /// Reads the stored game.
    /// </summary>
    /// <exception cref="GameSaveStorageException">No game is stored, or it cannot be read.</exception>
    /// <exception cref="UnsupportedGameSaveVersionException">The save uses a format this build cannot read.</exception>
    /// <exception cref="InvalidGameSaveException">The stored data is not a readable save.</exception>
    GameSave Load();
}