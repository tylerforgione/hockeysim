using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Playoffs;
using HockeySim.Desktop.Schedule;
using HockeySim.Domain;

namespace HockeySim.Desktop.Home;

/// <summary>
/// The managed team's next matches in any phase, each with its opponent's record or, in the
/// playoffs, the series game. A row opens the match on the schedule.
/// </summary>
public sealed partial class UpcomingGamesPanelViewModel : ObservableObject
{
    private const int MatchCount = 7;

    private readonly GameSession _session;
    private readonly Action<ShellPage> _navigate;
    private readonly Action<DateOnly, TeamId> _openMatch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows))]
    private IReadOnlyList<UpcomingGameRowViewModel> _rows = [];

    [ObservableProperty]
    private string _emptyText = string.Empty;

    public UpcomingGamesPanelViewModel(GameSession session, Action<ShellPage> navigate, Action<DateOnly, TeamId> openMatch)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(navigate);
        ArgumentNullException.ThrowIfNull(openMatch);

        _session = session;
        _navigate = navigate;
        _openMatch = openMatch;
        Refresh();
    }

    public bool HasRows => Rows.Count > 0;

    public void Refresh()
    {
        var snapshot = _session.Snapshot;
        var teamId = snapshot.ManagedTeamId;
        var records = snapshot.Season.TeamRecords.ToDictionary(record => record.TeamId);
        Rows = MatchDisplay.UpcomingMatches(snapshot, teamId)
            .Take(MatchCount)
            .Select(upcoming =>
            {
                var (match, phase) = upcoming;
                var isHome = match.HomeTeamId == teamId;
                var opponentId = isHome ? match.AwayTeamId : match.HomeTeamId;
                var detail = phase == SeasonPhase.Playoffs
                    ? PlayoffDisplay.FindGame(snapshot.Season.Playoffs, match.Date, teamId) is { } game
                        ? $"{PlayoffDisplay.RoundAbbreviation(game.Series.Round)} G{game.GameNumber}"
                        : string.Empty
                    : MatchDisplay.Record(records[opponentId]);
                return new UpcomingGameRowViewModel(
                    MatchDisplay.ShortDate(match.Date),
                    isHome ? "vs" : "@",
                    _session.GetTeam(opponentId).Name,
                    detail,
                    phase == SeasonPhase.Preseason ? "PRE" : string.Empty,
                    new RelayCommand(() => _openMatch(match.Date, teamId)));
            })
            .ToList();
        EmptyText = snapshot.Season.IsComplete ? "Season complete" : "No matches scheduled";
    }

    [RelayCommand]
    private void OpenSchedule()
    {
        _navigate(ShellPage.TeamSchedule);
    }
}

/// <param name="Venue">"vs" for a home match, "@" for an away match.</param>
/// <param name="Detail">The opponent's record, or the playoff round and game ("R1 G3").</param>
/// <param name="Phase">"PRE" for a preseason match, otherwise empty.</param>
public sealed record UpcomingGameRowViewModel(
    string Date,
    string Venue,
    string Opponent,
    string Detail,
    string Phase,
    System.Windows.Input.ICommand OpenCommand);