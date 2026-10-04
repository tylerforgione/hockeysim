using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Teams;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Schedule;

/// <summary>
/// One team's regular-season schedule with its results, defaulting to the managed team. Selecting
/// a completed match shows its box score.
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

    public override string Subtitle
    {
        get
        {
            var played = Matches.Count(match => match.IsCompleted);
            return $"{SelectedTeam.Name} · {played} of {Matches.Count} played";
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

    private List<ScheduleMatchRowViewModel> CreateRows(TeamId teamId)
    {
        var season = _session.Snapshot.Season;
        var results = season.Results
            .Where(result => result.Home.TeamId == teamId || result.Away.TeamId == teamId)
            .ToDictionary(result => result.Date);
        var nextDate = season.IsComplete
            ? (DateOnly?)null
            : _session.Snapshot.Schedule.Matches
                .Where(match => match.Date >= season.CurrentDate && (match.HomeTeamId == teamId || match.AwayTeamId == teamId))
                .Select(match => (DateOnly?)match.Date)
                .FirstOrDefault();

        return _session.Snapshot.Schedule.Matches
            .Where(match => match.HomeTeamId == teamId || match.AwayTeamId == teamId)
            .Select(match =>
            {
                var isHome = match.HomeTeamId == teamId;
                var opponent = _session.GetTeam(isHome ? match.AwayTeamId : match.HomeTeamId);
                results.TryGetValue(match.Date, out var result);
                return new ScheduleMatchRowViewModel(
                    match.Date,
                    match.HomeTeamId,
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

    private CompletedMatchSnapshot? FindResult(DateOnly date, TeamId homeTeamId) =>
        _session.Snapshot.Season.Results.FirstOrDefault(result => result.Date == date && result.Home.TeamId == homeTeamId);
}

/// <param name="Venue">"vs" for a home match, "@" for an away match.</param>
/// <param name="Result">The result from the listed team's side, or empty before the match is played.</param>
/// <param name="IsNext">Whether this is the team's next match to be played.</param>
public sealed record ScheduleMatchRowViewModel(
    DateOnly Date,
    TeamId HomeTeamId,
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
}