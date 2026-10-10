using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Playoffs;
using HockeySim.Desktop.Teams;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Schedule;

/// <summary>
/// One team's matches in every phase with their results, defaulting to the managed team: its
/// preseason, its regular season, and its playoff games as they are scheduled. Each row is
/// labelled by phase. Selecting a completed match shows its box score.
/// </summary>
public sealed partial class SchedulePageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private TeamEntryViewModel _selectedTeam;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private IReadOnlyList<ScheduleMatchRowViewModel> _matches = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionHint))]
    private ScheduleMatchRowViewModel? _selectedMatch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedResult))]
    private MatchDetailViewModel? _selectedResult;

    public SchedulePageViewModel(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        Teams = session.Snapshot.League.Conferences
            .SelectMany(conference => conference.Divisions.Select(division => (conference, division)))
            .SelectMany(pair => pair.division.Teams.Select(team => new TeamEntryViewModel(
                team.Id,
                team.Name,
                $"{pair.division.Name} · {pair.conference.Name}",
                team.Id == session.Snapshot.ManagedTeamId)))
            .ToList();
        _selectedTeam = Teams.Single(team => team.IsManaged);
        Refresh();
    }

    public override string Title => "Schedule";

    /// <summary>
    /// Counts the regular season's matches played; the preseason and playoffs are listed but
    /// vary in length.
    /// </summary>
    public override string Subtitle
    {
        get
        {
            var regularSeason = Matches.Where(match => match.Phase == SeasonPhase.RegularSeason).ToList();
            var played = regularSeason.Count(match => match.IsCompleted);
            return $"{SelectedTeam.Name} · {played} of {regularSeason.Count} regular-season matches played";
        }
    }

    /// <summary>
    /// Gets every team in league order: by conference, then division.
    /// </summary>
    public IReadOnlyList<TeamEntryViewModel> Teams { get; }

    public bool HasSelectedResult => SelectedResult is not null;

    /// <summary>
    /// Explains the empty detail pane: nothing selected, or a match not yet played.
    /// </summary>
    public string SelectionHint => SelectedMatch is null
        ? "Select a completed match to see its box score."
        : $"{SelectedMatch.Matchup} is scheduled for {MatchDisplay.LongDate(SelectedMatch.Date)}.";

    /// <summary>
    /// Shows one match, switching to the given team's schedule. The team must play in the match.
    /// </summary>
    public void OpenMatch(DateOnly date, TeamId teamId)
    {
        SelectedTeam = Teams.Single(team => team.Id == teamId);
        SelectedMatch = Matches.Single(match => match.Date == date);
    }

    public override void Refresh()
    {
        var selectedDate = SelectedMatch?.Date;
        Matches = CreateRows(SelectedTeam.Id);
        SelectedMatch = Matches.FirstOrDefault(match => match.Date == selectedDate);
    }

    partial void OnSelectedTeamChanged(TeamEntryViewModel value)
    {
        Matches = CreateRows(value.Id);
        SelectedMatch = null;
    }

    partial void OnSelectedMatchChanged(ScheduleMatchRowViewModel? value)
    {
        var result = value is null ? null : FindResult(value.Date, value.HomeTeamId);
        SelectedResult = result is null
            ? null
            : new MatchDetailViewModel(result, _session.PlayersById, teamId => _session.GetTeam(teamId).Name);
    }

    /// <summary>
    /// Lists the team's matches in date order. The phases follow one another and a team plays at
    /// most once a day, so a date identifies a team's match in any phase.
    /// </summary>
    private List<ScheduleMatchRowViewModel> CreateRows(TeamId teamId)
    {
        var snapshot = _session.Snapshot;
        var season = snapshot.Season;
        var results = AllResults()
            .Where(result => result.Home.TeamId == teamId || result.Away.TeamId == teamId)
            .ToDictionary(result => result.Date);
        var matches = snapshot.Schedule.PreseasonMatches.Select(match => (Match: match, Phase: SeasonPhase.Preseason))
            .Concat(snapshot.Schedule.Matches.Select(match => (Match: match, Phase: SeasonPhase.RegularSeason)))
            .Concat(snapshot.Schedule.PlayoffMatches.Select(match => (Match: match, Phase: SeasonPhase.Playoffs)))
            .Where(entry => entry.Match.HomeTeamId == teamId || entry.Match.AwayTeamId == teamId)
            .ToList();
        var nextDate = season.IsComplete
            ? (DateOnly?)null
            : matches
                .Where(entry => entry.Match.Date >= season.CurrentDate)
                .Select(entry => (DateOnly?)entry.Match.Date)
                .FirstOrDefault();

        return matches
            .Select(entry =>
            {
                var match = entry.Match;
                var isHome = match.HomeTeamId == teamId;
                var opponent = _session.GetTeam(isHome ? match.AwayTeamId : match.HomeTeamId);
                results.TryGetValue(match.Date, out var result);
                var (phaseLabel, phaseDescription) = PhaseOf(entry.Phase, match.Date, teamId);
                return new ScheduleMatchRowViewModel(
                    match.Date,
                    match.HomeTeamId,
                    entry.Phase,
                    phaseLabel,
                    phaseDescription,
                    MatchDisplay.ShortDate(match.Date),
                    isHome ? "vs" : "@",
                    opponent.Name,
                    $"{_session.GetTeam(match.AwayTeamId).Name} @ {_session.GetTeam(match.HomeTeamId).Name}",
                    result is null ? string.Empty : MatchDisplay.ResultFor(result, teamId),
                    result is not null && result.WinnerId == teamId,
                    result is not null,
                    match.Date == nextDate);
            })
            .ToList();
    }

    /// <summary>Labels a row's phase, naming a playoff game's round and number in its series.</summary>
    private (string Label, string Description) PhaseOf(SeasonPhase phase, DateOnly date, TeamId teamId)
    {
        switch (phase)
        {
            case SeasonPhase.Preseason:
                return ("PRE", "Preseason: counts toward nothing");
            case SeasonPhase.RegularSeason:
                return ("REG", "Regular season");
            default:
                var (series, gameNumber) = PlayoffDisplay.FindGame(_session.Snapshot.Season.Playoffs, date, teamId)
                    ?? throw new InvalidOperationException($"No playoff series holds the game on {date}.");
                return (
                    $"{PlayoffDisplay.RoundAbbreviation(series.Round)} G{gameNumber}",
                    $"Playoffs: {PlayoffDisplay.RoundName(series.Round)}, game {gameNumber}");
        }
    }

    private CompletedMatchSnapshot? FindResult(DateOnly date, TeamId homeTeamId) =>
        AllResults().FirstOrDefault(result => result.Date == date && result.Home.TeamId == homeTeamId);

    private IEnumerable<CompletedMatchSnapshot> AllResults()
    {
        var season = _session.Snapshot.Season;
        return season.PreseasonResults.Concat(season.Results).Concat(season.Playoffs?.Results ?? []);
    }
}

/// <param name="PhaseLabel">"PRE", "REG", or a playoff game's round and number, such as "R1 G3".</param>
/// <param name="Venue">"vs" for a home match, "@" for an away match.</param>
/// <param name="Result">The result from the listed team's side, or empty before the match is played.</param>
/// <param name="IsNext">Whether this is the team's next match to be played.</param>
public sealed record ScheduleMatchRowViewModel(
    DateOnly Date,
    TeamId HomeTeamId,
    SeasonPhase Phase,
    string PhaseLabel,
    string PhaseDescription,
    string DateLabel,
    string Venue,
    string Opponent,
    string Matchup,
    string Result,
    bool IsWin,
    bool IsCompleted,
    bool IsNext)
{
    public bool IsLoss => IsCompleted && !IsWin;

    public bool IsPlayoff => Phase == SeasonPhase.Playoffs;
}