using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Confirmation;
using HockeySim.Desktop.Home;
using HockeySim.Desktop.Inbox;
using HockeySim.Desktop.Injuries;
using HockeySim.Desktop.Lines;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Playoffs;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Saves;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.Standings;
using HockeySim.Desktop.Teams;
using HockeySim.Desktop.TeamStatistics;
using HockeySim.Domain;
using HockeySim.Management.Inbox;
using HockeySim.Management.Saves;
using HockeySim.Management.SeasonPlay;

namespace HockeySim.Desktop.Game;

/// <summary>
/// The in-game frame: a menu bar with the date and Continue, the managed team's banner, the current
/// section's page tabs, the active page, and a status bar.
/// </summary>
public sealed partial class GameShellViewModel : ObservableObject
{
    private readonly Action _showMainMenu;
    private readonly Action? _showLoadGame;
    private readonly ISavedGameLibrary? _saves;
    private readonly Dictionary<ShellPage, ShellPageViewModel> _pages;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Banner))]
    private ShellPageViewModel _currentPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeSection))]
    private NavigationSectionViewModel _currentSection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAdvanceError))]
    private string? _advanceError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSimulationSummary))]
    private string? _simulationSummary;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSaveDialogOpen))]
    private SaveGameViewModel? _saveDialog;

    private ShellPage _currentPageKind = ShellPage.Home;

    private SeasonPhase? _simulateAheadPhase;

    private CancellationTokenSource? _simulation;

    /// <param name="showMainMenu">Leaves the game for the main menu.</param>
    /// <param name="saves">Where the game can be saved; without it, saving is unavailable.</param>
    /// <param name="confirmation">
    /// Asks the user before an existing save is replaced. The main window shares its own, so the
    /// question appears over every screen.
    /// </param>
    /// <param name="showLoadGame">Opens the saved games; without it, loading is unavailable.</param>
    public GameShellViewModel(
        GameSession session,
        Action? showMainMenu = null,
        ISavedGameLibrary? saves = null,
        ConfirmationViewModel? confirmation = null,
        Action? showLoadGame = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        Session = session;
        _showMainMenu = showMainMenu ?? (() => { });
        _showLoadGame = showLoadGame;
        _saves = saves;
        Confirmation = confirmation ?? new ConfirmationViewModel();

        TeamBanner = new TeamBannerViewModel(session);
        Home = new HomePageViewModel(session, Navigate, OpenInboxMessage, OpenPlayer, OpenMatch);
        Inbox = new InboxPageViewModel(session);
        Roster = new RosterPageViewModel(session);
        Lines = new LinesPageViewModel(session);
        Schedule = new SchedulePageViewModel(session);
        TeamStatistics = new TeamStatisticsPageViewModel(session);
        Injuries = new InjuriesPageViewModel(session);
        Teams = new TeamsPageViewModel(session);
        Standings = new StandingsPageViewModel(session);
        Playoffs = new PlayoffsPageViewModel(session, OpenMatch);
        _pages = new Dictionary<ShellPage, ShellPageViewModel>
        {
            [ShellPage.Home] = Home,
            [ShellPage.Inbox] = Inbox,
            [ShellPage.Roster] = Roster,
            [ShellPage.Lines] = Lines,
            [ShellPage.TeamSchedule] = Schedule,
            [ShellPage.TeamStatistics] = TeamStatistics,
            [ShellPage.Injuries] = Injuries,
            [ShellPage.Standings] = Standings,
            [ShellPage.Teams] = Teams,
            [ShellPage.PlayoffPicture] = Playoffs,
            [ShellPage.LeagueSchedule] = Schedule,
        };

        Sections =
        [
            CreateSection(ShellSection.Home, (ShellPage.Home, "Overview")),
            CreateSection(
                ShellSection.Team,
                (ShellPage.Roster, "Roster"),
                (ShellPage.Lines, "Lines"),
                (ShellPage.TeamSchedule, "Schedule"),
                (ShellPage.TeamStatistics, "Team statistics"),
                (ShellPage.Injuries, "Injuries")),
            CreateSection(
                ShellSection.League,
                (ShellPage.Standings, "Standings"),
                (ShellPage.Teams, "Teams"),
                (ShellPage.LeagueLeaders, "League leaders"),
                (ShellPage.PlayoffPicture, "Playoff picture"),
                (ShellPage.LeagueSchedule, "Schedule")),
            CreateSection(ShellSection.Stats, (ShellPage.PlayerStatistics, "Player statistics")),
            CreateSection(ShellSection.Club, (ShellPage.Staff, "Staff"), (ShellPage.Transactions, "Transactions")),
            CreateSection(ShellSection.Inbox, (ShellPage.Inbox, "Inbox")),
        ];

        _currentPage = Home;
        _currentSection = Sections[0];
        UpdateNavigationState();
        UpdateSimulateAheadOptions();
        session.PropertyChanged += OnSessionChanged;
    }

    public GameSession Session { get; }

    public ConfirmationViewModel Confirmation { get; }

    public TeamBannerViewModel TeamBanner { get; }

    public HomePageViewModel Home { get; }

    public InboxPageViewModel Inbox { get; }

    public RosterPageViewModel Roster { get; }

    public LinesPageViewModel Lines { get; }

    /// <summary>Gets the schedule, shared by the team and league sections.</summary>
    public SchedulePageViewModel Schedule { get; }

    public TeamStatisticsPageViewModel TeamStatistics { get; }

    public InjuriesPageViewModel Injuries { get; }

    public TeamsPageViewModel Teams { get; }

    public StandingsPageViewModel Standings { get; }

    public PlayoffsPageViewModel Playoffs { get; }

    /// <summary>Gets every section with its page tabs, including pages not available yet.</summary>
    public IReadOnlyList<NavigationSectionViewModel> Sections { get; }

    public ShellPage CurrentPageKind => _currentPageKind;

    public bool IsHomeSection => CurrentSection.Section == ShellSection.Home;

    /// <summary>Gets the current page's own banner, or the managed team's.</summary>
    public object Banner => CurrentPage.Banner ?? TeamBanner;

    public string TeamName => Session.ManagedTeam.Name;

    /// <summary>
    /// Gets the date and what is coming: days to the managed team's next match, then the phase's
    /// key date: opening day in the preseason, the end of the regular season, or the playoff round
    /// under way. Once the season is complete, it names the champion.
    /// </summary>
    public string CalendarLabel
    {
        get
        {
            var snapshot = Session.Snapshot;
            var season = snapshot.Season;
            var schedule = snapshot.Schedule;
            var date = season.CurrentDate.ToString("ddd d MMM yyyy", CultureInfo.CurrentCulture);
            if (season.Playoffs?.ChampionId is { } championId)
            {
                return $"{date} · {Session.GetTeam(championId).Name} are champions";
            }

            var parts = new List<string> { date };
            var managedTeamId = snapshot.ManagedTeamId;
            var nextMatch = schedule.PreseasonMatches.Concat(schedule.Matches).Concat(schedule.PlayoffMatches).FirstOrDefault(match =>
                match.Date >= season.CurrentDate && (match.HomeTeamId == managedTeamId || match.AwayTeamId == managedTeamId));
            if (nextMatch is not null)
            {
                parts.Add($"Next match {DaysUntil(nextMatch.Date)}");
            }

            parts.Add(season switch
            {
                { Phase: SeasonPhase.Preseason } => $"Regular season starts {DaysUntil(schedule.Matches[0].Date)}",
                { Phase: SeasonPhase.Playoffs, Playoffs: { } playoffs } => $"Playoffs · {PlayoffDisplay.RoundName(playoffs.CurrentRound)}",
                _ => $"Regular season ends {DaysUntil(schedule.Matches[^1].Date)}",
            });
            return string.Join(" · ", parts);
        }
    }

    /// <summary>Gets Continue's text: what it does, or how far a day or run being played has got.</summary>
    public string ContinueLabel
    {
        get
        {
            if (!Session.IsAdvancing)
            {
                return "Continue";
            }

            if (!IsSimulatingAhead)
            {
                return "Playing…";
            }

            return Session.SimulatedDate is { } date ? $"Simulating… {MatchDisplay.ShortDate(date)}" : "Simulating…";
        }
    }

    /// <summary>Gets whether a run of several days is under way, which Stop can cancel.</summary>
    public bool IsSimulatingAhead => _simulation is not null;

    public bool HasSimulationSummary => SimulationSummary is not null;

    /// <summary>
    /// Gets Continue's menu: the current phase's runs. The collection is updated in place, because
    /// the menu keeps the collection it was first given and would not see a replacement.
    /// </summary>
    public ObservableCollection<SimulateAheadOptionViewModel> SimulateAheadOptions { get; } = [];

    /// <summary>Gets whether Continue's menu can be opened: whenever a day could be played.</summary>
    public bool CanOpenSimulateAhead => CanAdvanceDay();

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
                return "Season complete";
            }

            var date = MatchDisplay.ShortDate(season.CurrentDate);
            if (HasPlayersToReplace)
            {
                return $"{date} · Injured players to replace";
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
                return $"{date} · No league matches";
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
            return managedMatch is null ? $"{date} · {count}" : $"{date} · {count}, including yours";
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
            return $"Injured players dressed: {string.Join(", ", names)}";
        }
    }

    public int UnreadCount => Session.Snapshot.Inbox.Count(message => !message.IsRead);

    public string InboxMenuLabel => UnreadCount == 0 ? "Inbox" : $"Inbox ({UnreadCount})";

    public string GameName => Session.GameName;

    public bool IsSaveDialogOpen => SaveDialog is not null;

    public string SaveStatus => Session.HasUnsavedChanges ? "Unsaved changes" : "Saved";

    /// <summary>
    /// Gets how many of the managed team's dressed players cannot play. AI teams replace theirs;
    /// the user must.
    /// </summary>
    public int DressedPlayersOut
    {
        get
        {
            var team = Session.ManagedTeam;
            var dressed = team.Lineup.DressedPlayerIds.ToHashSet();
            return team.Roster.Count(player => dressed.Contains(player.Id) && !player.CanPlay);
        }
    }

    public bool IsLineupValid => DressedPlayersOut == 0;

    public string LineupStatus => DressedPlayersOut switch
    {
        0 => "Lineup valid",
        1 => "Lineup: 1 player out injured",
        var count => string.Create(CultureInfo.CurrentCulture, $"Lineup: {count} players out injured"),
    };

    /// <summary>
    /// Gets the newest unread message's subject, with how many more are unread, or empty when
    /// everything is read.
    /// </summary>
    public string LatestInboxItem
    {
        get
        {
            var unread = Session.Snapshot.Inbox.Where(message => !message.IsRead).ToList();
            return unread.Count switch
            {
                0 => string.Empty,
                1 => $"Inbox: {unread[0].Subject}",
                _ => string.Create(CultureInfo.CurrentCulture, $"Inbox: {unread[0].Subject} (+{unread.Count - 1})"),
            };
        }
    }

    public bool HasLatestInboxItem => UnreadCount > 0;

    public static string Version => $"HockeySim {AppVersion.Current}";

    /// <summary>
    /// Shows a page in its section. Opening the team schedule shows the managed team's; the league
    /// schedule keeps whichever team was chosen there.
    /// </summary>
    /// <exception cref="ArgumentException">The page is not available yet.</exception>
    public void Navigate(ShellPage page)
    {
        if (!_pages.TryGetValue(page, out var target))
        {
            throw new ArgumentException($"The {page} page is not available yet.", nameof(page));
        }

        if (page == ShellPage.TeamSchedule)
        {
            Schedule.ShowTeam(Session.Snapshot.ManagedTeamId);
        }

        _currentPageKind = page;
        CurrentSection = Sections.Single(section => section.Items.Any(item => item.Page == page));
        CurrentPage = target;
        UpdateNavigationState();
        OnPropertyChanged(nameof(CurrentPageKind));
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
    /// Shows a match in the schedule of the given team, which plays in it: the team schedule for
    /// the managed team, otherwise the league schedule.
    /// </summary>
    public void OpenMatch(DateOnly date, TeamId teamId)
    {
        Navigate(teamId == Session.Snapshot.ManagedTeamId ? ShellPage.TeamSchedule : ShellPage.LeagueSchedule);
        Schedule.OpenMatch(date, teamId);
    }

    [RelayCommand(CanExecute = nameof(IsPageAvailable))]
    private void OpenPage(ShellPage page)
    {
        Navigate(page);
    }

    private bool IsPageAvailable(ShellPage page) => _pages.ContainsKey(page);

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

    /// <summary>
    /// Plays days toward <paramref name="target"/> until it is reached, Stop is pressed, or
    /// something needs the user, then says how the run ended. Each day applies completely or not
    /// at all, so a failure part-way keeps the days before it.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSimulateAhead))]
    private async Task SimulateAheadAsync(SimulationTarget target)
    {
        var startDate = Session.Snapshot.Season.CurrentDate;
        AdvanceError = null;
        SimulationSummary = null;
        using var simulation = new CancellationTokenSource();
        _simulation = simulation;
        OnPropertyChanged(nameof(IsSimulatingAhead));
        OnPropertyChanged(nameof(ContinueLabel));
        StopSimulatingCommand.NotifyCanExecuteChanged();
        try
        {
            var result = await Session.SimulateAheadAsync(target, simulation.Token);
            SimulationSummary = DescribeSimulation(result);
        }
        catch (Exception exception)
        {
            // As for a single day, the engine and season validation may fail with any exception
            // type. Only the failed day is unplayed.
            var failedDate = Session.Snapshot.Season.CurrentDate;
            var played = DaysBetween(startDate, failedDate);
            var prefix = played == 0 ? string.Empty : $"Simulated {DayCount(played)}; ";
            AdvanceError = $"{prefix}{MatchDisplay.ShortDate(failedDate)} could not be played, and no results from it were applied. {exception.Message}";
        }
        finally
        {
            _simulation = null;
            OnPropertyChanged(nameof(IsSimulatingAhead));
            OnPropertyChanged(nameof(ContinueLabel));
            StopSimulatingCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanSimulateAhead(SimulationTarget target) =>
        CanAdvanceDay() && SimulationTargets.AvailableIn(Session.Snapshot.Season.Phase).Contains(target);

    /// <summary>Stops a run before its next day.</summary>
    [RelayCommand(CanExecute = nameof(IsSimulatingAhead))]
    private void StopSimulating()
    {
        _simulation?.Cancel();
    }

    [RelayCommand]
    private void DismissSimulationSummary()
    {
        SimulationSummary = null;
    }

    private string DescribeSimulation(SimulationResult result)
    {
        var season = result.Snapshot.Season;
        var days = $"Simulated {DayCount(result.DaysPlayed)}";
        return result.Stop switch
        {
            SimulationStop.Cancelled when result.DaysPlayed == 0 => "Stopped before any day was played.",
            SimulationStop.Cancelled => $"{days}; stopped on {MatchDisplay.ShortDate(season.CurrentDate)}.",
            SimulationStop.ManagedPlayerInjured => $"{days}; stopped because one of your players was injured.",
            SimulationStop.PlayersToReplace => $"{days}; stopped to replace injured players before today's match.",
            SimulationStop.PhaseChanged when season.Phase == SeasonPhase.Playoffs => $"{days}; stopped because the regular season is over.",
            SimulationStop.PhaseChanged => $"{days}; stopped because the regular season starts today.",
            SimulationStop.SeasonComplete => $"{days}; stopped because the season is complete.",
            _ => $"{days}, to {MatchDisplay.ShortDate(season.CurrentDate)}.",
        };
    }

    private static int DaysBetween(DateOnly from, DateOnly to) => to.DayNumber - from.DayNumber;

    private static string DayCount(int days) =>
        days == 1 ? "1 day" : string.Create(CultureInfo.CurrentCulture, $"{days} days");

    /// <summary>Offers the current phase's targets, rebuilding the menu only when the phase changes.</summary>
    private void UpdateSimulateAheadOptions()
    {
        var phase = Session.Snapshot.Season.Phase;
        if (phase != _simulateAheadPhase)
        {
            _simulateAheadPhase = phase;
            SimulateAheadOptions.Clear();
            foreach (var target in SimulationTargets.AvailableIn(phase))
            {
                SimulateAheadOptions.Add(new SimulateAheadOptionViewModel(target, SimulateAheadOptionViewModel.LabelFor(target), SimulateAheadCommand));
            }
        }

        SimulateAheadCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanOpenSimulateAhead));
    }

    [RelayCommand]
    private void OpenLines()
    {
        Navigate(ShellPage.Lines);
    }

    [RelayCommand]
    private void OpenLatestInboxItem()
    {
        var latest = Session.Snapshot.Inbox.FirstOrDefault(message => !message.IsRead);
        if (latest is null)
        {
            Navigate(ShellPage.Inbox);
            return;
        }

        OpenInboxMessage(latest.Id);
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
    /// Opens the saved games, which may replace this game. That waits until a day being played has
    /// finished, as leaving for the main menu does.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLoadGame))]
    private void LoadGame()
    {
        _showLoadGame!();
    }

    private bool CanLoadGame() => _showLoadGame is not null && !Session.IsAdvancing;

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

    private NavigationSectionViewModel CreateSection(ShellSection section, params (ShellPage Page, string Label)[] tabs) =>
        new(section, tabs.Select(tab => new NavigationItemViewModel(tab.Page, tab.Label, _pages.ContainsKey(tab.Page), Navigate)).ToList());

    private string DaysUntil(DateOnly date)
    {
        var days = date.DayNumber - Session.Snapshot.Season.CurrentDate.DayNumber;
        return days switch
        {
            0 => "today",
            1 => "tomorrow",
            _ => string.Create(CultureInfo.CurrentCulture, $"in {days} days"),
        };
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameSession.SimulatedDate))
        {
            OnPropertyChanged(nameof(ContinueLabel));
            return;
        }

        if (e.PropertyName == nameof(GameSession.IsAdvancing))
        {
            OnPropertyChanged(nameof(ContinueLabel));
            AdvanceDayCommand.NotifyCanExecuteChanged();
            SimulateAheadCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanOpenSimulateAhead));
            SaveGameCommand.NotifyCanExecuteChanged();
            LoadGameCommand.NotifyCanExecuteChanged();
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

        // The schedule serves two tabs; refresh each page once.
        foreach (var page in _pages.Values.Distinct())
        {
            page.Refresh();
        }

        TeamBanner.Refresh();
        UpdateNavigationState();
        OnPropertyChanged(nameof(CalendarLabel));
        OnPropertyChanged(nameof(ContinueDescription));
        OnPropertyChanged(nameof(HasPlayersToReplace));
        OnPropertyChanged(nameof(PlayersToReplaceMessage));
        OnPropertyChanged(nameof(UnreadCount));
        OnPropertyChanged(nameof(InboxMenuLabel));
        OnPropertyChanged(nameof(LatestInboxItem));
        OnPropertyChanged(nameof(HasLatestInboxItem));
        OnPropertyChanged(nameof(DressedPlayersOut));
        OnPropertyChanged(nameof(IsLineupValid));
        OnPropertyChanged(nameof(LineupStatus));
        AdvanceDayCommand.NotifyCanExecuteChanged();
        UpdateSimulateAheadOptions();
    }

    private void UpdateNavigationState()
    {
        foreach (var item in Sections.SelectMany(section => section.Items))
        {
            item.IsActive = item.Page == _currentPageKind;
        }
    }
}