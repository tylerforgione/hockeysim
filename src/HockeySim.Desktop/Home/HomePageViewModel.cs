using HockeySim.Desktop.Game;
using HockeySim.Domain;

namespace HockeySim.Desktop.Home;

/// <summary>
/// The page shown when a game opens, built around the managed team's next match: its upcoming
/// games, the standings, the next opponent compared, its latest results, and its leaders. Each
/// panel is self-contained: it reads the session, takes only the shortcuts it needs, and
/// refreshes itself, so a panel can be hosted elsewhere.
/// </summary>
public sealed class HomePageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;

    /// <param name="navigate">Opens a page.</param>
    /// <param name="openPlayer">Opens a player.</param>
    /// <param name="openTeam">Opens a team.</param>
    /// <param name="openMatch">Opens a match, played or not, on the schedule of the team that plays in it.</param>
    public HomePageViewModel(
        GameSession session,
        Action<ShellPage> navigate,
        Action<PlayerId> openPlayer,
        Action<TeamId> openTeam,
        Action<DateOnly, TeamId> openMatch)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(navigate);
        ArgumentNullException.ThrowIfNull(openPlayer);
        ArgumentNullException.ThrowIfNull(openTeam);
        ArgumentNullException.ThrowIfNull(openMatch);

        _session = session;
        UpcomingGames = new UpcomingGamesPanelViewModel(session, navigate, openMatch, openTeam);
        Standings = new StandingsPanelViewModel(session, navigate, openTeam);
        TaleOfTheTape = new TaleOfTheTapePanelViewModel(session, navigate, openPlayer, openTeam);
        RecentResults = new RecentResultsPanelViewModel(session, openMatch, openTeam);
        TeamLeaders = new TeamLeadersPanelViewModel(session, openPlayer);
    }

    public UpcomingGamesPanelViewModel UpcomingGames { get; }

    public StandingsPanelViewModel Standings { get; }

    public TaleOfTheTapePanelViewModel TaleOfTheTape { get; }

    public RecentResultsPanelViewModel RecentResults { get; }

    public TeamLeadersPanelViewModel TeamLeaders { get; }

    public override string Subtitle => $"{_session.ManagedTeam.Name} · {_session.FindDivision(_session.ManagedTeam).Division.Name}";

    public override void Refresh()
    {
        UpcomingGames.Refresh();
        Standings.Refresh();
        TaleOfTheTape.Refresh();
        RecentResults.Refresh();
        TeamLeaders.Refresh();
        OnPropertyChanged(nameof(Subtitle));
    }
}