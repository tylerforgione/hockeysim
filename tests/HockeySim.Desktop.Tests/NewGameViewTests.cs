using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;

using HockeySim.Desktop;
using HockeySim.Desktop.Main;
using HockeySim.Desktop.NewGame;
using HockeySim.Desktop.Startup;
using HockeySim.Management.GameManagement;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class NewGameViewTests
{
    [Fact]
    public void MainWindowNavigatesAndBindsEveryTeamAtDesktopSize()
    {
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true,
            })
            .SetupWithoutStarting();

        var viewModel = new MainWindowViewModel(new GameManager());
        var window = new MainWindow
        {
            DataContext = viewModel,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var startupView = window.GetVisualDescendants().OfType<StartupView>().Single();
        var newGameMenuButton = Assert.IsType<Button>(
            startupView.FindControl<Button>("NewGameMenuButton"));
        newGameMenuButton.Command?.Execute(newGameMenuButton.CommandParameter);
        Dispatcher.UIThread.RunJobs();

        var newGameView = window.GetVisualDescendants().OfType<NewGameView>().Single();
        var conferences = Assert.IsType<ItemsControl>(
            newGameView.FindControl<ItemsControl>("ConferencesList"));
        var teamButtons = newGameView.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.DataContext is TeamOptionViewModel)
            .ToList();
        var seattleButton = teamButtons.Single(button => button.Content is string content
            && string.Equals(content, "Seattle Evergreens", StringComparison.Ordinal));

        Assert.Equal(2, conferences.Items.Cast<object>().Count());
        Assert.Equal(32, teamButtons.Count);
        Assert.All(teamButtons, button =>
        {
            Assert.True(button.Bounds.Width > 80);
            Assert.True(button.Bounds.Height > 20);
        });

        seattleButton.Command?.Execute(seattleButton.CommandParameter);
        viewModel.NewGame.GameName = "Seattle Dynasty";

        var createGameButton = Assert.IsType<Button>(
            newGameView.FindControl<Button>("CreateGameButton"));
        createGameButton.Command?.Execute(createGameButton.CommandParameter);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Seattle Evergreens", viewModel.NewGame.SelectedTeamName);
        Assert.True(viewModel.NewGame.IsGameCreated);
        Assert.True(viewModel.Startup.ContinueCommand.CanExecute(null));
    }
}