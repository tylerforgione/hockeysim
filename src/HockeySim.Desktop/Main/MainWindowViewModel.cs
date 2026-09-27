using HockeySim.Desktop.NewGame;
using HockeySim.Management.GameManagement;

namespace HockeySim.Desktop.Main;

public sealed class MainWindowViewModel
{
    public MainWindowViewModel(GameManager gameManager)
    {
        ArgumentNullException.ThrowIfNull(gameManager);
        NewGame = new NewGameViewModel(gameManager);
    }

    public NewGameViewModel NewGame { get; }
}