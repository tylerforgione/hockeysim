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
    private readonly Action<TeamId>? _openTeam;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows))]
    private IReadOnlyList<RecentResultRowViewModel> _rows = [];

    /// <param name="openTeam">Opens an opponent's page; opponents are plain text without it.</param>
    public RecentResultsPanelViewModel(GameSession session, Action<DateOnly, TeamId> openMatch, Action<TeamId>? openTeam = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(openMatch);

        _session = session;
        _openMatch = openMatch;
        _openTeam = openTeam;
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
                var opponent = _session.GetTeam(isHome ? result.Away.TeamId : result.Home.TeamId);
                return new RecentResultRowViewModel(
                    MatchDisplay.ShortDate(result.Date),
                    isHome ? "vs" : "@",
                    opponent.Name,
                    MatchDisplay.ResultFor(result, teamId),
                    result.WinnerId == teamId,
                    new RelayCommand(() => _openMatch(result.Date, teamId)))
                {
                    OpenOpponentCommand = NameLinks.Open(_openTeam, opponent.Id),
                };
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
    /// <summary>Opens the opponent's team page; absent where the name links nowhere.</summary>
    public System.Windows.Input.ICommand? OpenOpponentCommand { get; init; }

    public bool IsLoss => !IsWin;
}