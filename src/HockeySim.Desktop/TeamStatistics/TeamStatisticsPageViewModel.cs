using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;

namespace HockeySim.Desktop.TeamStatistics;

/// <summary>The managed team's current-season special teams, faceoffs, and five-on-five shares.</summary>
public sealed partial class TeamStatisticsPageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;

    [ObservableProperty]
    private IReadOnlyList<SeasonStatViewModel> _statistics = [];

    public TeamStatisticsPageViewModel(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        Refresh();
    }

    public override string Subtitle => _session.ManagedTeam.Name;

    public override void Refresh()
    {
        var teamId = _session.Snapshot.ManagedTeamId;
        Statistics = TeamStatisticsDisplay.Rows(
            _session.Snapshot.Season.TeamStatistics.Single(statistics => statistics.TeamId == teamId));
    }
}