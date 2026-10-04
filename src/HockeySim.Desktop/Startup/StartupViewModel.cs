using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HockeySim.Desktop.Startup;

public sealed partial class StartupViewModel : ObservableObject
{
    private readonly Action _continueGame;
    private readonly Action _exitApplication;
    private readonly Action _showNewGame;
    private readonly Action _showLoadGame;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private bool _canContinue;

    public StartupViewModel(Action showNewGame, Action continueGame, Action showLoadGame, Action exitApplication)
    {
        ArgumentNullException.ThrowIfNull(showNewGame);
        ArgumentNullException.ThrowIfNull(continueGame);
        ArgumentNullException.ThrowIfNull(showLoadGame);
        ArgumentNullException.ThrowIfNull(exitApplication);

        _showNewGame = showNewGame;
        _continueGame = continueGame;
        _showLoadGame = showLoadGame;
        _exitApplication = exitApplication;
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private void Continue()
    {
        _continueGame();
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
    private void Exit()
    {
        _exitApplication();
    }
}