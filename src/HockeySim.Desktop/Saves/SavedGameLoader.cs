using HockeySim.Management.GameManagement;
using HockeySim.Management.Saves;

namespace HockeySim.Desktop.Saves;

/// <summary>
/// Loads a saved game in place of any game in progress, as the load screen and the startup menu
/// both do: confirming the loss of unsaved progress first, and describing a failed load.
/// </summary>
public sealed class SavedGameLoader
{
    private readonly GameManager _gameManager;
    private readonly ISavedGameLibrary _saves;
    private readonly Action<string, Action> _confirmDiscardingProgress;
    private readonly Func<bool> _hasActiveGame;
    private readonly Action<SaveName> _gameLoaded;

    /// <param name="confirmDiscardingProgress">
    /// Asks before discarding unsaved progress in the game in progress, describing the action that
    /// would discard it, and runs the action only if the user agrees or nothing would be lost.
    /// </param>
    /// <param name="hasActiveGame">Whether a game is in progress, which a failed load leaves as it was.</param>
    public SavedGameLoader(
        GameManager gameManager,
        ISavedGameLibrary saves,
        Action<string, Action> confirmDiscardingProgress,
        Func<bool> hasActiveGame,
        Action<SaveName> gameLoaded)
    {
        ArgumentNullException.ThrowIfNull(gameManager);
        ArgumentNullException.ThrowIfNull(saves);
        ArgumentNullException.ThrowIfNull(confirmDiscardingProgress);
        ArgumentNullException.ThrowIfNull(hasActiveGame);
        ArgumentNullException.ThrowIfNull(gameLoaded);

        _gameManager = gameManager;
        _saves = saves;
        _confirmDiscardingProgress = confirmDiscardingProgress;
        _hasActiveGame = hasActiveGame;
        _gameLoaded = gameLoaded;
    }

    /// <summary>
    /// Loads <paramref name="name"/> once any unsaved progress is confirmed away, then reports the
    /// loaded game; a failure is passed to <paramref name="failed"/> instead.
    /// </summary>
    public void Load(SaveName name, Action<string> failed)
    {
        ArgumentNullException.ThrowIfNull(failed);

        _confirmDiscardingProgress($"Loading '{name}'", () => LoadConfirmed(name, failed));
    }

    private void LoadConfirmed(SaveName name, Action<string> failed)
    {
        try
        {
            // Management rebuilds the whole save before replacing anything, so a failure here
            // leaves any game in progress exactly as it was.
            _gameManager.LoadGame(_saves.Open(name));
        }
        catch (GameSaveException exception)
        {
            failed(DescribeFailure(name, exception)
                + (_hasActiveGame() ? " Your current game is unchanged." : string.Empty));
            return;
        }

        _gameLoaded(name);
    }

    private static string DescribeFailure(SaveName name, GameSaveException exception) => exception switch
    {
        UnsupportedGameSaveVersionException unsupported =>
            $"'{name}' was saved by a different version of HockeySim (save format {unsupported.Version}). "
            + $"This version reads save format {unsupported.SupportedVersion} only.",
        InvalidGameSaveException =>
            $"'{name}' is damaged or is not a valid HockeySim save. {exception.Message}",
        _ => $"'{name}' could not be loaded. {exception.Message}",
    };
}