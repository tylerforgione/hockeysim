using System.Windows.Input;

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
public sealed partial class SchedulePageViewModel : ShellPageViewModel, ISelectablePage
{
    private readonly GameSession _session;
    private readonly Action<TeamId>? _openTeam;
    private readonly Action<PlayerId>? _openPlayer;

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

    /// <param name="openTeam">Opens a team's page from an opponent or box score team; names are plain text without it.</param>
    /// <param name="openPlayer">Opens a player's profile from the box score; names are plain text without it.</param>
    public SchedulePageViewModel(GameSession session, Action<TeamId>? openTeam = null, Action<PlayerId>? openPlayer = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _openTeam = openTeam;
        _openPlayer = openPlayer;
        Teams = TeamEntryViewModel.ForLeague(session.Snapshot);
        _selectedTeam = Teams.Single(team => team.IsManaged);
        Refresh();
    }

    public event EventHandler? SelectionChanged;

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

    object ISelectablePage.Selection => new ScheduleSelection(SelectedTeam.Id, SelectedMatch?.Date);

    /// <summary>
    /// Shows a team's schedule and, if it still has one, the match on the date. A team that no longer
    /// plays on that date shows no match.
    /// </summary>
    void ISelectablePage.Select(object selection)
    {
        var (teamId, date) = selection as ScheduleSelection ?? new ScheduleSelection(_session.Snapshot.ManagedTeamId, null);
        SelectedTeam = Teams.FirstOrDefault(team => team.Id == teamId) ?? Teams.Single(team => team.IsManaged);
        SelectedMatch = Matches.FirstOrDefault(match => match.Date == date);
    }

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
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedMatchChanged(ScheduleMatchRowViewModel? value)
    {
        ShowResultOf(value);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShowResultOf(ScheduleMatchRowViewModel? value)
    {
        var result = value is null ? null : FindResult(value.Date, value.HomeTeamId);
        if (result is null)
        {
            SelectedResult = null;
            return;
        }

        var detail = new MatchDetailViewModel(result, _session.PlayersById, _session.GetTeam, _boxScoreSort, _openPlayer, _openTeam);
        detail.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(MatchDetailViewModel.SkaterSort))
            {
                _boxScoreSort = detail.SkaterSort;
            }
        };
        SelectedResult = detail;
    }

    private List<ScheduleMatchRowViewModel> CreateRows(TeamId teamId) =>
        ScheduleRows.Create(_session, teamId, MatchesSort, _openTeam);

    private List<ScheduleMatchRowViewModel> SortedRows(IEnumerable<ScheduleMatchRowViewModel> rows) =>
        ScheduleRows.Sorted(rows, MatchesSort);

    private CompletedMatchSnapshot? FindResult(DateOnly date, TeamId homeTeamId) =>
        AllResults().FirstOrDefault(result => result.Date == date && result.Home.TeamId == homeTeamId);

    private IEnumerable<CompletedMatchSnapshot> AllResults() => ScheduleRows.AllResults(_session.Snapshot);
}

/// <summary>The schedule's history selection: a team and the date of the match chosen, if any.</summary>
public sealed record ScheduleSelection(TeamId Team, DateOnly? Match);

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
    /// <summary>Opens the opponent's team page; absent where the name links nowhere.</summary>
    public ICommand? OpenOpponentCommand { get; init; }

    public bool IsLoss => IsCompleted && !IsWin;

    public bool IsPlayoff => Phase == SeasonPhase.Playoffs;
}