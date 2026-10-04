using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Inbox;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.Standings;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Inbox;

namespace HockeySim.Desktop.Home;

/// <summary>
/// The dashboard shown when a game opens: a consolidated view of the club, the inbox, the
/// division, the next match, the latest league results, and the current lineup, with shortcuts
/// into the detailed pages.
/// </summary>
public sealed partial class HomePageViewModel : ShellPageViewModel
{
    private const int InboxPreviewCount = 5;

    private static readonly Rating[] LeaderRatings =
    [
        Rating.Skating,
        Rating.ShotAccuracy,
        Rating.Passing,
        Rating.DefensiveAwareness,
        Rating.Checking,
        Rating.GoalieReflex,
    ];

    private readonly GameSession _session;
    private readonly Action<ShellPage> _navigate;
    private readonly Action<InboxMessageId> _openMessage;
    private readonly Action<PlayerId> _openPlayer;
    private readonly Action<DateOnly, TeamId> _openMatch;

    [ObservableProperty]
    private IReadOnlyList<SummaryTileViewModel> _tiles = [];

    [ObservableProperty]
    private IReadOnlyList<InboxPreviewViewModel> _inboxPreview = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DivisionTitle))]
    private string _divisionName = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<StandingsRowViewModel> _divisionStandings = [];

    [ObservableProperty]
    private IReadOnlyList<LineupSummaryRowViewModel> _lineupSummary = [];

    [ObservableProperty]
    private IReadOnlyList<RatingLeaderViewModel> _ratingLeaders = [];

    [ObservableProperty]
    private string _nextMatchTitle = string.Empty;

    [ObservableProperty]
    private string _nextMatchCaption = string.Empty;

    [ObservableProperty]
    private string _lastResultCaption = string.Empty;

    [ObservableProperty]
    private string _latestResultsTitle = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLatestResults))]
    private IReadOnlyList<LeagueResultRowViewModel> _latestResults = [];

    [ObservableProperty]
    private string _latestResultsCaption = string.Empty;

    public HomePageViewModel(
        GameSession session,
        Action<ShellPage> navigate,
        Action<InboxMessageId> openMessage,
        Action<PlayerId> openPlayer,
        Action<DateOnly, TeamId> openMatch)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(navigate);
        ArgumentNullException.ThrowIfNull(openMessage);
        ArgumentNullException.ThrowIfNull(openPlayer);
        ArgumentNullException.ThrowIfNull(openMatch);

        _session = session;
        _navigate = navigate;
        _openMessage = openMessage;
        _openPlayer = openPlayer;
        _openMatch = openMatch;
        Refresh();
    }

    public override string Title => "Home";

    public override string Subtitle => $"{_session.ManagedTeam.Name} · {DivisionName}";

    public string DivisionTitle => DivisionName.ToUpperInvariant();

    public bool HasUnreadMessages => _session.Snapshot.Inbox.Any(message => !message.IsRead);

    public bool HasLatestResults => LatestResults.Count > 0;

    public override void Refresh()
    {
        var snapshot = _session.Snapshot;
        var team = _session.ManagedTeam;
        var (conference, division) = _session.FindDivision(team);
        var rosterById = team.Roster.ToDictionary(player => player.Id);
        var unread = snapshot.Inbox.Count(message => !message.IsRead);

        Tiles =
        [
            new("ROSTER", team.Roster.Count.ToString(CultureInfo.CurrentCulture), "players under contract"),
            new("DRESSED", team.Lineup.DressedPlayerIds.Count.ToString(CultureInfo.CurrentCulture), $"{team.ScratchedPlayerIds.Count} healthy scratches"),
            new("AVERAGE AGE", team.Roster.Average(player => player.Age).ToString("0.0", CultureInfo.CurrentCulture), "years"),
            new("INBOX", unread.ToString(CultureInfo.CurrentCulture), unread == 1 ? "unread message" : "unread messages"),
        ];

        InboxPreview = snapshot.Inbox
            .Take(InboxPreviewCount)
            .Select(message => new InboxPreviewViewModel(new InboxMessageViewModel(message), _openMessage))
            .ToList();

        DivisionName = division.Name;
        DivisionStandings = snapshot.Season.Standings.Conferences
            .Single(standings => standings.Name == conference.Name).Divisions
            .Single(standings => standings.Name == division.Name).Teams
            .Select(entry => StandingsRowViewModel.Create(entry, _session.GetTeam(entry.Record.TeamId).Name, entry.Record.TeamId == team.Id))
            .ToList();

        RefreshNextMatch(snapshot, team);
        RefreshLatestResults(snapshot, team);

        LineupSummary = team.Lineup.ForwardLines
            .Select((line, index) => new LineupSummaryRowViewModel(
                $"F{index + 1}",
                string.Join("  ·  ", new[] { line.LeftWingId, line.CentreId, line.RightWingId }
                    .Select(id => PlayerDisplay.ShortName(rosterById[id])))))
            .Concat(team.Lineup.DefencePairs.Select((pair, index) => new LineupSummaryRowViewModel(
                $"D{index + 1}",
                string.Join("  ·  ", new[] { pair.LeftDefenceId, pair.RightDefenceId }
                    .Select(id => PlayerDisplay.ShortName(rosterById[id]))))))
            .Append(new LineupSummaryRowViewModel(
                "G",
                $"{PlayerDisplay.ShortName(rosterById[team.Lineup.StartingGoalieId])}  ·  {PlayerDisplay.ShortName(rosterById[team.Lineup.BackupGoalieId])} (backup)"))
            .ToList();

        RatingLeaders = LeaderRatings
            .Select(rating =>
            {
                var candidates = team.Roster.Where(player =>
                    (player.Position == Position.Goalie) == (rating is Rating.GoalieReflex));
                var leader = candidates.MaxBy(player => player.Ratings[rating])!;
                return new RatingLeaderViewModel(
                    PlayerDisplay.RatingName(rating),
                    PlayerDisplay.FullName(leader),
                    PlayerDisplay.PositionAbbreviation(leader.Position),
                    leader.Ratings[rating],
                    leader.Id,
                    _openPlayer);
            })
            .ToList();

        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(HasUnreadMessages));
    }

    [RelayCommand]
    private void Navigate(ShellPage page)
    {
        _navigate(page);
    }

    private void RefreshNextMatch(GameSnapshot snapshot, TeamSnapshot team)
    {
        var season = snapshot.Season;
        var lastResult = season.Results.LastOrDefault(result => Involves(result, team.Id));
        LastResultCaption = lastResult is null
            ? string.Empty
            : $"Last: {MatchDisplay.ResultFor(lastResult, team.Id)} {Opponent(lastResult.Home.TeamId, lastResult.Away.TeamId, team.Id)} · {MatchDisplay.ShortDate(lastResult.Date)}";

        var next = season.IsComplete
            ? null
            : snapshot.Schedule.Matches.FirstOrDefault(match =>
                match.Date >= season.CurrentDate && (match.HomeTeamId == team.Id || match.AwayTeamId == team.Id));
        if (next is null)
        {
            NextMatchTitle = "Regular season complete";
            NextMatchCaption = "No further matches are scheduled.";
            return;
        }

        NextMatchTitle = Opponent(next.HomeTeamId, next.AwayTeamId, team.Id);
        var when = next.Date == season.CurrentDate ? "Today" : MatchDisplay.ShortDate(next.Date);
        NextMatchCaption = $"{when} · {(next.HomeTeamId == team.Id ? "Home" : "Away")}";
    }

    /// <summary>
    /// Shows the most recently played league day: the day before the current date. A day with no
    /// league matches says so, rather than repeating an older day's results.
    /// </summary>
    private void RefreshLatestResults(GameSnapshot snapshot, TeamSnapshot team)
    {
        var season = snapshot.Season;
        var openingDay = snapshot.Schedule.Matches[0].Date;
        if (season.CurrentDate <= openingDay)
        {
            LatestResultsTitle = "LEAGUE RESULTS";
            LatestResults = [];
            LatestResultsCaption = $"The regular season opens {MatchDisplay.LongDate(openingDay)}.";
            return;
        }

        var day = season.CurrentDate.AddDays(-1);
        LatestResultsTitle = $"LEAGUE RESULTS · {MatchDisplay.ShortDate(day).ToUpperInvariant()}";
        LatestResults = season.Results
            .Where(result => result.Date == day)
            .Select(result => new LeagueResultRowViewModel(
                _session.GetTeam(result.Away.TeamId).Name,
                result.Away.Score,
                _session.GetTeam(result.Home.TeamId).Name,
                result.Home.Score,
                MatchDisplay.DecisionSuffix(result.Decision),
                Involves(result, team.Id),
                () => _openMatch(result.Date, Involves(result, team.Id) ? team.Id : result.Home.TeamId)))
            .ToList();
        LatestResultsCaption = LatestResults.Count == 0
            ? $"No league matches were scheduled on {MatchDisplay.LongDate(day)}."
            : string.Empty;
    }

    private string Opponent(TeamId homeTeamId, TeamId awayTeamId, TeamId teamId) =>
        homeTeamId == teamId
            ? $"vs {_session.GetTeam(awayTeamId).Name}"
            : $"@ {_session.GetTeam(homeTeamId).Name}";

    private static bool Involves(CompletedMatchSnapshot result, TeamId teamId) =>
        result.Home.TeamId == teamId || result.Away.TeamId == teamId;
}

public sealed record SummaryTileViewModel(string Label, string Value, string Caption);

public sealed partial class LeagueResultRowViewModel
{
    private readonly Action _open;

    public LeagueResultRowViewModel(
        string awayTeamName,
        int awayScore,
        string homeTeamName,
        int homeScore,
        string decisionSuffix,
        bool involvesManagedTeam,
        Action open)
    {
        AwayTeamName = awayTeamName;
        AwayScore = awayScore;
        HomeTeamName = homeTeamName;
        HomeScore = homeScore;
        DecisionSuffix = decisionSuffix;
        InvolvesManagedTeam = involvesManagedTeam;
        _open = open;
    }

    public string AwayTeamName { get; }

    public int AwayScore { get; }

    public string HomeTeamName { get; }

    public int HomeScore { get; }

    /// <summary>"OT" or "SO", or empty for a regulation result.</summary>
    public string DecisionSuffix { get; }

    public bool InvolvesManagedTeam { get; }

    [RelayCommand]
    private void Open()
    {
        _open();
    }
}

public sealed record LineupSummaryRowViewModel(string Unit, string Players);

public sealed partial class InboxPreviewViewModel
{
    private readonly Action<InboxMessageId> _open;

    public InboxPreviewViewModel(InboxMessageViewModel message, Action<InboxMessageId> open)
    {
        Message = message;
        _open = open;
    }

    public InboxMessageViewModel Message { get; }

    [RelayCommand]
    private void Open()
    {
        _open(Message.Id);
    }
}

public sealed partial class RatingLeaderViewModel
{
    private readonly PlayerId _playerId;
    private readonly Action<PlayerId> _open;

    public RatingLeaderViewModel(
        string ratingName,
        string playerName,
        string position,
        int value,
        PlayerId playerId,
        Action<PlayerId> open)
    {
        RatingName = ratingName;
        PlayerName = playerName;
        Position = position;
        Value = value;
        _playerId = playerId;
        _open = open;
    }

    public string RatingName { get; }

    public string PlayerName { get; }

    public string Position { get; }

    public int Value { get; }

    [RelayCommand]
    private void Open()
    {
        _open(_playerId);
    }
}