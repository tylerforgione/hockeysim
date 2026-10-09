using HockeySim.Desktop.Game;
using HockeySim.Management.GameManagement;
using HockeySim.Management.NewGame;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Desktop.Tests;

internal static class GameTestData
{
    public const string ManagedTeamName = "Ottawa Owls";

    /// <summary>Starts a game and plays its preseason, for pages showing the regular season.</summary>
    public static GameSession StartSession(GameManager? gameManager = null)
    {
        gameManager ??= new GameManager();
        StartPreseasonSession(gameManager);
        gameManager.PlayPreseason();
        return new GameSession(gameManager, "Test Career");
    }

    /// <summary>Starts a game on its first preseason day.</summary>
    public static GameSession StartPreseasonSession(GameManager? gameManager = null)
    {
        gameManager ??= new GameManager();
        gameManager.StartNewGame(new NewGameCommand(2026, new RandomState(13579), ManagedTeamName));
        return new GameSession(gameManager, "Test Career");
    }
}