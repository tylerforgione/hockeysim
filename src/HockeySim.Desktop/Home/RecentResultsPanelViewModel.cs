using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Schedule;
using HockeySim.Domain;

namespace HockeySim.Desktop.Home;

/// <summary>
/// The managed team's latest results in any phase, newest first, each opening its box score.
/// </summary>
public sealed partial class RecentResultsPanelViewModel : ObservableObject
{
    private const int ResultCount = 6;

    private readonly GameSession _session;
    private readonly Action<DateOnly, TeamId> _openMatch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows))]
    private IReadOnlyList<RecentResultRowViewModel> _rows = [];

    public RecentResultsPanelViewModel(GameSession session, Action<DateOnly, TeamId> openMatch)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(openMatch);

        _session = session;
        _openMatch = openMatch;
        Refresh();
    }

    public bool HasRows => Rows.Count > 0;

    public string EmptyText => "No results";

    public void Refresh()
    {
        var season = _session.Snapshot.Season;
        var teamId = _session.Snapshot.ManagedTeamId;
        Rows = season.PreseasonResults
            .Concat(season.Results)
            .Concat(season.Playoffs?.Results ?? [])
            .Where(result => TeamResults.Involves(result, teamId))
            .TakeLast(ResultCount)
            .Reverse()
            .Select(result =>
            {
                var isHome = result.Home.TeamId == teamId;
                return new RecentResultRowViewModel(
                    MatchDisplay.ShortDate(result.Date),
                    isHome ? "vs" : "@",
                    _session.GetTeam(isHome ? result.Away.TeamId : result.Home.TeamId).Name,
                    MatchDisplay.ResultFor(result, teamId),
                    result.WinnerId == teamId,
                    new RelayCommand(() => _openMatch(result.Date, teamId)));
            })
            .ToList();
    }
}

/// <param name="Venue">"vs" for a home match, "@" for an away match.</param>
/// <param name="Result">The result from the managed team's side, such as "W 4–2" or "L 2–3 OT".</param>
public sealed record RecentResultRowViewModel(
    string Date,
    string Venue,
    string Opponent,
    string Result,
    bool IsWin,
    System.Windows.Input.ICommand OpenCommand)
{
    public bool IsLoss => !IsWin;
}