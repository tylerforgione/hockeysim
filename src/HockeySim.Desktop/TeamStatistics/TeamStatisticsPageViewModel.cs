using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;

namespace HockeySim.Desktop.TeamStatistics;

/// <summary>
/// The managed team's special teams, faceoffs, and five-on-five shares. Once the playoffs start,
/// the page switches between the regular season's and the playoffs'.
/// </summary>
public sealed partial class TeamStatisticsPageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;

    [ObservableProperty]
    private IReadOnlyList<SeasonStatViewModel> _statistics = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRegularSeason), nameof(ShowsPlayoffs))]
    private StatisticsSet _statisticsSet;

    public TeamStatisticsPageViewModel(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        Refresh();
    }

    public override string Subtitle => _session.ManagedTeam.Name;

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
        if (!HasPlayoffStatistics)
        {
            StatisticsSet = StatisticsSet.RegularSeason;
        }

        var season = _session.Snapshot.Season;
        var teamId = _session.Snapshot.ManagedTeamId;
        var teams = ShowsPlayoffs ? season.Playoffs?.TeamStatistics ?? [] : season.TeamStatistics;
        Statistics = TeamStatisticsDisplay.Rows(
            teams.SingleOrDefault(statistics => statistics.TeamId == teamId) ?? TeamStatisticsDisplay.None(teamId));
    }

    [RelayCommand]
    private void ShowStatisticsSet(StatisticsSet statistics)
    {
        StatisticsSet = HasPlayoffStatistics ? statistics : StatisticsSet.RegularSeason;
        Refresh();
    }
}