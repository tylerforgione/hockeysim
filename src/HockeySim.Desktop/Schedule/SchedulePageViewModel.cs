using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Playoffs;
using HockeySim.Desktop.Tables;
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

    /// <summary>The matches table's sort, or none for date order. It holds across teams and days.</summary>
    [ObservableProperty]
    private TableSort? _matchesSort;

    // The box score's sort, which the next box score opened starts with.
    private TableSort? _boxScoreSort;

    public SchedulePageViewModel(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        Teams = TeamEntryViewModel.ForLeague(session.Snapshot);
        _selectedTeam = Teams.Single(team => team.IsManaged);
        Refresh();
    }

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
    /// Fills the empty detail pane: blank with nothing selected, or a match not yet played.
    /// </summary>
    public string SelectionHint => SelectedMatch is null
        ? string.Empty
        : $"{SelectedMatch.Matchup} · {MatchDisplay.LongDate(SelectedMatch.Date)}";

    /// <summary>Shows a team's schedule, keeping the selected match when it is already shown.</summary>
    public void ShowTeam(TeamId teamId)
    {
        SelectedTeam = Teams.Single(team => team.Id == teamId);
    }

    /// <summary>
    /// Shows one match, switching to the given team's schedule. The team must play in the match.
    /// </summary>
    public void OpenMatch(DateOnly date, TeamId teamId)
    {
        SelectedTeam = Teams.Single(team => team.Id == teamId);
        SelectedMatch = Matches.Single(match => match.Date == date);
    }

    /// <summary>Sorts the matches by a column, or reverses the sort when it is already sorted by it.</summary>
    [RelayCommand]
    private void SortMatches(string column)
    {
        MatchesSort = TableSorting.Click(ScheduleTableColumns.Matches, MatchesSort, column);
        var selectedDate = SelectedMatch?.Date;
        Matches = SortedRows(Matches);
        SelectedMatch = Matches.FirstOrDefault(match => match.Date == selectedDate);
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
        if (result is null)
        {
            SelectedResult = null;
            return;
        }

        var detail = new MatchDetailViewModel(result, _session.PlayersById, _session.GetTeam, _boxScoreSort);
        detail.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(MatchDetailViewModel.SkaterSort))
            {
                _boxScoreSort = detail.SkaterSort;
            }
        };
        SelectedResult = detail;
    }

    /// <summary>Puts rows (in any order) into date order, then the table's sort.</summary>
    private List<ScheduleMatchRowViewModel> SortedRows(IEnumerable<ScheduleMatchRowViewModel> rows) =>
        TableSorting.Apply(rows.OrderBy(row => row.Date), ScheduleTableColumns.Matches, MatchesSort);

    /// <summary>
    /// Lists the team's matches in date order, then the table's sort. The phases follow one another and a team plays at
    /// most once a day, so a date identifies a team's match in any phase.
    /// </summary>
    private List<ScheduleMatchRowViewModel> CreateRows(TeamId teamId)
    {
        var snapshot = _session.Snapshot;
        var season = snapshot.Season;
        var results = AllResults()
            .Where(result => TeamResults.Involves(result, teamId))
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

        return SortedRows(matches
            .Select(entry =>
            {
                var match = entry.Match;
                var isHome = match.HomeTeamId == teamId;
                var opponent = _session.GetTeam(isHome ? match.AwayTeamId : match.HomeTeamId);
                results.TryGetValue(match.Date, out var result);
                var (phaseLabel, phaseDescription) = MatchDisplay.PhaseOf(entry.Phase, season.Playoffs, match.Date, teamId);
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
                    match.Date == nextDate,
                    result is null ? null : GoalDifferential(result, teamId));
            }));
    }

    private static int GoalDifferential(CompletedMatchSnapshot result, TeamId teamId) =>
        result.Home.TeamId == teamId
            ? result.Home.Score - result.Away.Score
            : result.Away.Score - result.Home.Score;

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
/// <param name="GoalDifferential">The listed team's goals for less against, or null before the match is played.</param>
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
    bool IsNext,
    int? GoalDifferential)
{
    public bool IsLoss => IsCompleted && !IsWin;

    public bool IsPlayoff => Phase == SeasonPhase.Playoffs;
}