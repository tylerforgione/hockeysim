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
using HockeySim.Desktop.Roster;
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
        Assert.False(shellView.FindControl<Button>("ContinueButton")?.IsEnabled);
        Assert.Single(window.GetVisualDescendants().OfType<HomePageView>());

        AssertNavigationRenders<InboxPageView>(window, ShellPage.Inbox);
        AssertNavigationRenders<RosterPageView>(window, ShellPage.Roster);
        AssertNavigationRenders<TeamsPageView>(window, ShellPage.Teams);
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