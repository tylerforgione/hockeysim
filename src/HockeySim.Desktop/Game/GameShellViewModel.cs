using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Confirmation;
using HockeySim.Desktop.Home;
using HockeySim.Desktop.Inbox;
using HockeySim.Desktop.Lines;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Playoffs;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Saves;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.Standings;
using HockeySim.Desktop.Teams;
using HockeySim.Domain;
using HockeySim.Management.Inbox;
using HockeySim.Management.Saves;

namespace HockeySim.Desktop.Game;

/// <summary>
/// The in-game frame: team identity and time controls across the top, navigation down the side,
/// and the active page in the middle.
/// </summary>
public sealed partial class GameShellViewModel : ObservableObject
{
    private const string NotAvailableYet = "Not available yet";

    private readonly Action _showMainMenu;
    private readonly ISavedGameLibrary? _saves;
    private readonly Dictionary<ShellPage, ShellPageViewModel> _pages;
    private readonly NavigationItemViewModel _inboxItem;

    [ObservableProperty]
    private ShellPageViewModel _currentPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAdvanceError))]
    private string? _advanceError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSaveDialogOpen))]
    private SaveGameViewModel? _saveDialog;

    /// <param name="saves">Where the game can be saved; without it, saving is unavailable.</param>
    /// <param name="confirmation">
    /// Asks the user before an existing save is replaced. The main window shares its own, so the
    /// question appears over every screen.
    /// </param>
    public GameShellViewModel(
        GameSession session,
        Action? showMainMenu = null,
        ISavedGameLibrary? saves = null,
        ConfirmationViewModel? confirmation = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        Session = session;
        _showMainMenu = showMainMenu ?? (() => { });
        _saves = saves;
        Confirmation = confirmation ?? new ConfirmationViewModel();

        Home = new HomePageViewModel(session, Navigate, OpenInboxMessage, OpenPlayer, OpenMatch);
        Inbox = new InboxPageViewModel(session);
        Roster = new RosterPageViewModel(session);
        Lines = new LinesPageViewModel(session);
        Teams = new TeamsPageViewModel(session);
        Standings = new StandingsPageViewModel(session);
        Playoffs = new PlayoffsPageViewModel(session, OpenMatch);
        Schedule = new SchedulePageViewModel(session);
        _pages = new Dictionary<ShellPage, ShellPageViewModel>
        {
            [ShellPage.Home] = Home,
            [ShellPage.Inbox] = Inbox,
            [ShellPage.Roster] = Roster,
            [ShellPage.Lines] = Lines,
            [ShellPage.Teams] = Teams,
            [ShellPage.Standings] = Standings,
            [ShellPage.Playoffs] = Playoffs,
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
                new(ShellPage.Playoffs, "Playoffs", Navigate),
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

    public ConfirmationViewModel Confirmation { get; }

    public HomePageViewModel Home { get; }

    public InboxPageViewModel Inbox { get; }

    public RosterPageViewModel Roster { get; }

    public LinesPageViewModel Lines { get; }

    public TeamsPageViewModel Teams { get; }

    public StandingsPageViewModel Standings { get; }

    public PlayoffsPageViewModel Playoffs { get; }

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

    /// <summary>
    /// The current phase and date, naming the playoff round under way, and the champion once the
    /// season is complete.
    /// </summary>
    public string PhaseLabel => Session.Snapshot.Season switch
    {
        { Playoffs.ChampionId: { } champion } => $"Season complete · {Session.GetTeam(champion).Name} are champions",
        { Phase: SeasonPhase.Preseason } season => $"Preseason · {MatchDisplay.ShortDate(season.CurrentDate)}",
        { Phase: SeasonPhase.Playoffs, Playoffs: { } playoffs } season =>
            $"Playoffs · {PlayoffDisplay.RoundName(playoffs.CurrentRound)} · {MatchDisplay.ShortDate(season.CurrentDate)}",
        var season => $"Regular season · {MatchDisplay.ShortDate(season.CurrentDate)}",
    };

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
                return "The season is complete. Results and rosters remain available.";
            }

            var date = MatchDisplay.ShortDate(season.CurrentDate);
            if (HasPlayersToReplace)
            {
                return $"Replace the injured players in your lineup before playing {date}.";
            }

            var schedule = season.Phase switch
            {
                SeasonPhase.Preseason => Session.Snapshot.Schedule.PreseasonMatches,
                SeasonPhase.Playoffs => Session.Snapshot.Schedule.PlayoffMatches,
                _ => Session.Snapshot.Schedule.Matches,
            };
            var matches = schedule.Where(match => match.Date == season.CurrentDate).ToList();
            if (matches.Count == 0)
            {
                return $"No league matches on {date}. Continue to the next day.";
            }

            var managedTeamId = Session.Snapshot.ManagedTeamId;
            var managedMatch = matches.FirstOrDefault(match => match.HomeTeamId == managedTeamId || match.AwayTeamId == managedTeamId);
            var kind = season.Phase switch
            {
                SeasonPhase.Preseason => "preseason",
                SeasonPhase.Playoffs => "playoff",
                _ => "league",
            };
            var count = matches.Count == 1 ? $"1 {kind} match" : $"{matches.Count} {kind} matches";
            return managedMatch is null
                ? $"Play {date}: {count}. Your team does not play."
                : $"Play {date}: {count}, including yours.";
        }
    }

    public bool HasAdvanceError => AdvanceError is not null;

    /// <summary>
    /// Gets whether the managed team plays today with players dressed who cannot play. Management
    /// would reject the day, so Continue waits until the lineup is changed.
    /// </summary>
    public bool HasPlayersToReplace => Session.Snapshot.PlayersToReplace.Count > 0;

    /// <summary>Names the players to replace, or is empty when there are none.</summary>
    public string PlayersToReplaceMessage
    {
        get
        {
            if (!HasPlayersToReplace)
            {
                return string.Empty;
            }

            var names = Session.Snapshot.PlayersToReplace.Select(id => Session.PlayersById[id]).Select(player => $"{PlayerDisplay.FullName(player)} (#{player.Number})");
            return $"Your lineup dresses injured players who cannot play: {string.Join(", ", names)}. "
                + "Replace them on the Lines page and save the lineup to continue.";
        }
    }

    public string GameName => Session.GameName;

    public bool IsSaveDialogOpen => SaveDialog is not null;

    public string SaveStatus => Session.HasUnsavedChanges ? "Unsaved changes" : "Saved";

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

    private bool CanAdvanceDay() => !Session.IsAdvancing && !Session.Snapshot.Season.IsComplete && !HasPlayersToReplace;

    [RelayCommand]
    private void OpenLines()
    {
        Navigate(ShellPage.Lines);
    }

    [RelayCommand]
    private void DismissAdvanceError()
    {
        AdvanceError = null;
    }

    /// <summary>
    /// Opens the save dialog. Saving stays available once the season is complete.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveGame))]
    private void SaveGame()
    {
        SaveDialog = new SaveGameViewModel(Session, _saves!, Confirmation, () => SaveDialog = null);
    }

    private bool CanSaveGame() => _saves is not null && !Session.IsAdvancing;

    /// <summary>
    /// Leaves for the main menu, where the game can be replaced. That waits until a day being
    /// played has finished, so the day cannot complete into a game that has since been replaced.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanShowMainMenu))]
    private void ShowMainMenu()
    {
        _showMainMenu();
    }

    private bool CanShowMainMenu() => !Session.IsAdvancing;

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
            SaveGameCommand.NotifyCanExecuteChanged();
            ShowMainMenuCommand.NotifyCanExecuteChanged();
            return;
        }

        if (e.PropertyName == nameof(GameSession.GameName))
        {
            OnPropertyChanged(nameof(GameName));
            return;
        }

        if (e.PropertyName == nameof(GameSession.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(SaveStatus));
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
        OnPropertyChanged(nameof(HasPlayersToReplace));
        OnPropertyChanged(nameof(PlayersToReplaceMessage));
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