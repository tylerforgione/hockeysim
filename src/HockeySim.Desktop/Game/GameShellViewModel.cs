using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Home;
using HockeySim.Desktop.Inbox;
using HockeySim.Desktop.Lines;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Roster;
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

    public GameShellViewModel(GameSession session, Action? showMainMenu = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        Session = session;
        _showMainMenu = showMainMenu ?? (() => { });

        Home = new HomePageViewModel(session, Navigate, OpenInboxMessage, OpenPlayer);
        Inbox = new InboxPageViewModel(session);
        Roster = new RosterPageViewModel(session);
        Lines = new LinesPageViewModel(session);
        Teams = new TeamsPageViewModel(session);
        _pages = new Dictionary<ShellPage, ShellPageViewModel>
        {
            [ShellPage.Home] = Home,
            [ShellPage.Inbox] = Inbox,
            [ShellPage.Roster] = Roster,
            [ShellPage.Lines] = Lines,
            [ShellPage.Teams] = Teams,
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
                new(ShellPage.Standings, "Standings", Navigate, NotAvailableYet),
                new(ShellPage.Schedule, "Schedule", Navigate, NotAvailableYet),
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

    public string PhaseLabel => "Preseason";

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
        if (e.PropertyName != nameof(GameSession.Snapshot))
        {
            return;
        }

        foreach (var page in _pages.Values)
        {
            page.Refresh();
        }

        UpdateNavigationState();
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