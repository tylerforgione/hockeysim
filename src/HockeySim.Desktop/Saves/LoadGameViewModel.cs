using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Management.GameManagement;
using HockeySim.Management.Saves;

namespace HockeySim.Desktop.Saves;

/// <summary>
/// Lists the saved games and loads the chosen one in place of any game in progress.
/// </summary>
public sealed partial class LoadGameViewModel : ObservableObject
{
    private readonly GameManager _gameManager;
    private readonly ISavedGameLibrary _saves;
    private readonly Action<string, Action> _confirmDiscardingProgress;
    private readonly Action _back;
    private readonly Action<SaveName> _gameLoaded;
    private readonly bool _hasActiveGame;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    private SavedGameRowViewModel? _selectedSave;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <param name="confirmDiscardingProgress">
    /// Asks before discarding unsaved progress in the game in progress, describing the action that
    /// would discard it, and runs the action only if the user agrees or nothing would be lost.
    /// </param>
    /// <param name="hasActiveGame">Whether a game is in progress, which a failed load leaves as it was.</param>
    public LoadGameViewModel(
        GameManager gameManager,
        ISavedGameLibrary saves,
        Action<string, Action> confirmDiscardingProgress,
        Action back,
        Action<SaveName> gameLoaded,
        bool hasActiveGame)
    {
        ArgumentNullException.ThrowIfNull(gameManager);
        ArgumentNullException.ThrowIfNull(saves);
        ArgumentNullException.ThrowIfNull(confirmDiscardingProgress);
        ArgumentNullException.ThrowIfNull(back);
        ArgumentNullException.ThrowIfNull(gameLoaded);

        _gameManager = gameManager;
        _saves = saves;
        _confirmDiscardingProgress = confirmDiscardingProgress;
        _back = back;
        _gameLoaded = gameLoaded;
        _hasActiveGame = hasActiveGame;
        Saves = SavedGameRowViewModel.List(saves, out _errorMessage);
    }

    public IReadOnlyList<SavedGameRowViewModel> Saves { get; }

    public bool HasSaves => Saves.Count > 0;

    public bool HasError => ErrorMessage is not null;

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private void Load()
    {
        if (SelectedSave is not { } save)
        {
            return;
        }

        ErrorMessage = null;
        _confirmDiscardingProgress($"Loading '{save.Name}'", () => LoadSave(save.Name));
    }

    private bool CanLoad() => SelectedSave is not null;

    [RelayCommand]
    private void Back()
    {
        _back();
    }

    private void LoadSave(SaveName name)
    {
        try
        {
            // Management rebuilds the whole save before replacing anything, so a failure here
            // leaves any game in progress exactly as it was.
            _gameManager.LoadGame(_saves.Open(name));
        }
        catch (GameSaveException exception)
        {
            ErrorMessage = DescribeFailure(name, exception)
                + (_hasActiveGame ? " Your current game is unchanged." : string.Empty);
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