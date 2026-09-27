using HockeySim.Desktop.Game;
using HockeySim.Management.GameManagement;
using HockeySim.Management.NewGame;

namespace HockeySim.Desktop.Tests;

internal static class GameTestData
{
    public const string ManagedTeamName = "Ottawa Owls";

    public static GameSession StartSession(GameManager? gameManager = null)
    {
        gameManager ??= new GameManager();
        gameManager.StartNewGame(new NewGameCommand(2026, new RandomState(13579), ManagedTeamName));
        return new GameSession(gameManager, "Test Career");
    }
}