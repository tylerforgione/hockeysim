using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Roster;

/// <summary>
/// A read-only roster split into skaters and goalies, with the team's season statistics and the
/// selected player's profile. The tables show ratings, basic season totals, or advanced
/// five-on-five figures; they do not fit side by side.
/// </summary>
public sealed partial class TeamRosterViewModel : ObservableObject
{
    private readonly TeamSnapshot _team;
    private readonly GameSession _session;

    [ObservableProperty]
    private PlayerRowViewModel? _selectedSkater;

    [ObservableProperty]
    private PlayerRowViewModel? _selectedGoalie;

    [ObservableProperty]
    private PlayerDetailViewModel? _selectedPlayer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRatings), nameof(ShowsBasic), nameof(ShowsAdvanced))]
    private RosterColumns _columns;

    public TeamRosterViewModel(
        GameSession session,
        TeamId teamId,
        PlayerId? initiallySelectedPlayerId = null,
        RosterColumns columns = RosterColumns.Ratings)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _team = session.GetTeam(teamId);
        _columns = columns;
        var rows = _team.Roster
            .OrderBy(player => player.Position)
            .ThenBy(player => player.LastName, StringComparer.Ordinal)
            .Select(player => new PlayerRowViewModel(player, _team.Lineup, session.GetSeasonTotals(player.Id)))
            .ToList();
        Skaters = rows.Where(row => row.Player.Position != Position.Goalie).ToList();
        Goalies = rows.Where(row => row.Player.Position == Position.Goalie).ToList();
        TeamStatistics = TeamStatisticsDisplay.Strip(
            session.Snapshot.Season.TeamStatistics.Single(statistics => statistics.TeamId == teamId));

        var initialRow = rows.FirstOrDefault(row => row.Player.Id == initiallySelectedPlayerId) ?? rows[0];
        SelectPlayer(initialRow.Player.Id);
    }

    public IReadOnlyList<PlayerRowViewModel> Skaters { get; }

    public IReadOnlyList<PlayerRowViewModel> Goalies { get; }

    /// <summary>
    /// The team's current-season special teams, faceoff percentage, and five-on-five shares.
    /// </summary>
    public IReadOnlyList<SeasonStatViewModel> TeamStatistics { get; }

    public bool ShowsRatings => Columns == RosterColumns.Ratings;

    public bool ShowsBasic => Columns == RosterColumns.Basic;

    public bool ShowsAdvanced => Columns == RosterColumns.Advanced;

    public void SelectPlayer(PlayerId playerId)
    {
        var row = Skaters.Concat(Goalies).Single(row => row.Player.Id == playerId);
        if (row.Player.Position == Position.Goalie)
        {
            SelectedGoalie = row;
        }
        else
        {
            SelectedSkater = row;
        }
    }

    [RelayCommand]
    private void ShowColumns(RosterColumns columns)
    {
        Columns = columns;
    }

    partial void OnSelectedSkaterChanged(PlayerRowViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedGoalie = null;
        SelectedPlayer = CreateDetail(value);
    }

    partial void OnSelectedGoalieChanged(PlayerRowViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedSkater = null;
        SelectedPlayer = CreateDetail(value);
    }

    private PlayerDetailViewModel CreateDetail(PlayerRowViewModel row) =>
        new(row.Player, _team, row.Season, _session.Snapshot.League.SeasonYear);
}

/// <summary>
/// Which columns the roster tables show alongside each player's identity and lineup role.
/// </summary>
public enum RosterColumns
{
    Ratings,

    /// <summary>Current-season counting totals and rates.</summary>
    Basic,

    /// <summary>Five-on-five on-ice shares and expected goals; goalie expected goals.</summary>
    Advanced,
}