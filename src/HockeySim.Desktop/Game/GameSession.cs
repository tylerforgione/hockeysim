using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Players;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Inbox;
using HockeySim.Management.Lineups;
using HockeySim.Management.Saves;

namespace HockeySim.Desktop.Game;

/// <summary>
/// Presents one running game to the shell's pages: it forwards commands to Management and
/// publishes the latest snapshot so every page renders the same state.
/// </summary>
public sealed partial class GameSession : ObservableObject
{
    private readonly GameManager _gameManager;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ManagedTeam))]
    private GameSnapshot _snapshot;

    [ObservableProperty]
    private bool _isAdvancing;

    private string _gameName;

    private bool _hasUnsavedChanges;

    private IReadOnlyDictionary<PlayerId, PlayerSnapshot> _playersById;

    private IReadOnlyDictionary<PlayerId, PlayerSeasonTotals> _seasonTotals;

    private IReadOnlyDictionary<PlayerId, PlayerSeasonTotals> _playoffTotals;

    /// <param name="isSaved">
    /// Whether the game Management holds is already saved as it stands, as it is just after a load.
    /// A new game is not saved until the user saves it.
    /// </param>
    public GameSession(GameManager gameManager, string gameName, bool isSaved = false)
    {
        ArgumentNullException.ThrowIfNull(gameManager);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);

        _gameManager = gameManager;
        _snapshot = gameManager.GetSnapshot();
        _playersById = IndexPlayers(_snapshot);
        _seasonTotals = PlayerSeasonTotals.Index(_snapshot.Season, StatisticsSet.RegularSeason);
        _playoffTotals = PlayerSeasonTotals.Index(_snapshot.Season, StatisticsSet.Playoffs);
        _gameName = gameName;
        _hasUnsavedChanges = !isSaved;
    }

    /// <summary>
    /// Gets the name the game is presented under: the name chosen for a new game, then the name
    /// it was last saved or loaded as.
    /// </summary>
    public string GameName
    {
        get => _gameName;
        private set => SetProperty(ref _gameName, value);
    }

    /// <summary>
    /// Gets whether the game has changed since it was last saved or loaded, or was never saved, so
    /// replacing it would lose progress.
    /// </summary>
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set => SetProperty(ref _hasUnsavedChanges, value);
    }

    public TeamSnapshot ManagedTeam => Snapshot.League.Teams.Single(team => team.Id == Snapshot.ManagedTeamId);

    /// <summary>
    /// Gets every player in the league by identity, for resolving box scores and results.
    /// </summary>
    public IReadOnlyDictionary<PlayerId, PlayerSnapshot> PlayersById => _playersById;

    /// <summary>
    /// Gets a player's regular-season or playoff totals, or <see cref="PlayerSeasonTotals.None"/>
    /// before their first appearance in that part of the season.
    /// </summary>
    public PlayerSeasonTotals GetSeasonTotals(PlayerId playerId, StatisticsSet statistics = StatisticsSet.RegularSeason) =>
        (statistics == StatisticsSet.Playoffs ? _playoffTotals : _seasonTotals).GetValueOrDefault(playerId, PlayerSeasonTotals.None);

    public TeamSnapshot GetTeam(TeamId teamId) => Snapshot.League.Teams.Single(team => team.Id == teamId);

    public void SetLineup(SetLineupCommand command)
    {
        Snapshot = _gameManager.SetLineup(command);
        HasUnsavedChanges = true;
    }

    public void MarkInboxMessageRead(InboxMessageId messageId)
    {
        Snapshot = _gameManager.MarkInboxMessageRead(messageId);
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Saves the whole game to <paramref name="store"/> under <paramref name="name"/>, which the game
    /// is then presented as.
    /// </summary>
    /// <exception cref="GameSaveException">The game could not be written; nothing changed.</exception>
    /// <exception cref="InvalidOperationException">A day is being advanced.</exception>
    public void SaveGame(IGameSaveStore store, SaveName name)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(name);

        // Saving waits for Management's lock, so saving mid-day would block the window until the
        // day ends, and the save would not hold the state the user saw when choosing to save.
        if (IsAdvancing)
        {
            throw new InvalidOperationException("Wait for the league day to finish before saving.");
        }

        _gameManager.SaveGame(store);
        GameName = name.Value;
        HasUnsavedChanges = false;
    }

    /// <summary>
    /// Plays the current league day off the UI thread so the window stays responsive, then
    /// publishes the resulting state. Management applies a day completely or not at all, so a
    /// failure leaves the published state unchanged and is rethrown for the caller to report.
    /// </summary>
    /// <exception cref="InvalidOperationException">A day is already being advanced.</exception>
    public async Task AdvanceDayAsync()
    {
        if (IsAdvancing)
        {
            throw new InvalidOperationException("A league day is already being advanced.");
        }

        IsAdvancing = true;
        try
        {
            await Task.Run(_gameManager.AdvanceDay);
            HasUnsavedChanges = true;
        }
        finally
        {
            IsAdvancing = false;

            // Management serializes commands, so one issued while the day played (such as a
            // lineup change) waited for it and may have published newer state than the day's own
            // snapshot. Reading the latest snapshot keeps the published state from going back.
            Snapshot = _gameManager.GetSnapshot();
        }
    }

    public (ConferenceSnapshot Conference, DivisionSnapshot Division) FindDivision(TeamSnapshot team)
    {
        foreach (var conference in Snapshot.League.Conferences)
        {
            foreach (var division in conference.Divisions)
            {
                if (division.Teams.Any(candidate => candidate.Id == team.Id))
                {
                    return (conference, division);
                }
            }
        }

        throw new InvalidOperationException($"Team '{team.Name}' does not belong to a division.");
    }

    partial void OnSnapshotChanged(GameSnapshot value)
    {
        _playersById = IndexPlayers(value);
        _seasonTotals = PlayerSeasonTotals.Index(value.Season, StatisticsSet.RegularSeason);
        _playoffTotals = PlayerSeasonTotals.Index(value.Season, StatisticsSet.Playoffs);
    }

    private static Dictionary<PlayerId, PlayerSnapshot> IndexPlayers(GameSnapshot snapshot) =>
        snapshot.League.Teams.SelectMany(team => team.Roster).ToDictionary(player => player.Id);
}