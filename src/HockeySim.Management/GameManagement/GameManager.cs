using HockeySim.Domain;
using HockeySim.Management.Lineups;
using HockeySim.Management.NewGame;

namespace HockeySim.Management.GameManagement;

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

    private Team GetManagedTeam()
    {
        return GetLeague().Teams.Single(team => team.Id == _managedTeamId);
    }

    public GameSnapshot SetLineup(SetLineupCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.ForwardLines);
        ArgumentNullException.ThrowIfNull(command.DefencePairs);

        var team = GetManagedTeam();
        var rosterById = team.Roster.ToDictionary(player => player.Id);

        Player Resolve(PlayerId id)
        {
            if (!rosterById.TryGetValue(id, out var player))
            {
                throw new ArgumentException(
                    $"Player '{id}' does not belong to the managed team's roster.",
                    nameof(command)
                );
            }

            return player;
        }

        var forwardLines = command.ForwardLines.Select(selection => new ForwardLine(
            Resolve(selection.LeftWingId),
            Resolve(selection.CentreId),
            Resolve(selection.RightWingId)
        )).ToList();

        var defencePairs = command.DefencePairs.Select(selection => new DefencePair(
            Resolve(selection.LeftDefenceId),
            Resolve(selection.RightDefenceId)
        )).ToList();

        var starter = Resolve(command.StartingGoalieId);
        var backup = Resolve(command.BackupGoalieId);

        var lineup = new Lineup(forwardLines, defencePairs, starter, backup);

        team.SetLineup(lineup);

        return CreateSnapshot();
    }
}