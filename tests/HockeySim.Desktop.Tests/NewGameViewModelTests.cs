using HockeySim.Desktop.Main;
using HockeySim.Desktop.NewGame;
using HockeySim.Management.GameManagement;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class NewGameViewModelTests
{
    [Fact]
    public void InitialStateGroupsEveryTeamByConferenceAndDivision()
    {
        var viewModel = new NewGameViewModel(new GameManager());

        Assert.Empty(viewModel.GameName);
        Assert.Null(viewModel.SelectedTeamName);
        Assert.Equal("No team selected", viewModel.SelectedTeamDisplayName);
        Assert.Equal(2, viewModel.Conferences.Count);
        Assert.All(viewModel.Conferences, conference =>
        {
            Assert.Equal(2, conference.Divisions.Count);
            Assert.All(conference.Divisions, division => Assert.Equal(8, division.Teams.Count));
        });
        Assert.False(viewModel.IsGameCreated);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public void SelectingATeamMarksOnlyThatTeam()
    {
        var viewModel = new NewGameViewModel(new GameManager());
        var teams = GetTeams(viewModel);
        var selectedTeam = teams.Single(team => team.Name == "Seattle Evergreens");

        selectedTeam.SelectCommand.Execute(null);

        Assert.Equal("Seattle Evergreens", viewModel.SelectedTeamName);
        Assert.True(selectedTeam.IsSelected);
        Assert.Single(teams, team => team.IsSelected);
    }

    [Fact]
    public void CreateGameBuildsTheSelectedTeamWorldAndKeepsTheGameName()
    {
        var gameManager = new GameManager();
        var viewModel = new NewGameViewModel(gameManager)
        {
            GameName = "  Seattle Dynasty  ",
        };
        GetTeams(viewModel)
            .Single(team => team.Name == "Seattle Evergreens")
            .SelectCommand.Execute(null);

        viewModel.CreateGameCommand.Execute(null);

        var snapshot = gameManager.GetSnapshot();
        Assert.True(viewModel.IsGameCreated);
        Assert.False(viewModel.HasError);
        Assert.Equal("Seattle Dynasty", viewModel.CreatedGameName);
        Assert.Equal("Seattle Evergreens", viewModel.CreatedTeamName);
        Assert.Equal(2026, snapshot.League.SeasonYear);
        Assert.Equal(
            "Seattle Evergreens",
            snapshot.League.Teams.Single(team => team.Id == snapshot.ManagedTeamId).Name);
    }

    [Theory]
    [InlineData("", "Enter a name")]
    [InlineData("Named game", "Select the team")]
    public void MissingRequiredChoiceShowsAnActionableError(string gameName, string expectedMessage)
    {
        var gameManager = new GameManager();
        var viewModel = new NewGameViewModel(gameManager)
        {
            GameName = gameName,
        };

        viewModel.CreateGameCommand.Execute(null);

        Assert.False(viewModel.IsGameCreated);
        Assert.True(viewModel.HasError);
        Assert.Contains(expectedMessage, viewModel.ErrorMessage);
        Assert.Throws<InvalidOperationException>(gameManager.GetSnapshot);
    }

    [Fact]
    public void StartupMenuNavigatesToNewGameAndCanExit()
    {
        var exitWasRequested = false;
        var viewModel = new MainWindowViewModel(
            new GameManager(),
            () => exitWasRequested = true);

        Assert.True(viewModel.IsStartupVisible);
        Assert.False(viewModel.IsNewGameVisible);

        viewModel.Startup.ShowNewGameCommand.Execute(null);

        Assert.False(viewModel.IsStartupVisible);
        Assert.True(viewModel.IsNewGameVisible);
        Assert.False(viewModel.Startup.ContinueCommand.CanExecute(null));

        viewModel.NewGame.BackCommand.Execute(null);
        viewModel.Startup.ExitCommand.Execute(null);

        Assert.True(viewModel.IsStartupVisible);
        Assert.True(exitWasRequested);
    }

    private static IReadOnlyList<TeamOptionViewModel> GetTeams(NewGameViewModel viewModel) =>
        viewModel.Conferences
            .SelectMany(conference => conference.Divisions)
            .SelectMany(division => division.Teams)
            .ToList();
}