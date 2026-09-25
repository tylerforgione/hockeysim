using HockeySim.Domain;

namespace HockeySim.Management.NewGame;

/// <summary>
/// Owns the current game world and exposes controlled commands and immutable snapshots.
/// </summary>
public sealed class GameManager
{
    private League? _league;
    private TeamId _managedTeamId;
    private RandomState _randomState;

    public GameSnapshot StartNewGame(NewGameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ManagedTeamName);

        var random = new ControlledRandom(command.RandomState);
        var league = LeagueGenerator.Create(command.SeasonYear, random);
        var managedTeam = league.Teams.SingleOrDefault(
            team => string.Equals(team.Name, command.ManagedTeamName, StringComparison.OrdinalIgnoreCase));

        if (managedTeam is null)
        {
            throw new ArgumentException(
                $"The managed team '{command.ManagedTeamName}' does not exist in the fictional league.",
                nameof(command));
        }

        _league = league;
        _managedTeamId = managedTeam.Id;
        _randomState = random.State;
        return CreateSnapshot();
    }

    public GameSnapshot SelectManagedTeam(TeamId teamId)
    {
        var league = GetLeague();
        if (league.Teams.All(team => team.Id != teamId))
        {
            throw new ArgumentException("The selected team does not exist in the current league.", nameof(teamId));
        }

        _managedTeamId = teamId;
        return CreateSnapshot();
    }

    public GameSnapshot GetSnapshot() => CreateSnapshot();

    private GameSnapshot CreateSnapshot() =>
        GameSnapshot.Create(GetLeague(), _managedTeamId, _randomState);

    private League GetLeague() =>
        _league ?? throw new InvalidOperationException("Start a new game before requesting game state.");
}