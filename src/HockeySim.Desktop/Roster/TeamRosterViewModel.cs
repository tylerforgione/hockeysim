using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Roster;

/// <summary>
/// A read-only roster split into skaters and goalies, with the team's season statistics and the
/// selected player's profile. The tables show ratings, basic season totals, or advanced
/// five-on-five figures; they do not fit side by side. Once the playoffs start, the totals, the
/// team strip, and the profile switch together between the regular season and the playoffs.
/// </summary>
public sealed partial class TeamRosterViewModel : ObservableObject
{
    private readonly TeamSnapshot _team;
    private readonly GameSession _session;

    [ObservableProperty]
    private IReadOnlyList<PlayerRowViewModel> _skaters = [];

    [ObservableProperty]
    private IReadOnlyList<PlayerRowViewModel> _goalies = [];

    [ObservableProperty]
    private IReadOnlyList<SeasonStatViewModel> _teamStatistics = [];

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
    [NotifyPropertyChangedFor(nameof(ShowsRegularSeason), nameof(ShowsPlayoffs), nameof(TeamStatisticsDescription))]
    private StatisticsSet _statistics;

    /// <param name="statistics">
    /// The totals to show; the regular season's until the playoffs start, whatever is asked.
    /// </param>
    public TeamRosterViewModel(
        GameSession session,
        TeamId teamId,
        PlayerId? initiallySelectedPlayerId = null,
        RosterColumns columns = RosterColumns.Ratings,
        StatisticsSet statistics = StatisticsSet.RegularSeason)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _team = session.GetTeam(teamId);
        _columns = columns;
        _statistics = HasPlayoffStatistics ? statistics : StatisticsSet.RegularSeason;

        // Players who cannot play come first, then those playing hurt; each in roster order.
        InjuryReport = _team.Roster
            .SelectMany(player => player.Injuries.Select(injury => (Player: player, Injury: injury)))
            .OrderBy(entry => entry.Injury.CanPlayThrough)
            .Select(entry => new InjuryReportRowViewModel(entry.Player, entry.Injury))
            .ToList();

        ShowStatistics();
        SelectPlayer(initiallySelectedPlayerId is { } id && _team.Roster.Any(player => player.Id == id)
            ? id
            : Skaters.Concat(Goalies).First().Player.Id);
    }

    /// <summary>Every injury on the team that has not healed: who is out, who is playing hurt.</summary>
    public IReadOnlyList<InjuryReportRowViewModel> InjuryReport { get; }

    public bool HasInjuries => InjuryReport.Count > 0;

    /// <summary>Whether the playoffs have started, so there are playoff statistics to switch to.</summary>
    public bool HasPlayoffStatistics => _session.Snapshot.Season.Playoffs is not null;

    public bool ShowsRegularSeason => Statistics == StatisticsSet.RegularSeason;

    public bool ShowsPlayoffs => Statistics == StatisticsSet.Playoffs;

    public string TeamStatisticsDescription => ShowsPlayoffs
        ? "Playoff special teams, faceoffs, and 5-on-5 shares"
        : "Regular-season special teams, faceoffs, and 5-on-5 shares";

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

    /// <summary>
    /// Rebuilds the tables and team strip from the chosen totals. A team that missed the playoffs
    /// has none, so its playoff strip shows dashes.
    /// </summary>
    private void ShowStatistics()
    {
        var rows = _team.Roster
            .OrderBy(player => player.Position)
            .ThenBy(player => player.LastName, StringComparer.Ordinal)
            .Select(player => new PlayerRowViewModel(player, _team.Lineup, _session.GetSeasonTotals(player.Id, Statistics)))
            .ToList();
        SelectedSkater = null;
        SelectedGoalie = null;
        Skaters = rows.Where(row => row.Player.Position != Position.Goalie).ToList();
        Goalies = rows.Where(row => row.Player.Position == Position.Goalie).ToList();

        var season = _session.Snapshot.Season;
        var teamStatistics = ShowsPlayoffs ? season.Playoffs?.TeamStatistics ?? [] : season.TeamStatistics;
        TeamStatistics = TeamStatisticsDisplay.Strip(
            teamStatistics.SingleOrDefault(statistics => statistics.TeamId == _team.Id) ?? TeamStatisticsDisplay.None(_team.Id));
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