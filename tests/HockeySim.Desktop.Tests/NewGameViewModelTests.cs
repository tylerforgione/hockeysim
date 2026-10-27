using System.Globalization;

using HockeySim.Desktop.Main;
using HockeySim.Desktop.NewGame;
using HockeySim.Management.GameManagement;
using HockeySim.Management.NewGame;
using HockeySim.Management.Saves;
using HockeySim.Simulation.Randomness;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class NewGameViewModelTests
{
    private const ulong Seed = 2468;
    private const string TeamName = "Seattle Evergreens";

    [Fact]
    public void InitialStateIsTheLeagueStepWithNoPreview()
    {
        var viewModel = new NewGameViewModel(new GameManager());

        Assert.Equal(NewGameStep.League, viewModel.Step);
        Assert.True(viewModel.IsLeagueStep);
        Assert.False(viewModel.BackCommand.CanExecute(null));
        Assert.True(viewModel.NextCommand.CanExecute(null));
        Assert.True(viewModel.IsNextVisible);
        Assert.Empty(viewModel.Conferences);
        Assert.False(viewModel.HasSelectedTeam);
        Assert.Empty(viewModel.WorldSeedText);
        Assert.Equal("2026–27", viewModel.SeasonLabel);
        Assert.Equal("32 teams · 2 conferences · 4 divisions · generated players", viewModel.LeagueSummary);
        Assert.Equal(["League", "Team", "Manager"], viewModel.Steps.Select(step => step.Title));
        Assert.Equal(["1", "2", "3"], viewModel.Steps.Select(step => step.Marker));
        Assert.Equal([true, false, false], viewModel.Steps.Select(step => step.IsCurrent));
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public void ATypedSeedBuildsThePreviewForThatSeed()
    {
        var gameManager = new GameManager();
        var viewModel = AtTeamStep(gameManager);

        var expected = gameManager.PreviewNewGame(2026, new RandomState(Seed));
        Assert.Equal(NewGameStep.Team, viewModel.Step);
        Assert.Equal(2, viewModel.Conferences.Count);
        Assert.All(viewModel.Conferences, conference =>
        {
            Assert.Equal(2, conference.Divisions.Count);
            Assert.All(conference.Divisions, division => Assert.Equal(8, division.Teams.Count));
        });
        var rows = GetTeams(viewModel);
        Assert.Equal(expected.Teams.Select(team => team.Name), rows.Select(team => team.Name));
        Assert.Equal(expected.Teams.Select(team => team.Overall), rows.Select(team => team.Overall));
        Assert.Throws<InvalidOperationException>(gameManager.GetSnapshot);
    }

    [Fact]
    public void StartingTheGameCreatesThePreviewedWorld()
    {
        var gameManager = new GameManager();
        var viewModel = AtTeamStep(gameManager);
        var expected = gameManager.PreviewNewGame(2026, new RandomState(Seed)).Teams.Single(team => team.Name == TeamName);
        Select(viewModel, TeamName);
        viewModel.NextCommand.Execute(null);
        viewModel.ManagerName = "Pat";

        viewModel.StartGameCommand.Execute(null);

        var snapshot = gameManager.GetSnapshot();
        var team = snapshot.League.Teams.Single(candidate => candidate.Id == snapshot.ManagedTeamId);
        Assert.Equal(TeamName, team.Name);
        Assert.All(expected.KeyPlayers.Where(player => player.Position != HockeySim.Domain.Position.Goalie), key =>
            Assert.Contains(team.Roster, player =>
                player.FirstName == key.FirstName && player.LastName == key.LastName && player.Number == key.Number && player.Overall == key.Overall));
        Assert.Contains(team.Roster, player =>
            player.FirstName == expected.KeyPlayers[4].FirstName && player.LastName == expected.KeyPlayers[4].LastName);
    }

    [Fact]
    public void ABlankSeedDrawsARandomStateShownInTheSummary()
    {
        var viewModel = new NewGameViewModel(new GameManager());

        viewModel.NextCommand.Execute(null);
        Select(viewModel, TeamName);
        viewModel.NextCommand.Execute(null);

        Assert.Equal(NewGameStep.Manager, viewModel.Step);
        Assert.True(ulong.TryParse(viewModel.SummarySeed, CultureInfo.CurrentCulture, out _));
        Assert.Equal("Fictional · 2026–27", viewModel.SummaryLeague);
        Assert.Equal(TeamName, viewModel.SummaryTeam);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("18446744073709551616")]
    public void AnInvalidSeedShowsAnErrorAndStaysOnTheLeagueStep(string seed)
    {
        var viewModel = new NewGameViewModel(new GameManager()) { WorldSeedText = seed };

        viewModel.NextCommand.Execute(null);

        Assert.Equal(NewGameStep.League, viewModel.Step);
        Assert.Equal("The world seed must be a whole number from 0 to 18446744073709551615.", viewModel.ErrorMessage);
        Assert.Empty(viewModel.Conferences);
    }

    [Fact]
    public void SurroundingWhitespaceAroundASeedIsAllowed()
    {
        var viewModel = new NewGameViewModel(new GameManager()) { WorldSeedText = $"  {Seed}  " };

        viewModel.NextCommand.Execute(null);

        Assert.Equal(NewGameStep.Team, viewModel.Step);
        Assert.Equal(Seed.ToString(CultureInfo.CurrentCulture), viewModel.SummarySeed);
    }

    [Fact]
    public void AnUnchangedSeedKeepsThePreviewAndAChangedSeedRegeneratesIt()
    {
        var viewModel = AtTeamStep(new GameManager());
        Select(viewModel, TeamName);
        var firstTeams = viewModel.Conferences;

        viewModel.BackCommand.Execute(null);
        viewModel.NextCommand.Execute(null);
        Assert.Same(firstTeams, viewModel.Conferences);
        Assert.Equal(TeamName, viewModel.SelectedTeam!.Name);

        viewModel.BackCommand.Execute(null);
        viewModel.WorldSeedText = "13579";
        viewModel.NextCommand.Execute(null);

        Assert.NotSame(firstTeams, viewModel.Conferences);
        Assert.Equal("13579", viewModel.SummarySeed);
        Assert.Equal(TeamName, viewModel.SelectedTeam!.Name);
        Assert.Single(GetTeams(viewModel), team => team.IsSelected);
        var refreshed = new GameManager().PreviewNewGame(2026, new RandomState(13579)).Teams.Single(team => team.Name == TeamName);
        Assert.Equal(refreshed.Overall, viewModel.SelectedTeam.Overall);
    }

    [Fact]
    public void ABlankSeedStaysTheSameRandomStateWhileUnchanged()
    {
        var viewModel = new NewGameViewModel(new GameManager());
        viewModel.NextCommand.Execute(null);
        var seed = viewModel.SummarySeed;

        viewModel.BackCommand.Execute(null);
        viewModel.NextCommand.Execute(null);

        Assert.Equal(seed, viewModel.SummarySeed);
    }

    [Fact]
    public void NextOnTheTeamStepNeedsASelectedTeam()
    {
        var viewModel = AtTeamStep(new GameManager());

        Assert.False(viewModel.NextCommand.CanExecute(null));

        Select(viewModel, TeamName);

        Assert.True(viewModel.NextCommand.CanExecute(null));
    }

    [Fact]
    public void SelectingATeamMarksOnlyThatTeam()
    {
        var viewModel = AtTeamStep(new GameManager());
        Select(viewModel, "Ottawa Owls");

        Select(viewModel, TeamName);

        var teams = GetTeams(viewModel);
        Assert.Equal(TeamName, viewModel.SelectedTeam!.Name);
        Assert.Equal(TeamName, Assert.Single(teams, team => team.IsSelected).Name);
        Assert.Equal("SE", viewModel.SelectedTeam.Initials);
        Assert.Equal("Pacific Division · Western Conference", viewModel.SelectedTeam.Location);
    }

    [Fact]
    public void TheTeamPreviewShowsTheTeamsFigures()
    {
        var gameManager = new GameManager();
        var viewModel = AtTeamStep(gameManager);
        var expected = gameManager.PreviewNewGame(2026, new RandomState(Seed)).Teams.Single(team => team.Name == TeamName);

        Select(viewModel, TeamName);

        var preview = viewModel.SelectedTeam!;
        Assert.Equal(expected.Overall, preview.Overall);
        Assert.Equal(TeamPreviewViewModel.OutlookName(expected.Outlook), preview.OutlookText);
        Assert.Equal(expected.AverageAge.ToString("0.0", CultureInfo.CurrentCulture), preview.AverageAgeText);
        Assert.Equal(["C", "W", "D", "D", "G"], preview.KeyPlayers.Select(player => player.Position));
        Assert.Equal(
            expected.KeyPlayers.Select(player => $"{player.FirstName} {player.LastName}"),
            preview.KeyPlayers.Select(player => player.Name));
        Assert.Equal(expected.KeyPlayers.Select(player => player.Overall), preview.KeyPlayers.Select(player => player.Overall));
        Assert.Equal(["Forwards", "Defence", "Goalies"], preview.Ratings.Select(rating => rating.Label));
        Assert.Equal([expected.Forwards, expected.Defence, expected.Goalies], preview.Ratings.Select(rating => rating.Value));
    }

    [Theory]
    [InlineData(TeamOutlook.Contender, "Contender")]
    [InlineData(TeamOutlook.PlayoffBubble, "Playoff bubble")]
    [InlineData(TeamOutlook.Middle, "Middle")]
    [InlineData(TeamOutlook.Rebuild, "Rebuild")]
    public void OutlooksReadAsLabels(TeamOutlook outlook, string expected)
    {
        Assert.Equal(expected, TeamPreviewViewModel.OutlookName(outlook));
    }

    [Fact]
    public void StepHeadersOnlyReachStepsAlreadyReached()
    {
        var viewModel = new NewGameViewModel(new GameManager()) { WorldSeedText = Seed.ToString(CultureInfo.InvariantCulture) };
        var steps = viewModel.Steps;

        Assert.False(steps[1].OpenCommand.CanExecute(null));
        Assert.False(steps[2].OpenCommand.CanExecute(null));

        viewModel.NextCommand.Execute(null);

        Assert.True(steps[0].OpenCommand.CanExecute(null));
        Assert.True(steps[1].OpenCommand.CanExecute(null));
        Assert.False(steps[2].OpenCommand.CanExecute(null));
        Assert.True(steps[0].IsDone);
        Assert.Equal("✓", steps[0].Marker);
        Assert.True(steps[1].IsCurrent);

        Select(viewModel, TeamName);
        Assert.True(steps[2].OpenCommand.CanExecute(null));

        steps[2].OpenCommand.Execute(null);
        Assert.Equal(NewGameStep.Manager, viewModel.Step);

        steps[0].OpenCommand.Execute(null);
        Assert.Equal(NewGameStep.League, viewModel.Step);
        Assert.False(viewModel.BackCommand.CanExecute(null));
    }

    [Fact]
    public void TheManagerStepReplacesNextWithStartGame()
    {
        var viewModel = AtManagerStep(new GameManager());

        Assert.False(viewModel.IsNextVisible);
        Assert.True(viewModel.IsManagerStep);
        Assert.True(viewModel.BackCommand.CanExecute(null));
        Assert.False(viewModel.NextCommand.CanExecute(null));
    }

    [Fact]
    public void TheSaveNameDefaultsToTheTeamAndFollowsItUntilEdited()
    {
        var viewModel = AtTeamStep(new GameManager());

        Select(viewModel, "Ottawa Owls");
        Assert.Equal("Ottawa Owls", viewModel.SaveName);
        Select(viewModel, TeamName);
        Assert.Equal(TeamName, viewModel.SaveName);

        viewModel.SaveName = "My Career";
        Select(viewModel, "Ottawa Owls");

        Assert.Equal("My Career", viewModel.SaveName);
    }

    [Fact]
    public void StartingTheGameStoresTheManagerNameAndHandsOverTheTrimmedSaveName()
    {
        var gameManager = new GameManager();
        string? createdName = null;
        var viewModel = AtManagerStep(gameManager, gameCreated: name => createdName = name);
        viewModel.ManagerName = "  Pat Quinn  ";
        viewModel.SaveName = "  Seattle Dynasty  ";

        viewModel.StartGameCommand.Execute(null);

        Assert.False(viewModel.HasError);
        Assert.Equal("Seattle Dynasty", createdName);
        Assert.Equal("Pat Quinn", gameManager.GetSnapshot().ManagerName);
        Assert.Equal(2026, gameManager.GetSnapshot().League.SeasonYear);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AManagerNameIsRequired(string managerName)
    {
        var gameManager = new GameManager();
        var gameWasCreated = false;
        var viewModel = AtManagerStep(gameManager, gameCreated: _ => gameWasCreated = true);
        viewModel.ManagerName = managerName;

        viewModel.StartGameCommand.Execute(null);

        Assert.False(gameWasCreated);
        Assert.Equal("Enter your name.", viewModel.ErrorMessage);
        Assert.Throws<InvalidOperationException>(gameManager.GetSnapshot);
    }

    [Fact]
    public void AManagerNameThatIsTooLongShowsManagementsMessage()
    {
        var gameManager = new GameManager();
        var viewModel = AtManagerStep(gameManager);
        viewModel.ManagerName = new string('a', NewGameCommand.MaximumManagerNameLength + 1);

        viewModel.StartGameCommand.Execute(null);

        Assert.Contains("longer than", viewModel.ErrorMessage);
        Assert.Throws<InvalidOperationException>(gameManager.GetSnapshot);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Leafs 2026/27")]
    [InlineData("A very long game name that goes well past the sixty characters a save allows")]
    public void ASaveNameThatCannotBeSavedIsRejectedAndNoGameIsCreated(string saveName)
    {
        var gameManager = new GameManager();
        var gameWasCreated = false;
        var viewModel = AtManagerStep(gameManager, gameCreated: _ => gameWasCreated = true);
        viewModel.SaveName = saveName;

        viewModel.StartGameCommand.Execute(null);

        SaveName.TryParse(saveName, out _, out var expectedError);
        Assert.False(gameWasCreated);
        Assert.Equal(expectedError, viewModel.ErrorMessage);
        Assert.Throws<InvalidOperationException>(gameManager.GetSnapshot);
    }

    [Fact]
    public void TheDefaultSaveNameIsAValidSaveName()
    {
        string? createdName = null;
        var viewModel = AtManagerStep(new GameManager(), gameCreated: name => createdName = name);

        viewModel.StartGameCommand.Execute(null);

        Assert.True(SaveName.TryParse(createdName, out var name, out _));
        Assert.Equal(createdName, name.Value);
    }

    [Fact]
    public void CancelReturnsToTheStartupMenu()
    {
        var startupWasShown = false;
        var viewModel = new NewGameViewModel(new GameManager(), () => startupWasShown = true);

        viewModel.CancelCommand.Execute(null);

        Assert.True(startupWasShown);
    }

    [Fact]
    public void StartupMenuNavigatesToNewGameAndCanExit()
    {
        var exitWasRequested = false;
        using var saves = new TemporarySaveDirectory();
        var viewModel = new MainWindowViewModel(
            new GameManager(),
            saves.Library,
            () => exitWasRequested = true);

        Assert.True(viewModel.IsStartupVisible);
        Assert.False(viewModel.IsNewGameVisible);

        viewModel.Startup.ShowNewGameCommand.Execute(null);

        Assert.False(viewModel.IsStartupVisible);
        Assert.True(viewModel.IsNewGameVisible);
        Assert.False(viewModel.Startup.ContinueCommand.CanExecute(null));

        viewModel.NewGame!.CancelCommand.Execute(null);
        viewModel.Startup.ExitCommand.Execute(null);

        Assert.True(viewModel.IsStartupVisible);
        Assert.True(exitWasRequested);
    }

    [Fact]
    public void CreatingAGameOpensTheGameAndMainMenuCanContinueIt()
    {
        using var saves = new TemporarySaveDirectory();
        var viewModel = new MainWindowViewModel(new GameManager(), saves.Library);
        viewModel.Startup.ShowNewGameCommand.Execute(null);
        var newGame = viewModel.NewGame!;
        newGame.WorldSeedText = Seed.ToString(CultureInfo.InvariantCulture);
        newGame.NextCommand.Execute(null);
        Select(newGame, "Ottawa Owls");
        newGame.NextCommand.Execute(null);
        newGame.ManagerName = "Pat";
        newGame.SaveName = "Ottawa Rebuild";

        newGame.StartGameCommand.Execute(null);

        Assert.True(viewModel.IsGameVisible);
        Assert.Equal("Ottawa Owls", viewModel.Game!.TeamName);
        Assert.Equal("Ottawa Rebuild", viewModel.Game.GameName);

        viewModel.Game.ShowMainMenuCommand.Execute(null);
        Assert.True(viewModel.IsStartupVisible);
        Assert.True(viewModel.Startup.ContinueCommand.CanExecute(null));

        viewModel.Startup.ContinueCommand.Execute(null);
        Assert.True(viewModel.IsGameVisible);
    }

    private static NewGameViewModel AtTeamStep(GameManager gameManager, Action<string>? gameCreated = null)
    {
        var viewModel = new NewGameViewModel(gameManager, gameCreated: gameCreated)
        {
            WorldSeedText = Seed.ToString(CultureInfo.InvariantCulture),
        };
        viewModel.NextCommand.Execute(null);
        return viewModel;
    }

    private static NewGameViewModel AtManagerStep(GameManager gameManager, Action<string>? gameCreated = null)
    {
        var viewModel = AtTeamStep(gameManager, gameCreated);
        Select(viewModel, TeamName);
        viewModel.NextCommand.Execute(null);
        viewModel.ManagerName = "Pat";
        return viewModel;
    }

    private static void Select(NewGameViewModel viewModel, string teamName) =>
        GetTeams(viewModel).Single(team => team.Name == teamName).SelectCommand.Execute(null);

    private static IReadOnlyList<TeamOptionViewModel> GetTeams(NewGameViewModel viewModel) =>
        viewModel.Conferences
            .SelectMany(conference => conference.Divisions)
            .SelectMany(division => division.Teams)
            .ToList();
}