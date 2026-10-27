using System.IO.Compression;
using System.Text;

using HockeySim.Desktop.Main;
using HockeySim.Desktop.Saves;
using HockeySim.Desktop.Startup;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.NewGame;
using HockeySim.Management.Saves;
using HockeySim.Simulation.Randomness;

using Xunit;

namespace HockeySim.Desktop.Tests;

/// <summary>
/// The startup menu's Continue and recent games, and the saved-game rows it shares with the load
/// screen, using real save files in a temporary folder.
/// </summary>
public sealed class StartupMenuTests : IDisposable
{
    private static readonly DateTimeOffset FirstSaved = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TemporarySaveDirectory _saves = new();

    public void Dispose() => _saves.Dispose();

    [Fact]
    public void WithNoSavesContinueIsDisabledAndNothingIsListed()
    {
        var main = new MainWindowViewModel(new GameManager(), _saves.Library);

        Assert.False(main.Startup.ContinueCommand.CanExecute(null));
        Assert.Equal(string.Empty, main.Startup.ContinueSubtitle);
        Assert.False(main.Startup.HasContinueSubtitle);
        Assert.Empty(main.Startup.RecentGames);
        Assert.False(main.Startup.HasRecentGames);
    }

    [Fact]
    public void AnUnlistableLibraryShowsTheErrorAndNoRows()
    {
        var main = new MainWindowViewModel(new GameManager(), new UnlistableLibrary());

        Assert.True(main.Startup.HasError);
        Assert.StartsWith("Your saved games could not be listed.", main.Startup.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(main.Startup.RecentGames);
        Assert.False(main.Startup.ContinueCommand.CanExecute(null));
    }

    [Fact]
    public void ASingleSaveIsListedWithItsDetailsAndContinueShowsItsNameAndDate()
    {
        CreateSave("Midseason", days: 6, savedAt: FirstSaved);
        var main = new MainWindowViewModel(new GameManager(), _saves.Library);

        var row = Assert.Single(main.Startup.RecentGames);
        Assert.Equal("Midseason", row.DisplayName);
        Assert.Equal(GameTestData.ManagedTeamName, row.TeamName);
        Assert.True(row.HasDetails);
        Assert.NotNull(row.PrimaryColour);
        Assert.NotNull(row.SecondaryColour);
        Assert.Matches(@"^\d+-\d+-\d+$", row.RecordLabel);
        Assert.True(main.Startup.ContinueCommand.CanExecute(null));
        Assert.Equal($"Midseason · {row.DateLabel}", main.Startup.ContinueSubtitle);
    }

    [Fact]
    public void SeveralSavesAreListedNewestFirstUpToEight()
    {
        for (var index = 1; index <= 10; index++)
        {
            CreateSave($"Save {index:00}", days: 0, savedAt: FirstSaved.AddHours(index));
        }

        var main = new MainWindowViewModel(new GameManager(), _saves.Library);

        Assert.Equal(
            ["Save 10", "Save 09", "Save 08", "Save 07", "Save 06", "Save 05", "Save 04", "Save 03"],
            main.Startup.RecentGames.Select(row => row.DisplayName));
        Assert.StartsWith("Save 10 · ", main.Startup.ContinueSubtitle, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnreadableSaveIsListedByNameAndTimeAndChoosingItShowsTheLoadError()
    {
        WriteSaveDocument("Future", """{"format":"HockeySim save","formatVersion":999,"game":{}}""", FirstSaved);
        var main = new MainWindowViewModel(new GameManager(), _saves.Library);

        var row = Assert.Single(main.Startup.RecentGames);
        Assert.False(row.HasDetails);
        Assert.Equal("Future", row.DisplayName);
        Assert.Equal("Can't be read", row.TeamName);
        Assert.Equal("—", row.DateLabel);
        Assert.Equal("—", row.RecordLabel);
        Assert.Equal("—", row.StandingLabel);
        Assert.False(string.IsNullOrEmpty(row.SavedAtLabel));
        Assert.False(main.Startup.ContinueCommand.CanExecute(null));

        main.Startup.OpenSaveCommand.Execute(row);

        Assert.Equal(
            "'Future' was saved by a different version of HockeySim (save format 999). This version reads save format 16 only.",
            main.Startup.ErrorMessage);
        Assert.True(main.IsStartupVisible);
    }

    [Fact]
    public void ContinueSkipsAnUnreadableSaveAndLoadsTheNewestReadableOne()
    {
        CreateSave("Older", days: 2, savedAt: FirstSaved);
        WriteSaveDocument("Newest But Damaged", """{"format":"HockeySim save","formatVersion":999,"game":{}}""", FirstSaved.AddHours(5));
        var main = new MainWindowViewModel(new GameManager(), _saves.Library);

        Assert.Equal(["Newest But Damaged", "Older"], main.Startup.RecentGames.Select(row => row.DisplayName));
        Assert.StartsWith("Older · ", main.Startup.ContinueSubtitle, StringComparison.Ordinal);

        main.Startup.ContinueCommand.Execute(null);

        Assert.True(main.IsGameVisible);
        Assert.Equal("Older", main.Game!.GameName);
        Assert.Equal("Saved", main.Game.SaveStatus);
    }

    [Fact]
    public void ContinueReturnsToTheOpenGameRatherThanLoadingASave()
    {
        CreateSave("Other Career", days: 4, savedAt: FirstSaved.AddHours(9), seed: 99, teamName: "Seattle Evergreens");
        var main = StartGame("Current Career");
        var current = main.Game!;
        current.ShowMainMenuCommand.Execute(null);

        var currentDate = SavedGameRowViewModel.InGameDate(current.Session.Snapshot.Season.CurrentDate);
        Assert.Equal($"Current Career · {currentDate}", main.Startup.ContinueSubtitle);

        main.Startup.ContinueCommand.Execute(null);

        Assert.False(main.Confirmation.IsOpen);
        Assert.Same(current, main.Game);
        Assert.Same(current, main.CurrentScreen);
    }

    [Fact]
    public void ChoosingARecentGameLoadsItAndAsksFirstWhenTheOpenGameHasUnsavedProgress()
    {
        CreateSave("Other Career", days: 4, savedAt: FirstSaved, seed: 99, teamName: "Seattle Evergreens");
        var main = StartGame("Current Career");
        var current = main.Game!;
        current.ShowMainMenuCommand.Execute(null);
        var row = main.Startup.RecentGames.Single();

        main.Startup.OpenSaveCommand.Execute(row);

        Assert.True(main.Confirmation.IsOpen);
        Assert.Equal(
            "'Current Career' has progress that has not been saved. Loading 'Other Career' will discard it.",
            main.Confirmation.Current!.Message);

        main.Confirmation.CancelCommand.Execute(null);
        Assert.True(main.IsStartupVisible);
        Assert.Same(current, main.Game);

        main.Startup.OpenSaveCommand.Execute(row);
        main.Confirmation.ConfirmCommand.Execute(null);

        Assert.True(main.IsGameVisible);
        Assert.Equal("Other Career", main.Game!.GameName);
        Assert.Equal("Seattle Evergreens", main.Game.TeamName);
    }

    [Fact]
    public void ChoosingARecentGameWithNothingOpenLoadsItWithoutAsking()
    {
        CreateSave("Midseason", days: 3, savedAt: FirstSaved);
        var main = new MainWindowViewModel(new GameManager(), _saves.Library);

        main.Startup.OpenSaveCommand.Execute(main.Startup.RecentGames.Single());

        Assert.False(main.Confirmation.IsOpen);
        Assert.True(main.IsGameVisible);
        Assert.Equal("Midseason", main.Game!.GameName);
    }

    [Fact]
    public void ALoadFailureWhileAGameIsOpenKeepsItAndSaysSo()
    {
        WriteSaveDocument("Future", """{"format":"HockeySim save","formatVersion":999,"game":{}}""", FirstSaved);
        var main = StartGame("Current Career");
        var current = main.Game!;
        Save(current, "Current Career");
        current.ShowMainMenuCommand.Execute(null);

        main.Startup.OpenSaveCommand.Execute(main.Startup.RecentGames.Single(row => row.DisplayName == "Future"));

        Assert.EndsWith("Your current game is unchanged.", main.Startup.ErrorMessage, StringComparison.Ordinal);
        Assert.Same(current, main.Game);
        Assert.True(main.Startup.ContinueCommand.CanExecute(null));
    }

    [Fact]
    public void TheListRefreshesAfterSavingAndQuittingToTheMenu()
    {
        CreateSave("Old Career", days: 1, savedAt: FirstSaved);
        var main = StartGame("New Career");
        Assert.Equal(["Old Career"], main.Startup.RecentGames.Select(row => row.DisplayName));

        Save(main.Game!, "New Career");
        main.Game!.ShowMainMenuCommand.Execute(null);

        Assert.Equal(["New Career", "Old Career"], main.Startup.RecentGames.Select(row => row.DisplayName));
        Assert.StartsWith("New Career · ", main.Startup.ContinueSubtitle, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLoadScreenShowsTheSameDetailsForEverySave()
    {
        for (var index = 1; index <= 10; index++)
        {
            CreateSave($"Save {index:00}", days: 0, savedAt: FirstSaved.AddHours(index));
        }

        var main = new MainWindowViewModel(new GameManager(), _saves.Library);
        main.Startup.ShowLoadGameCommand.Execute(null);

        var loadRows = main.LoadGame!.Saves;
        Assert.Equal(10, loadRows.Count);
        Assert.All(loadRows, row =>
        {
            Assert.Equal(GameTestData.ManagedTeamName, row.TeamName);
            Assert.Equal("Preseason", row.StandingLabel);
            Assert.Equal("0-0-0", row.RecordLabel);
        });
    }

    [Theory]
    [InlineData(SeasonPhase.Preseason, SavedPlayoffProgress.NotInPlayoffs, null, "Preseason")]
    [InlineData(SeasonPhase.RegularSeason, SavedPlayoffProgress.NotInPlayoffs, null, "3rd Atlantic")]
    [InlineData(SeasonPhase.Playoffs, SavedPlayoffProgress.Alive, PlayoffRound.SecondRound, "Second round")]
    [InlineData(SeasonPhase.Playoffs, SavedPlayoffProgress.Alive, PlayoffRound.Final, "Final")]
    [InlineData(SeasonPhase.Playoffs, SavedPlayoffProgress.Eliminated, PlayoffRound.FirstRound, "Eliminated")]
    [InlineData(SeasonPhase.Playoffs, SavedPlayoffProgress.Champion, PlayoffRound.Final, "Champions")]
    [InlineData(SeasonPhase.Playoffs, SavedPlayoffProgress.NotInPlayoffs, null, "3rd Atlantic")]
    public void StandingTextFollowsThePhaseAndPlayoffProgress(
        SeasonPhase phase,
        SavedPlayoffProgress playoffs,
        PlayoffRound? round,
        string expected)
    {
        var details = Details(phase, playoffs, round);

        Assert.Equal(expected, SavedGameRowViewModel.Standing(details));
        var row = new SavedGameRowViewModel(new SavedGameSummary(SaveName.Parse("Hand Built"), FirstSaved, details));
        Assert.Equal(expected, row.StandingLabel);
        Assert.Equal("30-20-5", row.RecordLabel);
        Assert.Equal("Ottawa Owls", row.TeamName);
    }

    private static SavedGameDetails Details(SeasonPhase phase, SavedPlayoffProgress playoffs, PlayoffRound? round) =>
        new(
            "Ottawa Owls",
            "#C8102E",
            "#FFFFFF",
            new DateOnly(2027, 1, 15),
            phase,
            Wins: 30,
            Losses: 20,
            OvertimeLosses: 5,
            Points: 65,
            DivisionName: "Atlantic",
            DivisionRank: 3,
            playoffs,
            round);

    private MainWindowViewModel StartGame(string gameName)
    {
        var main = new MainWindowViewModel(new GameManager(), _saves.Library);
        main.Startup.ShowNewGameCommand.Execute(null);
        var newGame = main.NewGame!;
        newGame.WorldSeedText = "13579";
        newGame.NextCommand.Execute(null);
        newGame.Conferences
            .SelectMany(conference => conference.Divisions)
            .SelectMany(division => division.Teams)
            .Single(team => team.Name == GameTestData.ManagedTeamName)
            .SelectCommand.Execute(null);
        newGame.NextCommand.Execute(null);
        newGame.ManagerName = "Test Manager";
        newGame.SaveName = gameName;
        newGame.StartGameCommand.Execute(null);
        return main;
    }

    private static void Save(HockeySim.Desktop.Game.GameShellViewModel shell, string name)
    {
        shell.SaveGameCommand.Execute(null);
        shell.SaveDialog!.Name = name;
        shell.SaveDialog.SaveCommand.Execute(null);
        Assert.False(shell.Session.HasUnsavedChanges);
    }

    private void CreateSave(
        string name,
        int days,
        DateTimeOffset savedAt,
        ulong seed = 13579,
        string teamName = GameTestData.ManagedTeamName)
    {
        var manager = new GameManager();
        manager.StartNewGame(new NewGameCommand(2026, new RandomState(seed), teamName));
        for (var day = 0; day < days; day++)
        {
            manager.AdvanceDayReplacingInjured();
        }

        manager.SaveGame(_saves.Library.Open(SaveName.Parse(name)));
        File.SetLastWriteTimeUtc(_saves.PathFor(name), savedAt.UtcDateTime);
    }

    private void WriteSaveDocument(string name, string json, DateTimeOffset savedAt)
    {
        Directory.CreateDirectory(_saves.Library.DirectoryPath);
        using (var file = File.Create(_saves.PathFor(name)))
        using (var compressed = new BrotliStream(file, CompressionLevel.Fastest))
        {
            compressed.Write(Encoding.UTF8.GetBytes(json));
        }

        File.SetLastWriteTimeUtc(_saves.PathFor(name), savedAt.UtcDateTime);
    }

    private sealed class UnlistableLibrary : ISavedGameLibrary
    {
        public IReadOnlyList<SavedGameSummary> List() => throw new GameSaveStorageException("The disk is not available.");

        public bool Contains(SaveName name) => false;

        public IGameSaveStore Open(SaveName name) => throw new GameSaveStorageException("The disk is not available.");

        public void Delete(SaveName name) => throw new GameSaveStorageException("The disk is not available.");
    }
}