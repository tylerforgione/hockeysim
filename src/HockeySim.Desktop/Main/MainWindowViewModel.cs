using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.NewGame;
using HockeySim.Desktop.Startup;
using HockeySim.Management.GameManagement;

namespace HockeySim.Desktop.Main;

/// <summary>
/// Chooses the top-level screen: the startup menu, new-game setup, or the running game.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly GameManager _gameManager;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStartupVisible))]
    [NotifyPropertyChangedFor(nameof(IsNewGameVisible))]
    [NotifyPropertyChangedFor(nameof(IsGameVisible))]
    private object _currentScreen;

    [ObservableProperty]
    private GameShellViewModel? _game;

    public MainWindowViewModel(GameManager gameManager, Action? exitApplication = null)
    {
        ArgumentNullException.ThrowIfNull(gameManager);

        _gameManager = gameManager;
        Startup = new StartupViewModel(ShowNewGame, ContinueGame, exitApplication ?? (() => { }));
        _currentScreen = Startup;
    }

    public StartupViewModel Startup { get; }

    public NewGameViewModel? NewGame => CurrentScreen as NewGameViewModel;

    public bool IsStartupVisible => CurrentScreen == Startup;

    public bool IsNewGameVisible => CurrentScreen is NewGameViewModel;

    public bool IsGameVisible => CurrentScreen is GameShellViewModel;

    partial void OnCurrentScreenChanged(object value)
    {
        OnPropertyChanged(nameof(NewGame));
    }

    private void ShowNewGame()
    {
        CurrentScreen = new NewGameViewModel(_gameManager, ShowStartup, StartGame);
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
        Game = new GameShellViewModel(new GameSession(_gameManager, gameName), ShowStartup);
        Startup.CanContinue = true;
        CurrentScreen = Game;
    }
}