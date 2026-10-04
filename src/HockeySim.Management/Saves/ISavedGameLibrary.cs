namespace HockeySim.Management.Saves;

/// <summary>
/// The user's saved games, each stored under its own <see cref="SaveName"/>.
/// </summary>
public interface ISavedGameLibrary
{
    /// <summary>
    /// Lists the saved games, most recently saved first.
    /// </summary>
    /// <exception cref="GameSaveStorageException">The saves cannot be listed.</exception>
    IReadOnlyList<SavedGameSummary> List();

    /// <summary>
    /// Whether a game is already saved under <paramref name="name"/>, so saving to it would
    /// replace that game.
    /// </summary>
    /// <exception cref="GameSaveStorageException">The saves cannot be read.</exception>
    bool Contains(SaveName name);

    /// <summary>
    /// The place a game named <paramref name="name"/> is saved to and loaded from. It need not
    /// hold a game yet.
    /// </summary>
    /// <exception cref="GameSaveStorageException">The saves cannot be read.</exception>
    IGameSaveStore Open(SaveName name);
}

/// <summary>
/// A saved game as listed for the user to choose from, without loading it.
/// </summary>
public sealed record SavedGameSummary(SaveName Name, DateTimeOffset SavedAt);