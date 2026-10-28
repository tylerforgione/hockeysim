using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Domain;

namespace HockeySim.Desktop.TeamStatistics;

/// <summary>
/// A team's special teams, faceoffs, and five-on-five shares, the managed team's by default. Once
/// the playoffs start, the page switches between the regular season's and the playoffs'. A team
/// page hosts it for its own team.
/// </summary>
public sealed partial class TeamStatisticsPageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;
    private readonly TeamId _teamId;

    [ObservableProperty]
    private IReadOnlyList<SeasonStatViewModel> _statistics = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRegularSeason), nameof(ShowsPlayoffs))]
    private StatisticsSet _statisticsSet;

    /// <param name="teamId">The team to show; the managed team without it.</param>
    public TeamStatisticsPageViewModel(GameSession session, TeamId? teamId = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _teamId = teamId ?? session.Snapshot.ManagedTeamId;
        Refresh();
    }

    public override string Subtitle => _session.GetTeam(_teamId).Name;

    /// <summary>Whether the playoffs have started, so there are playoff statistics to switch to.</summary>
    public bool HasPlayoffStatistics => _session.Snapshot.Season.Playoffs is not null;

    public bool ShowsRegularSeason => StatisticsSet == StatisticsSet.RegularSeason;

    public bool ShowsPlayoffs => StatisticsSet == StatisticsSet.Playoffs;

    /// <summary>
    /// Shows the chosen totals. A team that missed the playoffs has none, so its playoff figures
    /// show dashes.
    /// </summary>
    public override void Refresh()
    {
        OnPropertyChanged(nameof(HasPlayoffStatistics));
        StatisticsSet = StatisticsSet.OrRegularSeason(HasPlayoffStatistics);

        var season = _session.Snapshot.Season;
        var teamId = _teamId;
        var teams = ShowsPlayoffs ? season.Playoffs?.TeamStatistics ?? [] : season.TeamStatistics;
        Statistics = TeamStatisticsDisplay.Rows(
            teams.SingleOrDefault(statistics => statistics.TeamId == teamId) ?? TeamStatisticsDisplay.None(teamId));
    }

    [RelayCommand]
    private void ShowStatisticsSet(StatisticsSet statistics)
    {
        StatisticsSet = statistics.OrRegularSeason(HasPlayoffStatistics);
        Refresh();
    }
}