using HockeySim.Desktop.NewGame;
using HockeySim.Management.GameManagement;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class NewGameViewModelTests
{
    [Fact]
    public void InitialStateOffersEveryTeamAndReproducibleDefaults()
    {
        var viewModel = new NewGameViewModel(new GameManager());

        Assert.Equal("2026", viewModel.SeasonYear);
        Assert.Equal("2026", viewModel.WorldSeed);
        Assert.Equal(32, viewModel.TeamNames.Count);
        Assert.Equal("Halifax Mariners", viewModel.SelectedTeamName);
        Assert.False(viewModel.IsGameCreated);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public void CreateGameBuildsTheSelectedTeamWorldAndSummary()
    {
        var gameManager = new GameManager();
        var viewModel = new NewGameViewModel(gameManager)
        {
            SeasonYear = "2032",
            WorldSeed = "987654321",
            SelectedTeamName = "Seattle Evergreens",
        };

        viewModel.CreateGameCommand.Execute(null);

        var snapshot = gameManager.GetSnapshot();
        Assert.True(viewModel.IsGameCreated);
        Assert.False(viewModel.HasError);
        Assert.Equal("Seattle Evergreens", viewModel.CreatedTeamName);
        Assert.Equal("2032 season", viewModel.CreatedSeason);
        Assert.Equal(32, viewModel.CreatedTeamCount);
        Assert.Equal(736, viewModel.CreatedPlayerCount);
        Assert.Equal(2032, snapshot.League.SeasonYear);
        Assert.Equal(
            "Seattle Evergreens",
            snapshot.League.Teams.Single(team => team.Id == snapshot.ManagedTeamId).Name);
    }

    [Fact]
    public void InvalidSeedShowsAnActionableErrorWithoutCreatingAGame()
    {
        var gameManager = new GameManager();
        var viewModel = new NewGameViewModel(gameManager)
        {
            WorldSeed = "not a number",
        };

        viewModel.CreateGameCommand.Execute(null);

        Assert.False(viewModel.IsGameCreated);
        Assert.True(viewModel.HasError);
        Assert.Contains("whole-number world seed", viewModel.ErrorMessage);
        Assert.Throws<InvalidOperationException>(gameManager.GetSnapshot);
    }
}