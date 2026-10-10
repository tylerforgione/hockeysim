using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
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
        Assert.False(shellView.FindControl<Button>("SimulateAheadButton")?.IsEffectivelyEnabled);

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

        // Play the preseason and opening night from the menu bar, then open a result from the
        // home page.
        AssertNavigationRenders<HomePageView>(window, ShellPage.Home);
        Assert.Contains(" · Regular season starts ", shellView.FindControl<TextBlock>("CalendarLabel")?.Text, StringComparison.Ordinal);
        while (gameManager.GetSnapshot().Season.Phase == SeasonPhase.Preseason)
        {
            ClickAndWait(continueButton, () => !viewModel.Game!.Session.IsAdvancing);
        }

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
            .Where(button => button.DataContext is LeagueResultRowViewModel)
            .ToList();
        Assert.Equal(16, resultButtons.Count);
        Click(resultButtons[0]);

        var matchDetail = Single<MatchDetailView>(window);
        Assert.True(matchDetail.Bounds.Width > 300);
        Assert.StartsWith("Final", matchDetail.FindControl<TextBlock>("FinalLabel")?.Text, StringComparison.Ordinal);
        var skaterRows = matchDetail.GetVisualDescendants()
            .OfType<Grid>()
            .Count(grid => grid.DataContext is SkaterBoxScoreRowViewModel);
        Assert.Equal(36, skaterRows);
        var detailViewModel = Assert.IsType<MatchDetailViewModel>(matchDetail.DataContext);
        var goalRows = matchDetail.GetVisualDescendants()
            .OfType<Grid>()
            .Count(grid => grid.DataContext is GoalSummaryRowViewModel);
        Assert.True(matchDetail.FindControl<StackPanel>("ScoringSummary")?.IsEffectivelyVisible);
        Assert.Equal(detailViewModel.ScoringSummary.Count, goalRows);
        Assert.Equal(
            detailViewModel.PenaltySummary.Count,
            matchDetail.GetVisualDescendants().OfType<Grid>().Count(grid => grid.DataContext is PenaltySummaryRowViewModel));

        // Standings: four division tables by default with the marker legend, then the wild-card
        // view's four division leader tables and two races, then the single league table.
        AssertNavigationRenders<StandingsPageView>(window, ShellPage.Standings);
        var standingsView = Single<StandingsPageView>(window);
        Assert.Equal(32, CountStandingsRows(standingsView));
        Assert.Equal(4, standingsView.FindControl<ItemsControl>("StandingsTables")?.ItemCount);
        Assert.True(standingsView.FindControl<TextBlock>("PlayoffStatusLegend")?.IsEffectivelyVisible);
        Click(Assert.IsType<Button>(standingsView.FindControl<Button>("WildCardScopeButton")));
        Assert.Equal(6, standingsView.FindControl<ItemsControl>("StandingsTables")?.ItemCount);
        Assert.Equal(32, CountStandingsRows(standingsView));
        Click(Assert.IsType<Button>(standingsView.FindControl<Button>("LeagueScopeButton")));
        Assert.Equal(1, standingsView.FindControl<ItemsControl>("StandingsTables")?.ItemCount);
        Assert.Equal(32, CountStandingsRows(standingsView));
        Assert.All(
            standingsView.GetVisualDescendants().OfType<Border>().Select(border => border.DataContext).OfType<StandingsRowViewModel>(),
            row => Assert.Equal(1, row.GamesPlayed));

        // Roster: the tables switched to basic and advanced statistics; the detail panel always
        // shows the full line.
        AssertNavigationRenders<RosterPageView>(window, ShellPage.Roster);
        var rosterView = Single<TeamRosterView>(window);
        var ratingsTable = Assert.IsType<ListBox>(rosterView.FindControl<ListBox>("SkatersTable"));
        var basicTable = Assert.IsType<ListBox>(rosterView.FindControl<ListBox>("SkaterBasicTable"));
        var advancedTable = Assert.IsType<ListBox>(rosterView.FindControl<ListBox>("SkaterAdvancedTable"));
        Assert.True(ratingsTable.IsEffectivelyVisible);
        Assert.False(basicTable.IsEffectivelyVisible);
        Click(Assert.IsType<Button>(rosterView.FindControl<Button>("ShowBasicButton")));
        Assert.False(ratingsTable.IsEffectivelyVisible);
        Assert.True(basicTable.IsEffectivelyVisible);
        Assert.True(basicTable.Bounds.Height > 200);
        Assert.True(Assert.IsType<ListBox>(rosterView.FindControl<ListBox>("GoalieBasicTable")).IsEffectivelyVisible);
        Click(Assert.IsType<Button>(rosterView.FindControl<Button>("ShowAdvancedButton")));
        Assert.False(basicTable.IsEffectivelyVisible);
        Assert.True(advancedTable.IsEffectivelyVisible);
        Assert.True(Assert.IsType<ListBox>(rosterView.FindControl<ListBox>("GoalieAdvancedTable")).IsEffectivelyVisible);
        var playerDetail = Single<PlayerDetailView>(rosterView);
        var overallText = playerDetail.FindControl<TextBlock>("PlayerOverallText");
        Assert.True(overallText?.IsEffectivelyVisible);
        Assert.Equal(
            Assert.IsType<PlayerDetailViewModel>(playerDetail.DataContext).Overall.ToString(CultureInfo.InvariantCulture),
            overallText!.Text);
        Assert.Equal(
            Assert.IsType<PlayerDetailViewModel>(playerDetail.DataContext).Health,
            playerDetail.FindControl<TextBlock>("HealthText")?.Text);
        var seasonStatistics = playerDetail.FindControl<StackPanel>("SeasonStatistics");
        Assert.True(seasonStatistics?.IsEffectivelyVisible);
        Assert.Equal(
            ["GP", "G", "A", "P", "+/-", "PIM", "ENG"],
            seasonStatistics!.GetVisualDescendants().OfType<TextBlock>()
                .Where(text => text.DataContext is SeasonStatViewModel && text.Classes.Contains("label"))
                .Select(text => text.Text)
                .Take(7));

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

    private static int CountStandingsRows(Visual view) =>
        view.GetVisualDescendants().OfType<Border>().Count(border => border.DataContext is StandingsRowViewModel);

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