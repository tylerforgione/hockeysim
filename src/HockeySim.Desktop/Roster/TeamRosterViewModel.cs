using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Tables;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Roster;

/// <summary>
/// A read-only roster split into skaters and goalies, with the selected player's profile. The
/// tables show ratings, basic season totals, or advanced five-on-five figures; they do not fit side
/// by side. Once the playoffs start, the totals and the profile switch together between the regular
/// season and the playoffs.
/// </summary>
public sealed partial class TeamRosterViewModel : ObservableObject
{
    private readonly TeamSnapshot _team;
    private readonly GameSession _session;
    private List<PlayerRowViewModel> _rows = [];

    [ObservableProperty]
    private IReadOnlyList<PlayerRowViewModel> _skaters = [];

    [ObservableProperty]
    private IReadOnlyList<PlayerRowViewModel> _goalies = [];

    [ObservableProperty]
    private PlayerRowViewModel? _selectedSkater;

    [ObservableProperty]
    private PlayerRowViewModel? _selectedGoalie;

    [ObservableProperty]
    private PlayerDetailViewModel? _selectedPlayer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRatings), nameof(ShowsBasic), nameof(ShowsAdvanced))]
    private RosterColumns _columns;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRegularSeason), nameof(ShowsPlayoffs))]
    private StatisticsSet _statistics;

    /// <summary>The skaters table's sort in the chosen column view, or none for the default order.</summary>
    [ObservableProperty]
    private TableSort? _skaterSort;

    [ObservableProperty]
    private TableSort? _goalieSort;

    /// <param name="statistics">
    /// The totals to show; the regular season's until the playoffs start, whatever is asked.
    /// </param>
    /// <param name="sorts">The sorts to keep, from the roster this one replaces; none sorted by default.</param>
    public TeamRosterViewModel(
        GameSession session,
        TeamId teamId,
        PlayerId? initiallySelectedPlayerId = null,
        RosterColumns columns = RosterColumns.Ratings,
        StatisticsSet statistics = StatisticsSet.RegularSeason,
        RosterSorts? sorts = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _team = session.GetTeam(teamId);
        _columns = columns;
        Sorts = sorts ?? RosterSorts.None;
        _statistics = HasPlayoffStatistics ? statistics : StatisticsSet.RegularSeason;

        ShowStatistics();
        SelectPlayer(initiallySelectedPlayerId is { } id && _team.Roster.Any(player => player.Id == id)
            ? id
            : Skaters.Concat(Goalies).First().Player.Id);
    }

    /// <summary>The sorts chosen so far in every column view, to hand to the next roster.</summary>
    public RosterSorts Sorts { get; private set; }

    /// <summary>Whether the playoffs have started, so there are playoff statistics to switch to.</summary>
    public bool HasPlayoffStatistics => _session.Snapshot.Season.Playoffs is not null;

    public bool ShowsRegularSeason => Statistics == StatisticsSet.RegularSeason;

    public bool ShowsPlayoffs => Statistics == StatisticsSet.Playoffs;

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

    /// <summary>Sorts the skaters table by a column, or reverses the sort when it is already sorted by it.</summary>
    [RelayCommand]
    private void SortSkaters(string column)
    {
        var columns = RosterTableColumns.Skaters(Columns);
        Sorts = Sorts.With(Columns, false, TableSorting.Click(columns, Sorts.Get(Columns, false), column));
        ShowSortedRows();
    }

    [RelayCommand]
    private void SortGoalies(string column)
    {
        var columns = RosterTableColumns.Goalies(Columns);
        Sorts = Sorts.With(Columns, true, TableSorting.Click(columns, Sorts.Get(Columns, true), column));
        ShowSortedRows();
    }

    partial void OnColumnsChanged(RosterColumns value)
    {
        ShowSortedRows();
    }

    [RelayCommand]
    private void ShowStatisticsSet(StatisticsSet statistics)
    {
        Statistics = HasPlayoffStatistics ? statistics : StatisticsSet.RegularSeason;
    }

    partial void OnStatisticsChanged(StatisticsSet value)
    {
        var selectedPlayerId = SelectedPlayer?.Id;
        ShowStatistics();
        if (selectedPlayerId is { } id)
        {
            SelectPlayer(id);
        }
    }

    /// <summary>Rebuilds the tables from the chosen totals.</summary>
    private void ShowStatistics()
    {
        _rows = _team.Roster
            .OrderBy(player => player.Position)
            .ThenBy(player => player.LastName, StringComparer.Ordinal)
            .Select(player => new PlayerRowViewModel(player, _team.Lineup, _session.GetSeasonTotals(player.Id, Statistics)))
            .ToList();
        SelectedSkater = null;
        SelectedGoalie = null;
        ApplySorts();
    }

    /// <summary>Re-orders the tables by the sorts of the chosen column view, keeping the selected player.</summary>
    private void ShowSortedRows()
    {
        var selectedPlayerId = SelectedPlayer?.Id;
        ApplySorts();
        if (selectedPlayerId is { } id)
        {
            SelectPlayer(id);
        }
    }

    /// <summary>
    /// Orders the default-ordered rows (position, then last name) by the column view's sorts; rows
    /// that tie keep that order.
    /// </summary>
    private void ApplySorts()
    {
        SkaterSort = Sorts.Get(Columns, false);
        GoalieSort = Sorts.Get(Columns, true);
        Skaters = TableSorting.Apply(
            _rows.Where(row => row.Player.Position != Position.Goalie),
            RosterTableColumns.Skaters(Columns),
            SkaterSort);
        Goalies = TableSorting.Apply(
            _rows.Where(row => row.Player.Position == Position.Goalie),
            RosterTableColumns.Goalies(Columns),
            GoalieSort);
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
        new(row.Player, _team, row.Season, _session.Snapshot.League.SeasonYear, Statistics);
}

/// <summary>
/// Which columns the roster tables show alongside each player's identity and lineup role.
/// </summary>
public enum RosterColumns
{
    Ratings,

    /// <summary>Counting totals and rates for the regular season or the playoffs.</summary>
    Basic,

    /// <summary>Five-on-five on-ice shares and expected goals; goalie expected goals.</summary>
    Advanced,
}