using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Tables;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Roster;

/// <summary>
/// A read-only roster shown by player group (all players, skaters, forwards, defence, or goalies)
/// and column view, with the selected player's summary strip. The tables show ratings, basic season
/// totals, advanced five-on-five figures, or biographies; they do not fit side by side. Once the
/// playoffs start, the totals switch between the regular season and the playoffs, and the player
/// profile opened from the roster follows the same choice.
/// </summary>
public sealed partial class TeamRosterViewModel : ObservableObject
{
    private readonly TeamSnapshot _team;
    private readonly GameSession _session;
    private readonly Action<TeamId>? _openLines;
    private readonly Action<PlayerId, PlayerProfileTab, StatisticsSet>? _openPlayer;
    private List<PlayerRowViewModel> _rows = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayerCount))]
    private IReadOnlyList<PlayerRowViewModel> _skaters = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayerCount))]
    private IReadOnlyList<PlayerRowViewModel> _goalies = [];

    [ObservableProperty]
    private PlayerRowViewModel? _selectedSkater;

    [ObservableProperty]
    private PlayerRowViewModel? _selectedGoalie;

    /// <summary>The selected player's summary strip, or null with no player listed.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenProfileCommand), nameof(OpenGameLogCommand))]
    private PlayerStripViewModel? _selectedStrip;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRatings), nameof(ShowsBasic), nameof(ShowsAdvanced), nameof(ShowsBio))]
    private RosterColumns _columns;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(ShowsAllPlayers),
        nameof(ShowsSkatersGroup),
        nameof(ShowsForwardsGroup),
        nameof(ShowsDefenceGroup),
        nameof(ShowsGoaliesGroup),
        nameof(ShowsSkaterTable),
        nameof(ShowsGoalieTable))]
    private RosterGroup _group;

    /// <summary>Which forwards the Forwards group lists.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ForwardFilterIndex))]
    private ForwardFilter _forwardPosition;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRegularSeason), nameof(ShowsPlayoffs))]
    private StatisticsSet _statistics;

    /// <summary>
    /// The sort the skaters table shows, or none for the default order, which is also the case
    /// while the remembered sort's column is not in the chosen column view.
    /// </summary>
    [ObservableProperty]
    private TableSort? _skaterSort;

    [ObservableProperty]
    private TableSort? _goalieSort;

    /// <param name="statistics">
    /// The totals to show; the regular season's until the playoffs start, whatever is asked.
    /// </param>
    /// <param name="sorts">The sorts to keep, from the roster this one replaces; none sorted by default.</param>
    /// <param name="group">The player group to show, kept from the roster this one replaces.</param>
    /// <param name="forwardPosition">Which forwards the Forwards group lists.</param>
    /// <param name="openLines">Opens the Lines page on this roster's team; the Lines link is disabled without it.</param>
    /// <param name="openPlayer">
    /// Opens a player's profile on a tab with the roster's statistic set; the Profile and Game log links are disabled
    /// without it.
    /// </param>
    public TeamRosterViewModel(
        GameSession session,
        TeamId teamId,
        PlayerId? initiallySelectedPlayerId = null,
        RosterColumns columns = RosterColumns.Ratings,
        StatisticsSet statistics = StatisticsSet.RegularSeason,
        RosterSorts? sorts = null,
        RosterGroup group = RosterGroup.AllPlayers,
        ForwardFilter forwardPosition = ForwardFilter.AllForwards,
        Action<TeamId>? openLines = null,
        Action<PlayerId, PlayerProfileTab, StatisticsSet>? openPlayer = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _team = session.GetTeam(teamId);
        _openLines = openLines;
        _openPlayer = openPlayer;
        _columns = columns;
        _group = group;
        _forwardPosition = forwardPosition;
        Sorts = sorts ?? RosterSorts.None;
        _statistics = statistics.OrRegularSeason(HasPlayoffStatistics);

        ShowStatistics();
        if (initiallySelectedPlayerId is { } id && _team.Roster.Any(player => player.Id == id))
        {
            SelectPlayer(id);
        }
        else
        {
            SelectVisiblePlayer(null);
        }
    }

    /// <summary>
    /// The sorts chosen so far, to hand to the next roster. A sort is kept even when the chosen
    /// column view has no such column.
    /// </summary>
    public RosterSorts Sorts { get; private set; }

    /// <summary>Whether the playoffs have started, so there are playoff statistics to switch to.</summary>
    public bool HasPlayoffStatistics => _session.Snapshot.Season.Playoffs is not null;

    public bool ShowsRegularSeason => Statistics == StatisticsSet.RegularSeason;

    public bool ShowsPlayoffs => Statistics == StatisticsSet.Playoffs;

    public bool ShowsRatings => Columns == RosterColumns.Ratings;

    public bool ShowsBasic => Columns == RosterColumns.Basic;

    public bool ShowsAdvanced => Columns == RosterColumns.Advanced;

    public bool ShowsBio => Columns == RosterColumns.Bio;

    public bool ShowsAllPlayers => Group == RosterGroup.AllPlayers;

    public bool ShowsSkatersGroup => Group == RosterGroup.Skaters;

    public bool ShowsForwardsGroup => Group == RosterGroup.Forwards;

    public bool ShowsDefenceGroup => Group == RosterGroup.Defence;

    public bool ShowsGoaliesGroup => Group == RosterGroup.Goalies;

    /// <summary>Whether the group lists skaters, so the skater table is shown.</summary>
    public bool ShowsSkaterTable => Group != RosterGroup.Goalies;

    /// <summary>Whether the group lists goalies, so the goalie table is shown.</summary>
    public bool ShowsGoalieTable => Group is RosterGroup.AllPlayers or RosterGroup.Goalies;

    /// <summary>The number of players the tables list.</summary>
    public int PlayerCount => Skaters.Count + Goalies.Count;

    /// <summary><see cref="ForwardPosition"/> as a position in the filter's list of choices.</summary>
    public int ForwardFilterIndex
    {
        get => (int)ForwardPosition;
        set
        {
            if (Enum.IsDefined((ForwardFilter)value))
            {
                ForwardPosition = (ForwardFilter)value;
            }
        }
    }

    /// <summary>
    /// Selects a player. A player the group or filter hides is shown by switching to all players.
    /// </summary>
    public void SelectPlayer(PlayerId playerId)
    {
        var row = _rows.Single(row => row.Player.Id == playerId);
        if (!IsVisible(row))
        {
            Group = RosterGroup.AllPlayers;
        }

        SelectRow(row);
    }

    [RelayCommand]
    private void ShowColumns(RosterColumns columns)
    {
        Columns = columns;
    }

    [RelayCommand]
    private void ShowGroup(RosterGroup group)
    {
        Group = group;
    }

    [RelayCommand(CanExecute = nameof(CanOpenLines))]
    private void OpenLines()
    {
        _openLines!(_team.Id);
    }

    private bool CanOpenLines() => _openLines is not null;

    /// <summary>Opens the selected player's profile; a double-click on a row does the same.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenPlayer))]
    private void OpenProfile()
    {
        _openPlayer!(SelectedStrip!.PlayerId, PlayerProfileTab.Overview, Statistics);
    }

    [RelayCommand(CanExecute = nameof(CanOpenPlayer))]
    private void OpenGameLog()
    {
        _openPlayer!(SelectedStrip!.PlayerId, PlayerProfileTab.GameLog, Statistics);
    }

    private bool CanOpenPlayer() => _openPlayer is not null && SelectedStrip is not null;

    /// <summary>Sorts the skaters table by a column, or reverses the sort when it is already sorted by it.</summary>
    [RelayCommand]
    private void SortSkaters(string column)
    {
        Sorts = Sorts with { Skaters = TableSorting.Click(RosterTableColumns.Skaters(Columns), SkaterSort, column) };
        ShowSortedRows();
    }

    [RelayCommand]
    private void SortGoalies(string column)
    {
        Sorts = Sorts with { Goalies = TableSorting.Click(RosterTableColumns.Goalies(Columns), GoalieSort, column) };
        ShowSortedRows();
    }

    partial void OnColumnsChanged(RosterColumns value)
    {
        ShowSortedRows();
    }

    partial void OnGroupChanged(RosterGroup value)
    {
        ShowSortedRows();
    }

    partial void OnForwardPositionChanged(ForwardFilter value)
    {
        ShowSortedRows();
    }

    [RelayCommand]
    private void ShowStatisticsSet(StatisticsSet statistics)
    {
        Statistics = statistics.OrRegularSeason(HasPlayoffStatistics);
    }

    partial void OnStatisticsChanged(StatisticsSet value)
    {
        var selectedPlayerId = SelectedStrip?.PlayerId;
        ShowStatistics();
        SelectVisiblePlayer(selectedPlayerId);
    }

    /// <summary>Rebuilds the tables from the chosen totals.</summary>
    private void ShowStatistics()
    {
        _rows = _team.Roster
            .OrderBy(player => player.Position)
            .ThenBy(player => player.LastName, StringComparer.Ordinal)
            .Select(player => new PlayerRowViewModel(player, _team.Lineup, _session.GetSeasonTotals(player.Id, Statistics))
            {
                OpenProfileCommand = NameLinks.Open<PlayerId>(_openPlayer is null ? null : id => _openPlayer(id, PlayerProfileTab.Overview, Statistics), player.Id),
            })
            .ToList();
        SelectedSkater = null;
        SelectedGoalie = null;
        ApplyRows();
    }

    /// <summary>
    /// Re-lists and re-orders the tables for the chosen group, filter, column view, and sorts,
    /// keeping the selected player when still listed, otherwise selecting the first listed player.
    /// </summary>
    private void ShowSortedRows()
    {
        var selectedPlayerId = SelectedStrip?.PlayerId;
        ApplyRows();
        SelectVisiblePlayer(selectedPlayerId);
    }

    /// <summary>
    /// Lists the group's default-ordered rows (position, then last name) in the remembered sorts;
    /// rows that tie keep that order. A sort whose column the column view lacks is not applied.
    /// </summary>
    private void ApplyRows()
    {
        var skaterColumns = RosterTableColumns.Skaters(Columns);
        var goalieColumns = RosterTableColumns.Goalies(Columns);
        SkaterSort = Present(skaterColumns, Sorts.Skaters);
        GoalieSort = Present(goalieColumns, Sorts.Goalies);
        Skaters = TableSorting.Apply(
            _rows.Where(row => row.Player.Position != Position.Goalie && IsVisible(row)),
            skaterColumns,
            SkaterSort);
        Goalies = TableSorting.Apply(
            _rows.Where(row => row.Player.Position == Position.Goalie && IsVisible(row)),
            goalieColumns,
            GoalieSort);
    }

    private static TableSort? Present(IReadOnlyList<TableColumn<PlayerRowViewModel>> columns, TableSort? sort) =>
        sort is { } chosen && columns.Any(column => column.Key == chosen.Column) ? sort : null;

    /// <summary>Whether the chosen group, and for forwards the position filter, lists the player.</summary>
    private bool IsVisible(PlayerRowViewModel row) => (Group, row.Player.Position) switch
    {
        (RosterGroup.AllPlayers, _) => true,
        (RosterGroup.Goalies, var position) => position == Position.Goalie,
        (_, Position.Goalie) => false,
        (RosterGroup.Skaters, _) => true,
        (RosterGroup.Defence, var position) => position == Position.Defence,
        (RosterGroup.Forwards, var position) => position switch
        {
            Position.Centre => ForwardPosition != ForwardFilter.Wingers,
            Position.Wing => ForwardPosition != ForwardFilter.Centres,
            _ => false,
        },
        _ => false,
    };

    /// <summary>Selects the player when listed, otherwise the first listed player: skaters, then goalies.</summary>
    private void SelectVisiblePlayer(PlayerId? playerId)
    {
        var listed = Skaters.Concat(Goalies).ToList();
        var row = listed.FirstOrDefault(row => row.Player.Id == playerId) ?? listed.FirstOrDefault();
        if (row is null)
        {
            SelectedSkater = null;
            SelectedGoalie = null;
            SelectedStrip = null;
            return;
        }

        SelectRow(row);
    }

    private void SelectRow(PlayerRowViewModel row)
    {
        if (row.Player.Position == Position.Goalie)
        {
            SelectedGoalie = row;
        }
        else
        {
            SelectedSkater = row;
        }
    }

    partial void OnSelectedSkaterChanged(PlayerRowViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedGoalie = null;
        ShowPlayer(value);
    }

    partial void OnSelectedGoalieChanged(PlayerRowViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedSkater = null;
        ShowPlayer(value);
    }

    private void ShowPlayer(PlayerRowViewModel row)
    {
        SelectedStrip = new PlayerStripViewModel(row);
    }
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

    /// <summary>Age, handedness, height, weight, birth date and place, and nationality.</summary>
    Bio,
}

/// <summary>Which players the roster tables list.</summary>
public enum RosterGroup
{
    /// <summary>Skaters and goalies, each in its own table.</summary>
    AllPlayers,

    Skaters,

    /// <summary>Centres and wingers, narrowed by <see cref="ForwardFilter"/>.</summary>
    Forwards,

    Defence,

    Goalies,
}

/// <summary>Which forwards the Forwards group lists. The order is the filter's order of choices.</summary>
public enum ForwardFilter
{
    AllForwards,
    Centres,
    Wingers,
}