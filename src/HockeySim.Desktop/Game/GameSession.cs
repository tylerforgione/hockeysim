using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Players;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Inbox;
using HockeySim.Management.Lineups;

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

    private IReadOnlyDictionary<PlayerId, PlayerSnapshot> _playersById;

    private IReadOnlyDictionary<PlayerId, PlayerSeasonTotals> _seasonTotals;

    public GameSession(GameManager gameManager, string gameName)
    {
        ArgumentNullException.ThrowIfNull(gameManager);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);

        _gameManager = gameManager;
        _snapshot = gameManager.GetSnapshot();
        _playersById = IndexPlayers(_snapshot);
        _seasonTotals = PlayerSeasonTotals.Index(_snapshot.Season);
        GameName = gameName;
    }

    public string GameName { get; }

    public TeamSnapshot ManagedTeam => Snapshot.League.Teams.Single(team => team.Id == Snapshot.ManagedTeamId);

    /// <summary>
    /// Gets every player in the league by identity, for resolving box scores and results.
    /// </summary>
    public IReadOnlyDictionary<PlayerId, PlayerSnapshot> PlayersById => _playersById;

    /// <summary>
    /// Gets a player's current-season totals, or <see cref="PlayerSeasonTotals.None"/> before
    /// their first appearance.
    /// </summary>
    public PlayerSeasonTotals GetSeasonTotals(PlayerId playerId) =>
        _seasonTotals.GetValueOrDefault(playerId, PlayerSeasonTotals.None);

    public TeamSnapshot GetTeam(TeamId teamId) => Snapshot.League.Teams.Single(team => team.Id == teamId);

    public void SetLineup(SetLineupCommand command)
    {
        Snapshot = _gameManager.SetLineup(command);
    }

    public void MarkInboxMessageRead(InboxMessageId messageId)
    {
        Snapshot = _gameManager.MarkInboxMessageRead(messageId);
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
        _seasonTotals = PlayerSeasonTotals.Index(value.Season);
    }

    private static Dictionary<PlayerId, PlayerSnapshot> IndexPlayers(GameSnapshot snapshot) =>
        snapshot.League.Teams.SelectMany(team => team.Roster).ToDictionary(player => player.Id);
}