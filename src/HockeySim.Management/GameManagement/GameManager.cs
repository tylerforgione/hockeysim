using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Inbox;
using HockeySim.Management.Lineups;
using HockeySim.Management.NewGame;
using HockeySim.Management.Saves;
using HockeySim.Management.Scheduling;
using HockeySim.Management.SeasonPlay;
using HockeySim.Simulation;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Management.GameManagement;

/// <summary>
/// Owns the current game world and exposes controlled commands and immutable snapshots.
/// </summary>
/// <remarks>
/// Commands are serialized, so callers on different threads never observe or change the world
/// part-way through another command, such as a lineup changing while a league day is played.
/// </remarks>
public sealed class GameManager
{
    private readonly IMatchSimulator _matchSimulator;
    private readonly Lock _gate = new();
    private Season? _season;
    private InboxMessages _inbox = new();
    private TeamId _managedTeamId;
    private RandomState _randomState;
    private bool _isAdvancing;

    public GameManager()
        : this(new MatchSimulator())
    {
    }

    public GameManager(IMatchSimulator matchSimulator)
    {
        ArgumentNullException.ThrowIfNull(matchSimulator);
        _matchSimulator = matchSimulator;
    }

    public NewGameOptionsSnapshot GetNewGameOptions() =>
        NewGameOptionsSnapshot.Create(
            FictionalLeagueData.Conferences
                .Select(conference => NewGameConferenceSnapshot.Create(
                    conference.Name,
                    conference.Divisions.Select(division => NewGameDivisionSnapshot.Create(
                        division.Name,
                        division.TeamNames)))));

    public GameSnapshot StartNewGame(NewGameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ManagedTeamName);

        lock (_gate)
        {
            EnsureNotAdvancing();

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

            var schedule = ScheduleGenerator.Create(league, random);
            var inbox = new InboxMessages();
            NewGameMessages.Deliver(inbox, managedTeam, command.SeasonYear);

            _season = new Season(league, schedule);
            _managedTeamId = managedTeam.Id;
            _randomState = random.State;
            _inbox = inbox;
            return CreateSnapshot();
        }
    }

    public GameSnapshot SelectManagedTeam(TeamId teamId)
    {
        lock (_gate)
        {
            EnsureNotAdvancing();

            var league = GetLeague();
            var managedTeam = league.Teams.SingleOrDefault(team => team.Id == teamId)
                ?? throw new ArgumentException("The selected team does not exist in the current league.", nameof(teamId));

            if (teamId == _managedTeamId)
            {
                return CreateSnapshot();
            }

            // The welcome messages describe the managed club, so a new club gets its own set.
            var inbox = new InboxMessages();
            NewGameMessages.Deliver(inbox, managedTeam, league.SeasonYear);

            _managedTeamId = teamId;
            _inbox = inbox;
            return CreateSnapshot();
        }
    }

    public GameSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return CreateSnapshot();
        }
    }

    public GameSnapshot MarkInboxMessageRead(InboxMessageId messageId)
    {
        lock (_gate)
        {
            GetLeague();
            _inbox.MarkRead(messageId);
            return CreateSnapshot();
        }
    }

    public GameSnapshot SetLineup(SetLineupCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.ForwardLines);
        ArgumentNullException.ThrowIfNull(command.DefencePairs);

        lock (_gate)
        {
            EnsureNotAdvancing();

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

    /// <summary>
    /// Plays every match scheduled on the current date, using each team's current lineup, and
    /// moves to the next calendar day. A date without matches simply passes.
    /// </summary>
    /// <remarks>
    /// The day is all-or-nothing: if any match fails, no result is applied, the date does not
    /// move, and the random state is not advanced.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// No game has started, the season is complete, or a day is already being advanced.
    /// </exception>
    public GameSnapshot AdvanceDay()
    {
        lock (_gate)
        {
            EnsureNotAdvancing();

            var season = GetSeason();
            if (season.IsComplete)
            {
                throw new InvalidOperationException("The regular season is complete; no further days can be played.");
            }

            _isAdvancing = true;
            try
            {
                _randomState = LeagueDay.Play(season, _matchSimulator, _randomState);
            }
            finally
            {
                _isAdvancing = false;
            }

            return CreateSnapshot();
        }
    }

    /// <summary>
    /// Writes the whole game to <paramref name="store"/>, replacing whatever it held.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No game has started, or a day is being advanced on this thread.
    /// </exception>
    public void SaveGame(IGameSaveStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        GameSave save;
        lock (_gate)
        {
            EnsureNotAdvancing();
            save = GameSaveCapture.Create(GetSeason(), _managedTeamId, _randomState, _inbox);
        }

        // The save is a detached copy, so writing it does not hold up other commands.
        store.Save(save);
    }

    /// <summary>
    /// Replaces the current game, if any, with the game in <paramref name="store"/>. Continuing
    /// the loaded game with the same commands produces the same results as continuing the game
    /// that was saved, within the same engine version.
    /// </summary>
    /// <remarks>
    /// The save is read and fully rebuilt before anything changes, so a save that cannot be
    /// loaded leaves the current game exactly as it was.
    /// </remarks>
    /// <exception cref="GameSaveException">The save cannot be read or does not describe a valid game.</exception>
    /// <exception cref="InvalidOperationException">A day is being advanced on this thread.</exception>
    public GameSnapshot LoadGame(IGameSaveStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        var restored = GameSaveRestorer.Restore(
            store.Load() ?? throw new InvalidGameSaveException("The save store returned no game."));

        lock (_gate)
        {
            EnsureNotAdvancing();

            _season = restored.Season;
            _managedTeamId = restored.ManagedTeamId;
            _randomState = restored.RandomState;
            _inbox = restored.Inbox;
            return CreateSnapshot();
        }
    }

    /// <summary>
    /// Rejects a command issued from inside a day being played, such as by a match engine calling
    /// back into the game. The lock serializes other threads but is re-entrant on the same one.
    /// </summary>
    private void EnsureNotAdvancing()
    {
        if (_isAdvancing)
        {
            throw new InvalidOperationException("A league day is already being advanced.");
        }
    }

    private GameSnapshot CreateSnapshot() =>
        GameSnapshot.Create(GetSeason(), _managedTeamId, _randomState, _inbox);

    private Season GetSeason() =>
        _season ?? throw new InvalidOperationException("Start a new game before requesting game state.");

    private League GetLeague() => GetSeason().League;

    private Team GetManagedTeam()
    {
        return GetLeague().Teams.Single(team => team.Id == _managedTeamId);
    }
}