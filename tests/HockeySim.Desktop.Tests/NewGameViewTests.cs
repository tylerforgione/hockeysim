using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

using HockeySim.Desktop;
using HockeySim.Desktop.NewGame;
using HockeySim.Management.GameManagement;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class NewGameViewTests
{
    [Fact]
    public void ViewBindsTeamChoicesAndCreatedState()
    {
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .SetupWithoutStarting();

        var viewModel = new NewGameViewModel(new GameManager())
        {
            SelectedTeamName = "Seattle Evergreens",
        };
        var view = new NewGameView
        {
            DataContext = viewModel,
        };
        var window = new Window
        {
            Width = 880,
            Height = 720,
            Content = view,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var teamPicker = Assert.IsType<ComboBox>(view.FindControl<ComboBox>("TeamPicker"));
        var createGameButton = Assert.IsType<Button>(
            view.FindControl<Button>("CreateGameButton"));
        var successPanel = Assert.IsType<StackPanel>(
            view.FindControl<StackPanel>("SuccessPanel"));

        Assert.Equal(32, teamPicker.Items.Cast<object>().Count());
        Assert.Equal("Seattle Evergreens", teamPicker.SelectedItem);
        Assert.False(successPanel.IsVisible);

        Assert.NotNull(createGameButton.Command);
        createGameButton.Command.Execute(createGameButton.CommandParameter);
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.IsGameCreated);
        Assert.True(successPanel.IsVisible);
    }
}