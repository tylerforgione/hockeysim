using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using HockeySim.Desktop;
using HockeySim.Desktop.Game;
using HockeySim.Desktop.Home;
using HockeySim.Desktop.Inbox;
using HockeySim.Desktop.Injuries;
using HockeySim.Desktop.Lines;
using HockeySim.Desktop.Main;
using HockeySim.Desktop.NewGame;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Playoffs;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Saves;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.Standings;
using HockeySim.Desktop.Startup;
using HockeySim.Desktop.Tables;
using HockeySim.Desktop.Teams;
using HockeySim.Desktop.TeamStatistics;
using HockeySim.Desktop.Theme;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class MainWindowViewTests
{
    [Fact]
    public void MainWindowCreatesAGameAndRendersEveryAvailablePage()
    {
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true,
            })
            .SetupWithoutStarting();

        var gameManager = new GameManager();
        using var saves = new TemporarySaveDirectory();
        var viewModel = new MainWindowViewModel(gameManager, saves.Library);
        var window = new MainWindow
        {
            DataContext = viewModel,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var startupView = Single<StartupView>(window);
        Assert.False(window.Resources.ContainsKey(TeamPalette.PrimaryKey));
        Assert.Equal(
            $"Version {AppVersion.Current} · Pre-release build",
            startupView.FindControl<TextBlock>("VersionLabel")?.Text);
        Click(Assert.IsType<Button>(startupView.FindControl<Button>("NewGameMenuButton")));

        var newGameView = Single<NewGameView>(window);
        var conferences = Assert.IsType<ItemsControl>(newGameView.FindControl<ItemsControl>("ConferencesList"));
        var teamButtons = newGameView.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.DataContext is TeamOptionViewModel)
            .ToList();

        Assert.Equal(2, conferences.Items.Cast<object>().Count());
        Assert.Equal(32, teamButtons.Count);
        Assert.All(teamButtons, button =>
        {
            Assert.True(button.Bounds.Width > 80);
            Assert.True(button.Bounds.Height > 20);
        });

        Click(teamButtons.Single(button => Equals(button.Content, "Seattle Evergreens")));
        viewModel.NewGame!.GameName = "Seattle Dynasty";
        Click(Assert.IsType<Button>(newGameView.FindControl<Button>("CreateGameButton")));

        var shellView = Single<GameShellView>(window);
        Assert.Equal("SEATTLE EVERGREENS", Named<TextBlock>(shellView, "ShellTeamName").Text);
        Assert.Equal(3, Named<ItemsControl>(shellView, "BannerFigures").ItemCount);
        Assert.Contains(" · Next match ", shellView.FindControl<TextBlock>("CalendarLabel")?.Text, StringComparison.Ordinal);
        Assert.Equal("Lineup valid", shellView.FindControl<Button>("LineupStatusButton")?.Content);
        Assert.Equal($"HockeySim {AppVersion.Current}", shellView.FindControl<TextBlock>("VersionText")?.Text);
        Assert.False(shellView.FindControl<Button>("BackButton")?.IsEffectivelyEnabled);
        Assert.False(shellView.FindControl<Button>("ForwardButton")?.IsEffectivelyEnabled);
        Assert.True(shellView.FindControl<Button>("SimulateAheadButton")?.IsEffectivelyEnabled);
        Assert.False(shellView.FindControl<Button>("StopSimulatingButton")?.IsVisible);

        // Every menu and item has an access key, so the keyboard reaches each one.
        var menuItems = shellView.GetLogicalDescendants().OfType<MenuItem>().ToList();
        Assert.Equal(24, menuItems.Count);
        Assert.All(menuItems, item => Assert.Contains("_", item.Header?.ToString(), StringComparison.Ordinal));
        Assert.Equal("_Inbox (4)", MenuItemNamed(shellView, "InboxMenuItem").Header);

        // The window wears the managed team's colours, with primary-coloured text on the secondary.
        var seattlePrimary = Color.Parse("#0B4F3C");
        Assert.Equal(seattlePrimary, ResourceColor(window, TeamPalette.PrimaryKey));
        Assert.Equal(Colors.White, ResourceColor(window, TeamPalette.OnPrimaryKey));
        Assert.Equal(Color.Parse("#99D9C1"), ResourceColor(window, TeamPalette.SecondaryKey));
        Assert.Equal(seattlePrimary, ResourceColor(window, TeamPalette.OnSecondaryKey));
        var panelHeader = window.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("panel-header"));
        Assert.Equal(seattlePrimary, Assert.IsAssignableFrom<ISolidColorBrush>(panelHeader.Background).Color);
        var continueButton = Assert.IsType<Button>(shellView.FindControl<Button>("ContinueButton"));
        Assert.True(continueButton.IsEffectivelyEnabled);
        Assert.Single(window.GetVisualDescendants().OfType<HomePageView>());
        foreach (var panel in new Control[]
        {
            Single<UpcomingGamesPanelView>(window),
            Single<StandingsPanelView>(window),
            Single<TaleOfTheTapePanelView>(window),
            Single<RecentResultsPanelView>(window),
            Single<TeamLeadersPanelView>(window),
        })
        {
            Assert.True(panel.Bounds.Width > 150);
            Assert.True(panel.Bounds.Height > 150);
        }

        Assert.Equal(8, CountStandingsRows(Single<StandingsPanelView>(window)));

        AssertNavigationRenders<InboxPageView>(window, ShellPage.Inbox);
        AssertNavigationRenders<RosterPageView>(window, ShellPage.Roster);
        Assert.Equal(5, Named<ItemsControl>(shellView, "PageTabs").ItemCount);
        AssertNavigationRenders<TeamsPageView>(window, ShellPage.Teams);
        AssertNavigationRenders<StandingsPageView>(window, ShellPage.Standings);
        AssertNavigationRenders<PlayoffsPageView>(window, ShellPage.PlayoffPicture);
        Assert.True(Single<PlayoffsPageView>(window).FindControl<TextBlock>("PlayoffsNotStarted")?.IsEffectivelyVisible);
        AssertNavigationRenders<SchedulePageView>(window, ShellPage.LeagueSchedule);
        AssertNavigationRenders<SchedulePageView>(window, ShellPage.TeamSchedule);
        AssertNavigationRenders<TeamStatisticsPageView>(window, ShellPage.TeamStatistics);
        Assert.Equal(7, Named<ItemsControl>(window, "TeamStatisticsList").ItemCount);
        AssertNavigationRenders<InjuriesPageView>(window, ShellPage.Injuries);
        AssertNavigationRenders<LinesPageView>(window, ShellPage.Lines);

        var linesView = Single<LinesPageView>(window);
        var saveButton = Assert.IsType<Button>(linesView.FindControl<Button>("SaveLineupButton"));
        Assert.False(saveButton.IsEffectivelyEnabled);

        // Choose the scratched goalie as the starter through the rendered combo box.
        var starterCombo = Assert.IsType<ComboBox>(linesView.FindControl<ComboBox>("StartingGoalieSelector"));
        var scratchedGoalie = viewModel.Game!.Lines.Lineup.Scratches.Single(player => player.PositionAbbreviation == "G");
        starterCombo.SelectedItem = scratchedGoalie;
        Dispatcher.UIThread.RunJobs();

        Assert.True(saveButton.IsEffectivelyEnabled);
        Click(saveButton);

        var snapshot = gameManager.GetSnapshot();
        Assert.Equal(
            scratchedGoalie.Id,
            snapshot.League.Teams.Single(team => team.Id == snapshot.ManagedTeamId).Lineup.StartingGoalieId);
        Assert.False(saveButton.IsEffectivelyEnabled);

        // Each unit tab renders its units, with a choice for every slot.
        var powerPlayUnits = Assert.IsType<ItemsControl>(linesView.FindControl<ItemsControl>("PowerPlayUnits"));
        Assert.False(powerPlayUnits.IsVisible);
        Click(Assert.IsType<Button>(linesView.FindControl<Button>("PowerPlayTabButton")));
        Assert.True(powerPlayUnits.IsVisible);
        Assert.Equal(28, powerPlayUnits.GetVisualDescendants().OfType<ComboBox>().Count());
        Click(Assert.IsType<Button>(linesView.FindControl<Button>("OtherTabButton")));
        var otherUnits = Assert.IsType<StackPanel>(linesView.FindControl<StackPanel>("OtherSituationUnits"));
        Assert.Equal(19, otherUnits.GetVisualDescendants().OfType<ComboBox>().Count());

        // Another team's lineup is shown read-only, without the save bar.
        var linesTeamSelector = Assert.IsType<ComboBox>(linesView.FindControl<ComboBox>("LinesTeamSelector"));
        linesTeamSelector.SelectedItem = viewModel.Game!.Lines.Teams.First(team => !team.IsManaged);
        Dispatcher.UIThread.RunJobs();
        Assert.False(saveButton.IsEffectivelyVisible);
        Assert.All(otherUnits.GetVisualDescendants().OfType<ComboBox>(), combo => Assert.False(combo.IsEffectivelyEnabled));
        linesTeamSelector.SelectedItem = viewModel.Game!.Lines.Teams.Single(team => team.IsManaged);
        Dispatcher.UIThread.RunJobs();
        Assert.True(saveButton.IsEffectivelyVisible);

        // Simulate the preseason from Continue's menu and play opening night with Continue, then
        // open a result from the home page.
        AssertNavigationRenders<HomePageView>(window, ShellPage.Home);
        Assert.Contains(" · Regular season starts ", shellView.FindControl<TextBlock>("CalendarLabel")?.Text, StringComparison.Ordinal);
        var simulateAhead = Named<Button>(shellView, "SimulateAheadButton");
        var simulateAheadMenu = Assert.IsType<MenuFlyout>(simulateAhead.Flyout);
        simulateAheadMenu.ShowAt(simulateAhead);
        Dispatcher.UIThread.RunJobs();
        Assert.True(simulateAheadMenu.IsOpen);
        var options = window.GetVisualDescendants().OfType<MenuFlyoutPresenter>().Single().GetVisualDescendants().OfType<MenuItem>().ToList();
        Assert.Equal(["Next day", "To the next match", "To the end of the preseason"], options.Select(item => item.Header));
        var toOpeningDay = options.Single(item => Equals(item.Header, "To the end of the preseason"));
        Click(toOpeningDay);
        simulateAheadMenu.Hide();
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (viewModel.Game!.Session.IsAdvancing)
        {
            Assert.True(DateTime.UtcNow < deadline, "The run did not finish.");
            Thread.Sleep(5);
            Dispatcher.UIThread.RunJobs();
        }

        Dispatcher.UIThread.RunJobs();
        Assert.Equal(SeasonPhase.RegularSeason, gameManager.GetSnapshot().Season.Phase);
        Assert.True(shellView.FindControl<Border>("SimulationSummaryBanner")?.IsVisible);

        // The menu now offers the regular season's runs.
        simulateAheadMenu.ShowAt(simulateAhead);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(
            ["Next day", "To the next match", "One week", "One month", "To the playoffs"],
            window.GetVisualDescendants().OfType<MenuFlyoutPresenter>().Single().GetVisualDescendants().OfType<MenuItem>().Select(item => item.Header));
        simulateAheadMenu.Hide();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(112, gameManager.GetSnapshot().Season.PreseasonResults.Count);
        Assert.Empty(gameManager.GetSnapshot().Season.Results);
        ClickAndWait(continueButton, () => !viewModel.Game!.Session.IsAdvancing);
        Assert.Equal(16, gameManager.GetSnapshot().Season.Results.Count);
        Assert.StartsWith("Fri 2 Oct 2026", shellView.FindControl<TextBlock>("CalendarLabel")?.Text, StringComparison.Ordinal);
        var figureValues = Named<ItemsControl>(shellView, "BannerFigures").GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.Classes.Contains("banner-figure-value"))
            .Select(text => text.Text)
            .ToList();
        Assert.DoesNotContain("—", figureValues.Skip(1));
        Assert.False(shellView.FindControl<Border>("AdvanceErrorBanner")?.IsVisible);

        var homeView = Single<HomePageView>(window);
        var resultButtons = homeView.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.DataContext is RecentResultRowViewModel)
            .ToList();
        Assert.Equal(6, resultButtons.Count);
        Click(resultButtons[0]);

        // The box score opens on its summary; the player tables are on their own tab.
        var matchDetail = Single<MatchDetailView>(window);
        Assert.True(matchDetail.Bounds.Width > 300);
        Assert.StartsWith("Final", matchDetail.FindControl<TextBlock>("FinalLabel")?.Text, StringComparison.Ordinal);
        Assert.True(matchDetail.FindControl<Grid>("MatchBanner")?.IsEffectivelyVisible);
        var detailViewModel = Assert.IsType<MatchDetailViewModel>(matchDetail.DataContext);
        Assert.EndsWith(
            detailViewModel.Arena,
            string.Concat(Named<TextBlock>(matchDetail, "MatchWhereAndWhen").Inlines!.OfType<Run>().Select(run => run.Text)),
            StringComparison.Ordinal);
        Assert.Equal(0, CountRows<SkaterBoxScoreRowViewModel>(matchDetail));
        Assert.Equal(8, CountRows<PeriodRowViewModel>(matchDetail));
        Assert.True(matchDetail.FindControl<StackPanel>("ScoringSummary")?.IsEffectivelyVisible);
        Assert.Equal(detailViewModel.ScoringSummary.Count, CountRows<GoalSummaryRowViewModel>(matchDetail));
        Assert.Equal(detailViewModel.PenaltySummary.Count, CountRows<PenaltySummaryRowViewModel>(matchDetail));
        Assert.Equal(9, CountRows<ComparisonRowViewModel>(matchDetail));
        Assert.Equal(3, CountRows<StarRowViewModel>(matchDetail));
        Assert.Equal(2, CountRows<GoalieSummaryRowViewModel>(matchDetail));
        Click(Named<Button>(matchDetail, "PlayersTabButton"));
        Assert.False(matchDetail.FindControl<ScrollViewer>("SummaryTab")?.IsVisible);
        Assert.Equal(36, CountRows<SkaterBoxScoreRowViewModel>(matchDetail));
        Click(Named<Button>(matchDetail, "SummaryTabButton"));
        Assert.True(matchDetail.FindControl<ScrollViewer>("SummaryTab")?.IsVisible);

        // Standings: the conferences side by side, four division tables by default with the marker
        // legend, then the wild-card view's four division leader tables and two races, then the
        // single league table in one column.
        AssertNavigationRenders<StandingsPageView>(window, ShellPage.Standings);
        var standingsView = Single<StandingsPageView>(window);
        var standingsColumns = Assert.IsType<ItemsControl>(standingsView.FindControl<ItemsControl>("StandingsColumns"));
        Assert.Equal(32, CountStandingsRows(standingsView));
        Assert.Equal(2, standingsColumns.ItemCount);
        Assert.Equal(4, CountStandingsTables(standingsView));
        Assert.True(standingsView.FindControl<TextBlock>("PlayoffStatusLegend")?.IsEffectivelyVisible);
        Click(Assert.IsType<Button>(standingsView.FindControl<Button>("WildCardScopeButton")));
        Assert.Equal(2, standingsColumns.ItemCount);
        Assert.Equal(6, CountStandingsTables(standingsView));
        Assert.Equal(32, CountStandingsRows(standingsView));
        Click(Assert.IsType<Button>(standingsView.FindControl<Button>("LeagueScopeButton")));
        Assert.Equal(1, standingsColumns.ItemCount);
        Assert.Equal(1, CountStandingsTables(standingsView));
        Assert.Equal(32, CountStandingsRows(standingsView));
        Assert.All(
            standingsView.GetVisualDescendants().OfType<Border>().Select(border => border.DataContext).OfType<StandingsRowViewModel>(),
            row => Assert.Equal(1, row.GamesPlayed));
        var teamLink = standingsView.GetVisualDescendants().OfType<Button>().First(button => button.Classes.Contains("cell-link"));
        var linkedTeam = Assert.IsType<StandingsRowViewModel>(teamLink.DataContext).TeamId;
        Click(teamLink);
        Single<TeamsPageView>(window);
        Assert.Equal(linkedTeam, viewModel.Game!.Teams.SelectedTeam.Id);

        // Roster: the tables switched to basic and advanced statistics; the detail panel always
        // shows the full line.
        AssertNavigationRenders<RosterPageView>(window, ShellPage.Roster);
        var rosterView = Single<TeamRosterView>(window);
        var ratingsTable = Named<ListBox>(rosterView, "SkatersTable");
        var basicTable = Named<ListBox>(rosterView, "SkaterBasicTable");
        var advancedTable = Named<ListBox>(rosterView, "SkaterAdvancedTable");
        Assert.True(ratingsTable.IsEffectivelyVisible);
        Assert.False(basicTable.IsEffectivelyVisible);
        Click(Assert.IsType<Button>(rosterView.FindControl<Button>("ShowBasicButton")));
        Assert.False(ratingsTable.IsEffectivelyVisible);
        Assert.True(basicTable.IsEffectivelyVisible);
        Assert.True(basicTable.Bounds.Height > 200);
        Assert.True(Named<ListBox>(rosterView, "GoalieBasicTable").IsEffectivelyVisible);
        Click(Assert.IsType<Button>(rosterView.FindControl<Button>("ShowAdvancedButton")));
        Assert.False(basicTable.IsEffectivelyVisible);
        Assert.True(advancedTable.IsEffectivelyVisible);
        Assert.True(Named<ListBox>(rosterView, "GoalieAdvancedTable").IsEffectivelyVisible);

        // A header sorts its table and shows the direction beside its label.
        var corsiHeader = rosterView.GetVisualDescendants().OfType<SortHeader>().Single(header => header.Column == "corsi-percentage");
        var rosterModel = Assert.IsType<TeamRosterViewModel>(rosterView.DataContext);
        Assert.Equal(string.Empty, corsiHeader.Indicator);
        Click(corsiHeader);
        Assert.Equal("▼", corsiHeader.Indicator);
        Assert.Contains(corsiHeader.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "▼");
        Assert.Equal(rosterModel.Skaters.Select(row => row.Player.Id), advancedTable.Items.OfType<PlayerRowViewModel>().Select(row => row.Player.Id));
        Assert.Equal(
            rosterModel.Skaters.Select(row => row.Season.CorsiPercentageValue).Where(value => value is not null).OrderDescending(),
            rosterModel.Skaters.Select(row => row.Season.CorsiPercentageValue).Where(value => value is not null));
        Click(corsiHeader);
        Assert.Equal("▲", corsiHeader.Indicator);

        // Each column view shows its columns in both tables, and the situation selector is
        // advanced-only, disabled, and five on five alone.
        var skaterTable = Named<SkaterTableView>(rosterView, "SkaterTable");
        var goalieTable = Named<GoalieTableView>(rosterView, "GoalieTable");
        var situation = Named<ComboBox>(rosterView, "SituationSelector");
        Assert.True(situation.IsEffectivelyVisible);
        Assert.False(situation.IsEnabled);
        Assert.Equal(["5 on 5"], situation.Items.OfType<ComboBoxItem>().Select(item => item.Content));
        (string Button, RosterColumns Columns, string[] Skaters, string[] Goalies)[] columnViews =
        [
            ("ShowBasicButton", RosterColumns.Basic,
                ["#", "NAME", "POS", "LINEUP", "AGE", "OVR", "GP", "G", "A", "P", "+/-", "PIM", "PPP", "SHP", "S", "TOI/GP", "FO%"],
                ["#", "NAME", "LINEUP", "AGE", "OVR", "GP", "SA", "SV", "GA", "SV%", "GAA", "SO"]),
            ("ShowAdvancedButton", RosterColumns.Advanced,
                ["#", "NAME", "POS", "LINEUP", "GP", "CF", "CA", "CF%", "FF", "FA", "FF%", "xGF", "xGA", "xGF%", "ixG"],
                ["#", "NAME", "LINEUP", "GP", "TOI", "SA", "GA", "xGA", "GSAx", "SV%", "xSV%"]),
            ("ShowRatingsButton", RosterColumns.Ratings,
                ["#", "NAME", "POS", "LINEUP", "OVR", "SKA", "SHP", "SHA", "PUC", "PAS", "OFF", "DEF", "CHK", "BLK", "STK"],
                ["#", "NAME", "LINEUP", "OVR", "REF", "POS", "REB"]),
            ("ShowBioButton", RosterColumns.Bio,
                ["#", "NAME", "POS", "LINEUP", "AGE", "SH", "HT", "WT", "BORN", "BIRTHPLACE", "NAT"],
                ["#", "NAME", "LINEUP", "AGE", "CA", "HT", "WT", "BORN", "BIRTHPLACE", "NAT"]),
        ];
        foreach (var (button, columns, skaterLabels, goalieLabels) in columnViews)
        {
            Click(Named<Button>(rosterView, button));
            Assert.Equal(columns == RosterColumns.Advanced, situation.IsEffectivelyVisible);
            var skaterHeaders = skaterTable.GetVisualDescendants().OfType<SortHeader>().Where(header => header.IsEffectivelyVisible).ToList();
            var goalieHeaders = goalieTable.GetVisualDescendants().OfType<SortHeader>().Where(header => header.IsEffectivelyVisible).ToList();
            Assert.Equal(skaterLabels, skaterHeaders.Select(header => header.Label));
            Assert.Equal(goalieLabels, goalieHeaders.Select(header => header.Label));
            Assert.Equal(RosterTableColumns.Skaters(columns).Select(column => column.Key), skaterHeaders.Select(header => header.Column));
            Assert.Equal(RosterTableColumns.Goalies(columns).Select(column => column.Key), goalieHeaders.Select(header => header.Column));
        }

        // Player groups show or hide the tables; the forwards group offers its position filter.
        var positionFilter = Named<ComboBox>(rosterView, "PositionFilter");
        Assert.True(skaterTable.IsEffectivelyVisible);
        Assert.True(goalieTable.IsEffectivelyVisible);
        Assert.False(positionFilter.IsEffectivelyVisible);
        Click(Named<Button>(rosterView, "ShowForwardsButton"));
        Assert.True(positionFilter.IsEffectivelyVisible);
        Assert.False(goalieTable.IsEffectivelyVisible);
        positionFilter.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ForwardFilter.Centres, rosterModel.ForwardPosition);
        Assert.All(rosterModel.Skaters, row => Assert.Equal(Position.Centre, row.Player.Position));
        Click(Named<Button>(rosterView, "ShowGoaliesButton"));
        Assert.False(skaterTable.IsEffectivelyVisible);
        Assert.True(goalieTable.IsEffectivelyVisible);
        Assert.False(positionFilter.IsEffectivelyVisible);

        // The strip under the tables shows the selected player, with links to the profile, the game log, and the lines.
        var strip = Named<PlayerStripView>(rosterView, "PlayerStripPanel");
        Assert.True(strip.IsEffectivelyVisible);
        Assert.Equal(rosterModel.SelectedStrip!.Name, Named<TextBlock>(strip, "StripNameText").Text);
        Assert.Equal("Goalie", Named<TextBlock>(strip, "StripPositionText").Text);
        Assert.True(Named<Button>(strip, "StripProfileLink").IsEffectivelyEnabled);
        Assert.True(Named<Button>(strip, "StripGameLogLink").IsEffectivelyEnabled);
        Assert.True(Named<Button>(strip, "StripLinesLink").IsEffectivelyEnabled);
        Assert.DoesNotContain(window.GetVisualDescendants(), visual => visual is PlayerProfilePageView);
        Click(Named<Button>(rosterView, "ShowAllPlayersButton"));
        Click(Named<Button>(rosterView, "ShowAdvancedButton"));
        rosterModel.SelectPlayer(rosterModel.Skaters[0].Player.Id);
        Dispatcher.UIThread.RunJobs();

        // Double-clicking a row opens that player's profile, on the overview.
        var skaterRow = Named<SkaterTableView>(rosterView, "SkaterTable").GetVisualDescendants().OfType<ListBoxItem>().First(item => item.IsEffectivelyVisible);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var released = new PointerReleasedEventArgs(
            skaterRow, pointer, window, new Point(5, 5), 0, new PointerPointProperties(), KeyModifiers.None, MouseButton.Left);
        skaterRow.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, released) { Source = skaterRow });
        Dispatcher.UIThread.RunJobs();

        var profileView = Single<PlayerProfilePageView>(window);
        var profile = Assert.IsType<PlayerProfilePageViewModel>(profileView.DataContext);
        Assert.Equal(rosterModel.Skaters[0].Player.Id, profile.Id);
        Assert.Equal(PlayerProfileTab.Overview, profile.Tab);
        Assert.True(profileView.Bounds.Width > 600);
        Assert.True(profileView.Bounds.Height > 300);

        // The player's own banner replaces the team's, and the profile's tabs the section's.
        Assert.DoesNotContain(shellView.GetVisualDescendants().OfType<TextBlock>(), text => text.Name == "ShellTeamName");
        Assert.Equal(profile.FullName.ToUpperInvariant(), Named<TextBlock>(shellView, "PlayerNameText").Text);
        Assert.Equal(profile.Number[1..], Named<TextBlock>(shellView, "PlayerNumberText").Text);
        Assert.Equal(3, Named<ItemsControl>(shellView, "PlayerFigures").ItemCount);
        Assert.Equal(seattlePrimary, Assert.IsAssignableFrom<ISolidColorBrush>(Named<Border>(shellView, "PlayerBanner").Background).Color);
        Assert.False(Named<Button>(shellView, "PlayerTeamLink").Content is null);
        Assert.False(Named<Border>(shellView, "PlayerInjuryBadge").IsEffectivelyVisible);
        var pageTabs = Named<ItemsControl>(shellView, "PageTabs");
        Assert.Equal(6, pageTabs.ItemCount);
        var tabButtons = pageTabs.GetVisualDescendants().OfType<Button>().ToList();
        Assert.Equal(["Overview", "Ratings", "Statistics", "Game log", "Health", "Career"], tabButtons.Select(button => button.Content));
        Assert.Equal([true, true, true, true, true, false], tabButtons.Select(button => button.IsEffectivelyEnabled));
        Assert.Equal("Seattle Evergreens", shellView.FindControl<TextBlock>("PageSubtitle")?.Text);

        // The overview: biography and usage, statistics with the last five games, and ratings.
        Assert.True(Named<Grid>(profileView, "OverviewTab").IsEffectivelyVisible);
        Assert.Equal(8, Named<ItemsControl>(profileView, "BiographyFacts").ItemCount);
        Assert.Equal(4, Named<ItemsControl>(profileView, "UsageFacts").ItemCount);
        Assert.Equal(4, Named<ItemsControl>(profileView, "OverviewStatistics").ItemCount);
        Assert.Equal(14, Named<ItemsControl>(profileView, "OverviewRatings").ItemCount);
        Assert.Equal(profile.StatisticsTitle.ToUpperInvariant(), Named<TextBlock>(profileView, "OverviewStatisticsTitle").Text);
        Assert.False(Named<Border>(profileView, "InjuredUntilPanel").IsEffectivelyVisible);
        foreach (var panel in new[] { "BiographyPanel", "UsagePanel", "StatisticsPanel", "OverviewRatingsPanel" })
        {
            Assert.True(Named<Border>(profileView, panel).Bounds.Height > 100);
        }

        // Each tab shows its page and hides the others.
        Click(tabButtons.Single(button => Equals(button.Content, "Ratings")));
        Assert.True(Named<Border>(profileView, "RatingsTab").IsEffectivelyVisible);
        Assert.False(Named<Grid>(profileView, "OverviewTab").IsEffectivelyVisible);
        Assert.Equal(3, Named<ItemsControl>(profileView, "RatingGroups").ItemCount);
        Click(tabButtons.Single(button => Equals(button.Content, "Statistics")));
        Assert.True(Named<Border>(profileView, "StatisticsTab").IsEffectivelyVisible);
        Assert.Equal(5, Named<ItemsControl>(profileView, "FullStatistics").ItemCount);
        Click(tabButtons.Single(button => Equals(button.Content, "Game log")));
        Assert.True(Named<Border>(profileView, "GameLogTab").IsEffectivelyVisible);
        Click(tabButtons.Single(button => Equals(button.Content, "Health")));
        Assert.True(Named<Border>(profileView, "HealthTab").IsEffectivelyVisible);
        Assert.True(Named<TextBlock>(profileView, "HealthyText").IsEffectivelyVisible);
        Click(tabButtons.Single(button => Equals(button.Content, "Overview")));
        Assert.True(Named<Grid>(profileView, "OverviewTab").IsEffectivelyVisible);

        // The team link opens the team; the roster's Game log link opens the profile on its game log.
        Click(Named<Button>(shellView, "PlayerTeamLink"));
        Assert.Single(window.GetVisualDescendants().OfType<TeamsPageView>());
        AssertNavigationRenders<RosterPageView>(window, ShellPage.Roster);
        Click(Named<Button>(Single<RosterPageView>(window), "StripGameLogLink"));
        profileView = Single<PlayerProfilePageView>(window);
        Assert.Equal(PlayerProfileTab.GameLog, Assert.IsType<PlayerProfilePageViewModel>(profileView.DataContext).Tab);
        Assert.True(Named<Border>(profileView, "GameLogTab").IsEffectivelyVisible);

        // Save from the Game menu under a typed name.
        Assert.Equal("Unsaved changes", Single<GameShellView>(window).FindControl<TextBlock>("SaveStatusText")?.Text);
        Click(MenuItemNamed(shellView, "SaveGameMenuItem"));
        Assert.True(shellView.FindControl<Border>("SaveDialogOverlay")?.IsVisible);
        var saveView = Single<SaveGameView>(window);
        Assert.True(saveView.Bounds.Width > 300);
        var saveName = Assert.IsType<TextBox>(saveView.FindControl<TextBox>("SaveNameTextBox"));
        Assert.Equal("Seattle Dynasty", saveName.Text);
        saveName.Text = "Opening Night";
        Click(Assert.IsType<Button>(saveView.FindControl<Button>("ConfirmSaveButton")));
        Assert.False(shellView.FindControl<Border>("SaveDialogOverlay")?.IsVisible);
        Assert.Equal("Opening Night", shellView.FindControl<TextBlock>("GameNameText")?.Text);
        Assert.Equal("Saved", shellView.FindControl<TextBlock>("SaveStatusText")?.Text);

        // Load from the Game menu, and go back to the game.
        Click(MenuItemNamed(shellView, "LoadGameMenuItem"));
        Click(Assert.IsType<LoadGameViewModel>(Single<LoadGameView>(window).DataContext).BackCommand);
        shellView = Single<GameShellView>(window);
        Assert.Same(viewModel.Game, shellView.DataContext);
        continueButton = Assert.IsType<Button>(shellView.FindControl<Button>("ContinueButton"));

        // Play on, then load the save from the main menu; the unsaved day must be confirmed away.
        ClickAndWait(continueButton, () => !viewModel.Game!.Session.IsAdvancing);
        Click(MenuItemNamed(shellView, "QuitToMenuItem"));
        Click(Assert.IsType<Button>(Single<StartupView>(window).FindControl<Button>("LoadGameMenuButton")));
        var loadView = Single<LoadGameView>(window);
        var savesList = Assert.IsType<ListBox>(loadView.FindControl<ListBox>("SavesList"));
        Assert.Equal(1, savesList.ItemCount);
        savesList.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();
        Click(Assert.IsType<Button>(loadView.FindControl<Button>("LoadSaveButton")));

        var confirmationOverlay = Assert.IsType<Border>(window.FindControl<Border>("ConfirmationOverlay"));
        Assert.True(confirmationOverlay.IsVisible);
        Assert.Equal("Discard unsaved progress?", window.FindControl<TextBlock>("ConfirmationTitle")?.Text);
        Assert.Equal("Discard progress", window.FindControl<Button>("ConfirmButton")?.Content);
        Click(Assert.IsType<Button>(window.FindControl<Button>("ConfirmButton")));
        Assert.False(confirmationOverlay.IsVisible);

        var loadedShell = Single<GameShellView>(window);
        Assert.Equal("Opening Night", loadedShell.FindControl<TextBlock>("GameNameText")?.Text);
        Assert.Equal(16, gameManager.GetSnapshot().Season.Results.Count);
        AssertNavigationRenders<StandingsPageView>(window, ShellPage.Standings);
        Assert.All(
            window.GetVisualDescendants().OfType<Border>().Select(border => border.DataContext).OfType<StandingsRowViewModel>(),
            row => Assert.Equal(1, row.GamesPlayed));

        // Closing the window with unsaved progress asks first instead of closing.
        ClickAndWait(Assert.IsType<Button>(loadedShell.FindControl<Button>("ContinueButton")), () => !viewModel.Game!.Session.IsAdvancing);
        window.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.True(window.IsVisible);
        Assert.True(confirmationOverlay.IsVisible);
        Click(Assert.IsType<Button>(window.FindControl<Button>("CancelConfirmationButton")));
        Assert.True(window.IsVisible);
        Assert.False(confirmationOverlay.IsVisible);
    }

    /// <summary>Counts the rows showing on screen whose data is a <typeparamref name="TRow"/>.</summary>
    private static int CountRows<TRow>(Visual view) =>
        view.GetVisualDescendants().OfType<Grid>().Count(grid => grid.DataContext is TRow && grid.IsEffectivelyVisible);

    private static int CountStandingsTables(Visual view) =>
        view.GetVisualDescendants().OfType<Border>().Count(border => border.DataContext is StandingsTableViewModel && border.Classes.Contains("panel"));

    private static int CountStandingsRows(Visual view) =>
        view.GetVisualDescendants().OfType<Border>().Count(border => border.DataContext is StandingsRowViewModel && border.Classes.Contains("standing-row"));

    private static void ClickAndWait(Button button, Func<bool> isDone)
    {
        Click(button);
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!isDone())
        {
            Assert.True(DateTime.UtcNow < deadline, "The command did not finish.");
            Thread.Sleep(5);
            Dispatcher.UIThread.RunJobs();
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Opens a page from the menu bar: its menu item, or the home button.</summary>
    private static void AssertNavigationRenders<TView>(Window window, ShellPage page)
        where TView : Control
    {
        var shellView = Single<GameShellView>(window);
        if (page == ShellPage.Home)
        {
            Click(Named<Button>(shellView, "HomeButton"));
        }
        else
        {
            Click(shellView.GetLogicalDescendants().OfType<MenuItem>().First(item => Equals(item.CommandParameter, page)));
        }

        var view = Single<TView>(window);
        Assert.True(view.Bounds.Width > 600);
        Assert.True(view.Bounds.Height > 400);
    }

    private static Color ResourceColor(Window window, string key) =>
        Assert.IsAssignableFrom<ISolidColorBrush>(window.FindResource(key)).Color;

    private static T Single<T>(Visual root)
        where T : Visual =>
        root.GetVisualDescendants().OfType<T>().Single();

    private static T Named<T>(Control root, string name)
        where T : Control =>
        Assert.IsType<T>(root.FindControl<T>(name) ?? root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name));

    /// <summary>Finds a menu item, which is not in the visual tree until its menu opens.</summary>
    private static MenuItem MenuItemNamed(Control root, string name) =>
        root.GetLogicalDescendants().OfType<MenuItem>().Single(item => item.Name == name);

    private static void Click(Button button)
    {
        button.Command?.Execute(button.CommandParameter);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(MenuItem item)
    {
        Assert.True(item.Command?.CanExecute(item.CommandParameter));
        item.Command!.Execute(item.CommandParameter);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(System.Windows.Input.ICommand command)
    {
        command.Execute(null);
        Dispatcher.UIThread.RunJobs();
    }
}