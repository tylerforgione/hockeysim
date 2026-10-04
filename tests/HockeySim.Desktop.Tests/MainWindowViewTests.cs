using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;

using HockeySim.Desktop;
using HockeySim.Desktop.Game;
using HockeySim.Desktop.Home;
using HockeySim.Desktop.Inbox;
using HockeySim.Desktop.Lines;
using HockeySim.Desktop.Main;
using HockeySim.Desktop.NewGame;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.Standings;
using HockeySim.Desktop.Startup;
using HockeySim.Desktop.Teams;
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
        var viewModel = new MainWindowViewModel(gameManager);
        var window = new MainWindow
        {
            DataContext = viewModel,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var startupView = Single<StartupView>(window);
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
        Assert.Equal("Seattle Evergreens", shellView.FindControl<TextBlock>("ShellTeamName")?.Text);
        var continueButton = Assert.IsType<Button>(shellView.FindControl<Button>("ContinueButton"));
        Assert.True(continueButton.IsEffectivelyEnabled);
        Assert.Single(window.GetVisualDescendants().OfType<HomePageView>());

        AssertNavigationRenders<InboxPageView>(window, ShellPage.Inbox);
        AssertNavigationRenders<RosterPageView>(window, ShellPage.Roster);
        AssertNavigationRenders<TeamsPageView>(window, ShellPage.Teams);
        AssertNavigationRenders<StandingsPageView>(window, ShellPage.Standings);
        AssertNavigationRenders<SchedulePageView>(window, ShellPage.Schedule);
        AssertNavigationRenders<LinesPageView>(window, ShellPage.Lines);

        var linesView = Single<LinesPageView>(window);
        var saveButton = Assert.IsType<Button>(linesView.FindControl<Button>("SaveLineupButton"));
        Assert.False(saveButton.IsEffectivelyEnabled);

        // Choose the scratched goalie as the starter through the rendered combo box.
        var starterCombo = Assert.IsType<ComboBox>(linesView.FindControl<ComboBox>("StartingGoalieSelector"));
        var scratchedGoalie = viewModel.Game!.Lines.Scratches.Single(player => player.PositionAbbreviation == "G");
        starterCombo.SelectedItem = scratchedGoalie;
        Dispatcher.UIThread.RunJobs();

        Assert.True(saveButton.IsEffectivelyEnabled);
        Click(saveButton);

        var snapshot = gameManager.GetSnapshot();
        Assert.Equal(
            scratchedGoalie.Id,
            snapshot.League.Teams.Single(team => team.Id == snapshot.ManagedTeamId).Lineup.StartingGoalieId);
        Assert.False(saveButton.IsEffectivelyEnabled);

        // Play opening night from the title bar, then open a result from the home page.
        AssertNavigationRenders<HomePageView>(window, ShellPage.Home);
        ClickAndWait(continueButton, () => !viewModel.Game!.Session.IsAdvancing);
        Assert.Equal(16, gameManager.GetSnapshot().Season.Results.Count);
        Assert.Contains("Oct", shellView.FindControl<TextBlock>("PhaseLabel")?.Text, StringComparison.Ordinal);
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

        // Standings: four division tables by default, then the single league table.
        AssertNavigationRenders<StandingsPageView>(window, ShellPage.Standings);
        var standingsView = Single<StandingsPageView>(window);
        Assert.Equal(32, CountStandingsRows(standingsView));
        Assert.Equal(4, standingsView.FindControl<ItemsControl>("StandingsTables")?.ItemCount);
        Click(Assert.IsType<Button>(standingsView.FindControl<Button>("LeagueScopeButton")));
        Assert.Equal(1, standingsView.FindControl<ItemsControl>("StandingsTables")?.ItemCount);
        Assert.Equal(32, CountStandingsRows(standingsView));
        Assert.All(
            standingsView.GetVisualDescendants().OfType<Border>().Select(border => border.DataContext).OfType<StandingsRowViewModel>(),
            row => Assert.Equal(1, row.GamesPlayed));

        // Roster: switch the tables to season totals; the detail panel always shows them.
        AssertNavigationRenders<RosterPageView>(window, ShellPage.Roster);
        var rosterView = Single<TeamRosterView>(window);
        var ratingsTable = Assert.IsType<ListBox>(rosterView.FindControl<ListBox>("SkatersTable"));
        var seasonTable = Assert.IsType<ListBox>(rosterView.FindControl<ListBox>("SkaterSeasonTable"));
        Assert.True(ratingsTable.IsEffectivelyVisible);
        Assert.False(seasonTable.IsEffectivelyVisible);
        Click(Assert.IsType<Button>(rosterView.FindControl<Button>("ShowSeasonButton")));
        Assert.False(ratingsTable.IsEffectivelyVisible);
        Assert.True(seasonTable.IsEffectivelyVisible);
        Assert.True(seasonTable.Bounds.Height > 200);
        Assert.True(Assert.IsType<ListBox>(rosterView.FindControl<ListBox>("GoalieSeasonTable")).IsEffectivelyVisible);
        var seasonStatistics = Single<PlayerDetailView>(rosterView).FindControl<StackPanel>("SeasonStatistics");
        Assert.True(seasonStatistics?.IsEffectivelyVisible);
        Assert.Equal(
            ["GP", "G", "A", "P"],
            seasonStatistics!.GetVisualDescendants().OfType<TextBlock>()
                .Where(text => text.DataContext is SeasonStatViewModel && text.Classes.Contains("label"))
                .Select(text => text.Text));
    }

    private static int CountStandingsRows(Visual view) =>
        view.GetVisualDescendants().OfType<Border>().Count(border => border.DataContext is StandingsRowViewModel);

    private static void ClickAndWait(Button button, Func<bool> isDone)
    {
        Click(button);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!isDone())
        {
            Assert.True(DateTime.UtcNow < deadline, "The command did not finish.");
            Thread.Sleep(5);
            Dispatcher.UIThread.RunJobs();
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertNavigationRenders<TView>(Window window, ShellPage page)
        where TView : Control
    {
        var navButton = window.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.DataContext is NavigationItemViewModel item && item.Page == page);
        Click(navButton);

        var view = Single<TView>(window);
        Assert.True(view.Bounds.Width > 600);
        Assert.True(view.Bounds.Height > 400);
    }

    private static T Single<T>(Visual root)
        where T : Visual =>
        root.GetVisualDescendants().OfType<T>().Single();

    private static void Click(Button button)
    {
        button.Command?.Execute(button.CommandParameter);
        Dispatcher.UIThread.RunJobs();
    }
}