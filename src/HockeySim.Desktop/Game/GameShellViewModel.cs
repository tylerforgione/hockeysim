using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Home;
using HockeySim.Desktop.Inbox;
using HockeySim.Desktop.Lines;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.Standings;
using HockeySim.Desktop.Teams;
using HockeySim.Domain;
using HockeySim.Management.Inbox;

namespace HockeySim.Desktop.Game;

/// <summary>
/// The in-game frame: team identity and time controls across the top, navigation down the side,
/// and the active page in the middle.
/// </summary>
public sealed partial class GameShellViewModel : ObservableObject
{
    private const string NotAvailableYet = "Not available yet";

    private readonly Action _showMainMenu;
    private readonly Dictionary<ShellPage, ShellPageViewModel> _pages;
    private readonly NavigationItemViewModel _inboxItem;

    [ObservableProperty]
    private ShellPageViewModel _currentPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAdvanceError))]
    private string? _advanceError;

    public GameShellViewModel(GameSession session, Action? showMainMenu = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        Session = session;
        _showMainMenu = showMainMenu ?? (() => { });

        Home = new HomePageViewModel(session, Navigate, OpenInboxMessage, OpenPlayer, OpenMatch);
        Inbox = new InboxPageViewModel(session);
        Roster = new RosterPageViewModel(session);
        Lines = new LinesPageViewModel(session);
        Teams = new TeamsPageViewModel(session);
        Standings = new StandingsPageViewModel(session);
        Schedule = new SchedulePageViewModel(session);
        _pages = new Dictionary<ShellPage, ShellPageViewModel>
        {
            [ShellPage.Home] = Home,
            [ShellPage.Inbox] = Inbox,
            [ShellPage.Roster] = Roster,
            [ShellPage.Lines] = Lines,
            [ShellPage.Teams] = Teams,
            [ShellPage.Standings] = Standings,
            [ShellPage.Schedule] = Schedule,
        };

        _inboxItem = new NavigationItemViewModel(ShellPage.Inbox, "Inbox", Navigate);
        NavigationSections =
        [
            new("CLUB",
            [
                new(ShellPage.Home, "Home", Navigate),
                _inboxItem,
                new(ShellPage.Roster, "Roster", Navigate),
                new(ShellPage.Lines, "Lines", Navigate),
            ]),
            new("LEAGUE",
            [
                new(ShellPage.Teams, "Teams", Navigate),
                new(ShellPage.Standings, "Standings", Navigate),
                new(ShellPage.Schedule, "Schedule", Navigate),
            ]),
            new("TRANSACTIONS",
            [
                new(ShellPage.FreeAgents, "Free Agents", Navigate, NotAvailableYet),
                new(ShellPage.Trades, "Trades", Navigate, NotAvailableYet),
            ]),
        ];

        _currentPage = Home;
        UpdateNavigationState();
        session.PropertyChanged += OnSessionChanged;
    }

    public GameSession Session { get; }

    public HomePageViewModel Home { get; }

    public InboxPageViewModel Inbox { get; }

    public RosterPageViewModel Roster { get; }

    public LinesPageViewModel Lines { get; }

    public TeamsPageViewModel Teams { get; }

    public StandingsPageViewModel Standings { get; }

    public SchedulePageViewModel Schedule { get; }

    public IReadOnlyList<NavigationSectionViewModel> NavigationSections { get; }

    public string TeamName => Session.ManagedTeam.Name;

    public string TeamInitials => PlayerDisplay.TeamInitials(TeamName);

    public string TeamDivision
    {
        get
        {
            var (conference, division) = Session.FindDivision(Session.ManagedTeam);
            return $"{division.Name} · {conference.Name}";
        }
    }

    public string SeasonLabel => $"{PlayerDisplay.FormatSeason(Session.Snapshot.League.SeasonYear)} Season";

    public string PhaseLabel => Session.Snapshot.Season.IsComplete
        ? "Regular season complete"
        : $"Regular season · {MatchDisplay.ShortDate(Session.Snapshot.Season.CurrentDate)}";

    public string ContinueLabel => Session.IsAdvancing ? "Playing…" : "Continue";

    /// <summary>
    /// Describes what continuing will do: which day is played, or why it cannot be.
    /// </summary>
    public string ContinueDescription
    {
        get
        {
            var season = Session.Snapshot.Season;
            if (season.IsComplete)
            {
                return "The regular season is complete. Results and rosters remain available.";
            }

            var date = MatchDisplay.ShortDate(season.CurrentDate);
            var matches = Session.Snapshot.Schedule.Matches.Where(match => match.Date == season.CurrentDate).ToList();
            if (matches.Count == 0)
            {
                return $"No league matches on {date}. Continue to the next day.";
            }

            var managedTeamId = Session.Snapshot.ManagedTeamId;
            var managedMatch = matches.FirstOrDefault(match => match.HomeTeamId == managedTeamId || match.AwayTeamId == managedTeamId);
            var count = matches.Count == 1 ? "1 league match" : $"{matches.Count} league matches";
            return managedMatch is null
                ? $"Play {date}: {count}. Your team does not play."
                : $"Play {date}: {count}, including yours.";
        }
    }

    public bool HasAdvanceError => AdvanceError is not null;

    public string GameName => Session.GameName;

    public ShellPage CurrentPageKind => _pages.Single(pair => pair.Value == CurrentPage).Key;

    public void Navigate(ShellPage page)
    {
        if (!_pages.TryGetValue(page, out var target))
        {
            throw new ArgumentException($"The {page} page is not available yet.", nameof(page));
        }

        CurrentPage = target;
    }

    public void OpenInboxMessage(InboxMessageId messageId)
    {
        Navigate(ShellPage.Inbox);
        Inbox.OpenMessage(messageId);
    }

    public void OpenPlayer(PlayerId playerId)
    {
        Navigate(ShellPage.Roster);
        Roster.SelectPlayer(playerId);
    }

    /// <summary>
    /// Shows a match on the schedule page, in the schedule of the given team, which plays in it.
    /// </summary>
    public void OpenMatch(DateOnly date, TeamId teamId)
    {
        Navigate(ShellPage.Schedule);
        Schedule.OpenMatch(date, teamId);
    }

    /// <summary>
    /// Plays the current league day. Management applies a day completely or not at all, so a
    /// failure is reported and nothing from that day is shown as played.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAdvanceDay))]
    private async Task AdvanceDayAsync()
    {
        var date = MatchDisplay.ShortDate(Session.Snapshot.Season.CurrentDate);
        AdvanceError = null;
        try
        {
            await Session.AdvanceDayAsync();
        }
        catch (Exception exception)
        {
            // The match engine and season validation may fail with any exception type. The day
            // stays unplayed, so the user can read why and try again.
            AdvanceError = $"{date} could not be played, and no results were applied. {exception.Message}";
        }
    }

    private bool CanAdvanceDay() => !Session.IsAdvancing && !Session.Snapshot.Season.IsComplete;

    [RelayCommand]
    private void DismissAdvanceError()
    {
        AdvanceError = null;
    }

    [RelayCommand]
    private void ShowMainMenu()
    {
        _showMainMenu();
    }

    partial void OnCurrentPageChanged(ShellPageViewModel value)
    {
        UpdateNavigationState();
        OnPropertyChanged(nameof(CurrentPageKind));
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameSession.IsAdvancing))
        {
            OnPropertyChanged(nameof(ContinueLabel));
            AdvanceDayCommand.NotifyCanExecuteChanged();
            return;
        }

        if (e.PropertyName != nameof(GameSession.Snapshot))
        {
            return;
        }

        foreach (var page in _pages.Values)
        {
            page.Refresh();
        }

        UpdateNavigationState();
        OnPropertyChanged(nameof(PhaseLabel));
        OnPropertyChanged(nameof(ContinueDescription));
        AdvanceDayCommand.NotifyCanExecuteChanged();
    }

    private void UpdateNavigationState()
    {
        var current = CurrentPageKind;
        foreach (var item in NavigationSections.SelectMany(section => section.Items))
        {
            item.IsActive = item.Page == current;
        }

        _inboxItem.BadgeCount = Session.Snapshot.Inbox.Count(message => !message.IsRead);
    }
}