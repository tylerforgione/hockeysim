using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Inbox;
using HockeySim.Desktop.Players;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Inbox;

namespace HockeySim.Desktop.Home;

/// <summary>
/// The dashboard shown when a game opens: a consolidated view of the club, the inbox, the
/// division, and the current lineup, with shortcuts into the detailed pages.
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

    [ObservableProperty]
    private IReadOnlyList<SummaryTileViewModel> _tiles = [];

    [ObservableProperty]
    private IReadOnlyList<InboxPreviewViewModel> _inboxPreview = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DivisionTitle))]
    private string _divisionName = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<StandingRowViewModel> _divisionStandings = [];

    [ObservableProperty]
    private IReadOnlyList<LineupSummaryRowViewModel> _lineupSummary = [];

    [ObservableProperty]
    private IReadOnlyList<RatingLeaderViewModel> _ratingLeaders = [];

    public HomePageViewModel(
        GameSession session,
        Action<ShellPage> navigate,
        Action<InboxMessageId> openMessage,
        Action<PlayerId> openPlayer)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(navigate);
        ArgumentNullException.ThrowIfNull(openMessage);
        ArgumentNullException.ThrowIfNull(openPlayer);

        _session = session;
        _navigate = navigate;
        _openMessage = openMessage;
        _openPlayer = openPlayer;
        Refresh();
    }

    public override string Title => "Home";

    public override string Subtitle => $"{_session.ManagedTeam.Name} · {DivisionName}";

    public string DivisionTitle => DivisionName.ToUpperInvariant();

    public bool HasUnreadMessages => _session.Snapshot.Inbox.Any(message => !message.IsRead);

    public override void Refresh()
    {
        var snapshot = _session.Snapshot;
        var team = _session.ManagedTeam;
        var (_, division) = _session.FindDivision(team);
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
        DivisionStandings = division.Teams
            .Select(divisionTeam => new StandingRowViewModel(divisionTeam.Name, divisionTeam.Id == team.Id))
            .ToList();

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
}

public sealed record SummaryTileViewModel(string Label, string Value, string Caption);

public sealed record StandingRowViewModel(string TeamName, bool IsManaged)
{
    // Every team starts the season without results; standings calculation arrives with the schedule.
    public int GamesPlayed => 0;

    public int Wins => 0;

    public int Losses => 0;

    public int OvertimeLosses => 0;

    public int Points => 0;
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