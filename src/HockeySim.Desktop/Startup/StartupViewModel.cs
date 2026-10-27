using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Saves;
using HockeySim.Management.Saves;

namespace HockeySim.Desktop.Startup;

/// <summary>
/// The startup menu: Continue, new game, load game, quit, and the most recently saved games to
/// choose from. <see cref="Refresh"/> brings the saves up to date each time the menu is shown.
/// </summary>
public sealed partial class StartupViewModel : ObservableObject
{
    /// <summary>How many of the most recent saves the menu lists.</summary>
    public const int RecentGameCount = 8;

    private readonly ISavedGameLibrary _saves;
    private readonly SavedGameLoader _loader;
    private readonly Func<GameSession?> _openGame;
    private readonly Action _returnToGame;
    private readonly Action _showNewGame;
    private readonly Action _showLoadGame;
    private readonly Action _quit;
    private SavedGameRowViewModel? _newestReadable;

    [ObservableProperty]
    private IReadOnlyList<SavedGameRowViewModel> _recentGames = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasContinueSubtitle))]
    private string _continueSubtitle = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <param name="openGame">The game in progress, which Continue returns to, or null when there is none.</param>
    /// <param name="returnToGame">Shows the game in progress.</param>
    public StartupViewModel(
        ISavedGameLibrary saves,
        SavedGameLoader loader,
        Func<GameSession?> openGame,
        Action returnToGame,
        Action showNewGame,
        Action showLoadGame,
        Action quit)
    {
        ArgumentNullException.ThrowIfNull(saves);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(openGame);
        ArgumentNullException.ThrowIfNull(returnToGame);
        ArgumentNullException.ThrowIfNull(showNewGame);
        ArgumentNullException.ThrowIfNull(showLoadGame);
        ArgumentNullException.ThrowIfNull(quit);

        _saves = saves;
        _loader = loader;
        _openGame = openGame;
        _returnToGame = returnToGame;
        _showNewGame = showNewGame;
        _showLoadGame = showLoadGame;
        _quit = quit;
        Refresh();
    }

    /// <summary>Shown on the startup menu so bug reports can cite the build.</summary>
    public string Version { get; } = AppVersion.Current;

    public bool HasRecentGames => RecentGames.Count > 0;

    public bool HasContinueSubtitle => ContinueSubtitle.Length > 0;

    public bool HasError => ErrorMessage is not null;

    /// <summary>
    /// Reads the saved games again, and what Continue would open: the game in progress, or else
    /// the newest save whose details can be read.
    /// </summary>
    public void Refresh()
    {
        var saves = SavedGameRowViewModel.List(_saves, out var error);
        _newestReadable = saves.FirstOrDefault(save => save.HasDetails);
        RecentGames = saves.Take(RecentGameCount).ToList();
        ErrorMessage = error;

        ContinueSubtitle = _openGame() is { } game
            ? Describe(game.GameName, game.Snapshot.Season.CurrentDate)
            : _newestReadable is { Details: { } details } newest
                ? Describe(newest.DisplayName, details.CurrentDate)
                : string.Empty;
        ContinueCommand.NotifyCanExecuteChanged();
    }

    partial void OnRecentGamesChanged(IReadOnlyList<SavedGameRowViewModel> value)
    {
        OnPropertyChanged(nameof(HasRecentGames));
    }

    private static string Describe(string name, DateOnly date) => $"{name} · {SavedGameRowViewModel.InGameDate(date)}";

    private bool CanContinue() => _openGame() is not null || _newestReadable is not null;

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private void Continue()
    {
        if (_openGame() is not null)
        {
            _returnToGame();
        }
        else if (_newestReadable is { } newest)
        {
            Load(newest);
        }
    }

    [RelayCommand]
    private void OpenSave(SavedGameRowViewModel? save)
    {
        if (save is not null)
        {
            Load(save);
        }
    }

    private void Load(SavedGameRowViewModel save)
    {
        ErrorMessage = null;
        _loader.Load(save.Name, message => ErrorMessage = message);
    }

    [RelayCommand]
    private void ShowNewGame()
    {
        _showNewGame();
    }

    [RelayCommand]
    private void ShowLoadGame()
    {
        _showLoadGame();
    }

    [RelayCommand]
    private void Quit()
    {
        _quit();
    }
}