using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.NewGame;
using HockeySim.Desktop.Startup;
using HockeySim.Management.GameManagement;

namespace HockeySim.Desktop.Main;

public sealed partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNewGameVisible))]
    private bool _isStartupVisible = true;

    public MainWindowViewModel(GameManager gameManager, Action? exitApplication = null)
    {
        ArgumentNullException.ThrowIfNull(gameManager);

        Startup = new StartupViewModel(ShowNewGame, ShowNewGame, exitApplication ?? (() => { }));
        NewGame = new NewGameViewModel(gameManager, ShowStartup, EnableContinue);
    }

    public StartupViewModel Startup { get; }

    public NewGameViewModel NewGame { get; }

    public bool IsNewGameVisible => !IsStartupVisible;

    private void ShowNewGame()
    {
        IsStartupVisible = false;
    }

    private void ShowStartup()
    {
        IsStartupVisible = true;
    }

    private void EnableContinue()
    {
        Startup.CanContinue = true;
    }
}