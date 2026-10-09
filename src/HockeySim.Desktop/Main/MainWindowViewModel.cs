using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Confirmation;
using HockeySim.Desktop.Game;
using HockeySim.Desktop.NewGame;
using HockeySim.Desktop.Saves;
using HockeySim.Desktop.Startup;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.Saves;

namespace HockeySim.Desktop.Main;

/// <summary>
/// Chooses the top-level screen: the startup menu, new-game setup, the saved games, or the running
/// game. Anything that would replace or close a game with unsaved progress is confirmed first.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly GameManager _gameManager;
    private readonly ISavedGameLibrary _saves;
    private readonly Action _exitApplication;
    private bool _exitConfirmed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStartupVisible))]
    [NotifyPropertyChangedFor(nameof(IsNewGameVisible))]
    [NotifyPropertyChangedFor(nameof(IsGameVisible))]
    private object _currentScreen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TeamColours))]
    private GameShellViewModel? _game;

    public MainWindowViewModel(GameManager gameManager, ISavedGameLibrary saves, Action? exitApplication = null)
    {
        ArgumentNullException.ThrowIfNull(gameManager);
        ArgumentNullException.ThrowIfNull(saves);

        _gameManager = gameManager;
        _saves = saves;
        _exitApplication = exitApplication ?? (() => { });
        Startup = new StartupViewModel(ShowNewGame, ContinueGame, () => ShowLoadGame(ShowStartup), RequestExit);
        _currentScreen = Startup;
    }

    public StartupViewModel Startup { get; }

    /// <summary>
    /// Gets the confirmation shown over whichever screen is current.
    /// </summary>
    public ConfirmationViewModel Confirmation { get; } = new();

    public NewGameViewModel? NewGame => CurrentScreen as NewGameViewModel;

    public LoadGameViewModel? LoadGame => CurrentScreen as LoadGameViewModel;

    public bool IsStartupVisible => CurrentScreen == Startup;

    public bool IsNewGameVisible => CurrentScreen is NewGameViewModel;

    public bool IsGameVisible => CurrentScreen is GameShellViewModel;

    /// <summary>
    /// Gets the colours the window wears: the managed team's while a game is open, on every screen,
    /// or none before the first game, leaving the league defaults.
    /// </summary>
    public TeamColours? TeamColours => Game?.Session.ManagedTeam.Colours;

    /// <summary>
    /// Gets whether a game is in progress with changes that closing it would lose.
    /// </summary>
    public bool HasUnsavedProgress => Game?.Session.HasUnsavedChanges == true;

    /// <summary>
    /// Gets whether the window may close straight away: nothing would be lost, or the user has
    /// already agreed to exit.
    /// </summary>
    public bool CanCloseWithoutConfirmation => _exitConfirmed || !HasUnsavedProgress;

    /// <summary>
    /// Exits the application, first asking whether to discard any unsaved progress.
    /// </summary>
    public void RequestExit()
    {
        ConfirmDiscardingProgress("Exiting", () =>
        {
            _exitConfirmed = true;
            _exitApplication();
        });
    }

    partial void OnCurrentScreenChanged(object value)
    {
        OnPropertyChanged(nameof(NewGame));
        OnPropertyChanged(nameof(LoadGame));
    }

    /// <summary>
    /// Runs <paramref name="proceed"/> straight away when nothing would be lost; otherwise asks the
    /// user first. <paramref name="action"/> names what would discard the progress, such as
    /// "Starting a new game".
    /// </summary>
    private void ConfirmDiscardingProgress(string action, Action proceed)
    {
        if (!HasUnsavedProgress)
        {
            proceed();
            return;
        }

        Confirmation.Request(
            "Discard unsaved progress?",
            $"'{Game!.GameName}' has progress that has not been saved. {action} will discard it.",
            "Discard progress",
            proceed);
    }

    private void ShowNewGame()
    {
        ConfirmDiscardingProgress(
            "Starting a new game",
            () => CurrentScreen = new NewGameViewModel(_gameManager, ShowStartup, StartGame));
    }

    /// <param name="back">Where the load screen's back button returns: the menu or game it was opened from.</param>
    private void ShowLoadGame(Action back)
    {
        CurrentScreen = new LoadGameViewModel(
            _gameManager,
            _saves,
            ConfirmDiscardingProgress,
            back,
            name => ShowGame(new GameSession(_gameManager, name.Value, isSaved: true)),
            hasActiveGame: Game is not null);
    }

    private void ShowStartup()
    {
        CurrentScreen = Startup;
    }

    private void ContinueGame()
    {
        if (Game is not null)
        {
            CurrentScreen = Game;
        }
    }

    private void StartGame(string gameName)
    {
        ShowGame(new GameSession(_gameManager, gameName));
    }

    /// <summary>
    /// Presents the game Management now holds. A loaded game may be a different league entirely,
    /// so the shell and its pages are built afresh rather than refreshed, leaving no selection or
    /// unapplied lineup edit from the replaced game behind.
    /// </summary>
    private void ShowGame(GameSession session)
    {
        Game = new GameShellViewModel(session, ShowStartup, _saves, Confirmation, () => ShowLoadGame(ContinueGame));
        Startup.CanContinue = true;
        CurrentScreen = Game;
    }
}