using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Home;
using HockeySim.Desktop.Lines;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.TeamStatistics;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Teams;

/// <summary>
/// One team's page, a page of the shell outside any section. It brings its own banner, in the
/// team's colours, and its own tabs: Roster, Lines, Schedule, and Statistics. Everything but the
/// managed team's lineup is read-only; that is edited here or on the Lines page, which share their
/// unsaved edits.
/// </summary>
public sealed partial class TeamPageViewModel : ShellPageViewModel, IDisposable
{
    private readonly GameSession _session;
    private readonly Action<PlayerId, PlayerProfileTab, StatisticsSet> _openPlayer;
    private readonly TeamPageBannerViewModel _banner;
    private readonly IReadOnlyList<PlayerProfileTabViewModel> _tabs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRoster), nameof(ShowsLines), nameof(ShowsSchedule), nameof(ShowsStatistics))]
    private TeamPageTab _tab;

    [ObservableProperty]
    private TeamRosterViewModel _roster;

    /// <param name="lineupEdits">The managed team's lineup edits, shared with the Lines page.</param>
    /// <param name="openPlayer">Opens a player's profile on a tab with a statistic set.</param>
    /// <param name="openMatch">Opens a match's box score in the given team's schedule.</param>
    /// <param name="openTeam">Opens a team's page, for the schedule's opponents.</param>
    public TeamPageViewModel(
        GameSession session,
        TeamId teamId,
        TeamPageTab tab,
        ManagedLineupEdits lineupEdits,
        Action<PlayerId, PlayerProfileTab, StatisticsSet> openPlayer,
        Action<DateOnly, TeamId> openMatch,
        Action<TeamId>? openTeam = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(lineupEdits);
        ArgumentNullException.ThrowIfNull(openPlayer);
        ArgumentNullException.ThrowIfNull(openMatch);

        _session = session;
        _openPlayer = openPlayer;
        Id = teamId;
        _banner = new TeamPageBannerViewModel(new TeamBannerViewModel(session, teamId));
        _tab = tab;
        _roster = CreateRoster(null, null);
        Lines = new LinesPageViewModel(session, playerId => openPlayer(playerId, PlayerProfileTab.Overview, HomeStatistics.For(session.Snapshot, teamId)), lineupEdits, teamId);
        Schedule = new TeamScheduleViewModel(session, teamId, openMatch, openTeam);
        Statistics = new TeamStatisticsPageViewModel(session, teamId);
        _tabs =
        [
            TabFor("Roster", TeamPageTab.Roster),
            TabFor("Lines", TeamPageTab.Lines),
            TabFor("Schedule", TeamPageTab.Schedule),
            TabFor("Statistics", TeamPageTab.Statistics),
        ];
        UpdateActiveTab();
    }

    public TeamId Id { get; }

    public override string Subtitle => _banner.Team.Division;

    public override object? Banner => _banner;

    public override IReadOnlyList<IPageTab>? Tabs => _tabs;

    /// <summary>Gets the lineup, editable only for the managed team.</summary>
    public LinesPageViewModel Lines { get; }

    public TeamScheduleViewModel Schedule { get; }

    public TeamStatisticsPageViewModel Statistics { get; }

    public bool ShowsRoster => Tab == TeamPageTab.Roster;

    public bool ShowsLines => Tab == TeamPageTab.Lines;

    public bool ShowsSchedule => Tab == TeamPageTab.Schedule;

    public bool ShowsStatistics => Tab == TeamPageTab.Statistics;

    public override void Refresh()
    {
        _banner.Team.Refresh();
        Roster = CreateRoster(Roster.SelectedStrip?.PlayerId, Roster);
        Lines.Refresh();
        Schedule.Refresh();
        Statistics.Refresh();
        OnPropertyChanged(nameof(Subtitle));
    }

    /// <summary>Stops the lineup following the managed team's shared edits, once the page is replaced.</summary>
    public void Dispose()
    {
        Lines.Dispose();
    }

    partial void OnTabChanged(TeamPageTab value)
    {
        UpdateActiveTab();
    }

    private PlayerProfileTabViewModel TabFor(string label, TeamPageTab tab) =>
        new(label, new RelayCommand(() => Tab = tab));

    private void UpdateActiveTab()
    {
        for (var index = 0; index < _tabs.Count; index++)
        {
            _tabs[index].IsActive = index == (int)Tab;
        }
    }

    /// <summary>
    /// Builds the roster from the latest snapshot, keeping the chosen group, columns, statistics, and
    /// sorts of the one it replaces. Its Lines link shows this page's Lines tab.
    /// </summary>
    private TeamRosterViewModel CreateRoster(PlayerId? selectedPlayerId, TeamRosterViewModel? previous) =>
        new(
            _session,
            Id,
            selectedPlayerId,
            previous?.Columns ?? RosterColumns.Ratings,
            previous?.Statistics ?? StatisticsSet.RegularSeason,
            previous?.Sorts,
            previous?.Group ?? RosterGroup.AllPlayers,
            previous?.ForwardPosition ?? ForwardFilter.AllForwards,
            _ => Tab = TeamPageTab.Lines,
            _openPlayer);
}

/// <summary>A team page's selection in the navigation history: the team and the tab it was left on.</summary>
public sealed record TeamPageSelection(TeamId Team, TeamPageTab Tab);

/// <summary>A team page's banner: the team's figures, painted in the team's own colours.</summary>
public sealed record TeamPageBannerViewModel(TeamBannerViewModel Team)
{
    public TeamColours Colours => Team.Colours;
}