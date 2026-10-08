using System.IO.Compression;
using System.Text;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Main;
using HockeySim.Desktop.Saves;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.NewGame;
using HockeySim.Management.Saves;
using HockeySim.Simulation.Randomness;

using Xunit;

namespace HockeySim.Desktop.Tests;

/// <summary>
/// Named saves through the main window and game shell, using real save files in a temporary folder.
/// </summary>
public sealed class SaveAndLoadTests : IDisposable
{
    private readonly TemporarySaveDirectory _saves = new();

    public void Dispose() => _saves.Dispose();

    [Fact]
    public void ANewGameIsUnsavedUntilSavedUnderTheChosenName()
    {
        var main = StartGame("Ottawa Rebuild");
        var shell = main.Game!;
        Assert.True(shell.Session.HasUnsavedChanges);
        Assert.Equal("Unsaved changes", shell.SaveStatus);

        shell.SaveGameCommand.Execute(null);
        var dialog = shell.SaveDialog!;
        Assert.Equal("Ottawa Rebuild", dialog.Name);
        Assert.False(dialog.HasExistingSaves);

        dialog.SaveCommand.Execute(null);

        Assert.False(shell.IsSaveDialogOpen);
        Assert.False(main.Confirmation.IsOpen);
        Assert.False(shell.Session.HasUnsavedChanges);
        Assert.Equal("Saved", shell.SaveStatus);
        Assert.True(File.Exists(_saves.PathFor("Ottawa Rebuild")));
        Assert.Equal(["Ottawa Rebuild"], _saves.Library.List().Select(save => save.Name.Value));
    }

    [Fact]
    public async Task PlayingChangingTheLineupOrReadingMailIsUnsavedProgress()
    {
        var shell = StartGame().Game!;

        Save(shell, "Progress");
        shell.Inbox.SelectedMessage = shell.Inbox.Messages.First(message => message.IsUnread);
        Assert.True(shell.Session.HasUnsavedChanges);

        Save(shell, "Progress", confirmOverwrite: true);
        await shell.AdvanceDayCommand.ExecuteAsync(null);
        Assert.True(shell.Session.HasUnsavedChanges);

        Save(shell, "Progress", confirmOverwrite: true);
        var lines = shell.Lines;
        lines.Lineup.StartingGoalie.SelectedPlayer = lines.Lineup.Scratches.Single(player => player.PositionAbbreviation == "G");
        lines.SaveCommand.Execute(null);
        Assert.True(shell.Session.HasUnsavedChanges);
    }

    [Fact]
    public async Task AFailedDayIsNotUnsavedProgress()
    {
        var session = new GameSession(StartManager(new GameManager(new FailingMatchSimulator())), "Saved Career", isSaved: true);
        var shell = new GameShellViewModel(session, saves: _saves.Library);

        await shell.AdvanceDayCommand.ExecuteAsync(null);

        Assert.True(shell.HasAdvanceError);
        Assert.False(session.HasUnsavedChanges);
    }

    [Fact]
    public void SavingOverAnExistingSaveAsksFirstAndCancellingKeepsIt()
    {
        var shell = StartGame().Game!;
        Save(shell, "Dynasty");
        var original = File.ReadAllBytes(_saves.PathFor("Dynasty"));
        shell.Session.MarkInboxMessageRead(shell.Session.Snapshot.Inbox[0].Id);

        shell.SaveGameCommand.Execute(null);
        var dialog = shell.SaveDialog!;
        var existing = Assert.Single(dialog.ExistingSaves);
        dialog.Name = "Something else";
        dialog.SelectedSave = existing;
        Assert.Equal("Dynasty", dialog.Name);
        dialog.SaveCommand.Execute(null);

        Assert.True(shell.Confirmation.IsOpen);
        Assert.Equal("Overwrite saved game?", shell.Confirmation.Current!.Title);
        Assert.Contains("'Dynasty'", shell.Confirmation.Current.Message, StringComparison.Ordinal);

        shell.Confirmation.CancelCommand.Execute(null);

        Assert.False(shell.Confirmation.IsOpen);
        Assert.True(shell.IsSaveDialogOpen);
        Assert.True(shell.Session.HasUnsavedChanges);
        Assert.Equal(original, File.ReadAllBytes(_saves.PathFor("Dynasty")));

        dialog.SaveCommand.Execute(null);
        shell.Confirmation.ConfirmCommand.Execute(null);

        Assert.False(shell.IsSaveDialogOpen);
        Assert.False(shell.Session.HasUnsavedChanges);
        Assert.NotEqual(original, File.ReadAllBytes(_saves.PathFor("Dynasty")));
    }

    [Fact]
    public void SavingUnderANewNameRenamesTheGameAndKeepsTheOldSave()
    {
        var shell = StartGame("First Name").Game!;
        Save(shell, "First Name");

        Save(shell, "Second Name");

        Assert.Equal("Second Name", shell.GameName);
        Assert.Equal(["First Name", "Second Name"], _saves.Library.List().Select(save => save.Name.Value).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("", "Enter a name for the save.")]
    [InlineData("Season/2", "Save names can use letters")]
    public void AnInvalidSaveNameIsExplainedAndNothingIsSaved(string name, string expected)
    {
        var shell = StartGame().Game!;
        shell.SaveGameCommand.Execute(null);
        var dialog = shell.SaveDialog!;

        dialog.Name = name;
        dialog.SaveCommand.Execute(null);

        Assert.True(dialog.HasError);
        Assert.StartsWith(expected, dialog.ErrorMessage, StringComparison.Ordinal);
        Assert.True(shell.IsSaveDialogOpen);
        Assert.Empty(_saves.Library.List());
    }

    [Fact]
    public void ASaveThatCannotBeWrittenIsReportedAndTheGameStaysUnsaved()
    {
        var gameManager = StartManager(new GameManager());
        var session = new GameSession(gameManager, "Career");
        var shell = new GameShellViewModel(session, saves: new FailingSaveLibrary());

        shell.SaveGameCommand.Execute(null);
        shell.SaveDialog!.SaveCommand.Execute(null);

        Assert.True(shell.IsSaveDialogOpen);
        Assert.Equal($"The game was not saved. {FailingSaveLibrary.Message}", shell.SaveDialog.ErrorMessage);
        Assert.True(session.HasUnsavedChanges);
        Assert.Equal("Career", session.GameName);
    }

    [Fact]
    public void SavesThatCannotBeListedAreReportedInTheSaveDialogAndLoadScreen()
    {
        var gameManager = StartManager(new GameManager());
        var shell = new GameShellViewModel(new GameSession(gameManager, "Career"), saves: new FailingSaveLibrary());
        shell.SaveGameCommand.Execute(null);
        Assert.Equal($"Your saved games could not be listed. {FailingSaveLibrary.Message}", shell.SaveDialog!.ErrorMessage);

        var main = new MainWindowViewModel(new GameManager(), new FailingSaveLibrary());
        main.Startup.ShowLoadGameCommand.Execute(null);
        Assert.Equal($"Your saved games could not be listed. {FailingSaveLibrary.Message}", main.LoadGame!.ErrorMessage);
        Assert.False(main.LoadGame.HasSaves);
    }

    [Fact]
    public void ACompletedSeasonCanStillBeSaved()
    {
        var gameManager = StartManager(new GameManager());
        while (!gameManager.GetSnapshot().Season.IsComplete)
        {
            gameManager.AdvanceDay();
        }

        var shell = new GameShellViewModel(new GameSession(gameManager, "Champions"), saves: _saves.Library);
        Assert.False(shell.AdvanceDayCommand.CanExecute(null));
        Assert.True(shell.SaveGameCommand.CanExecute(null));

        Save(shell, "Champions");

        var loaded = new GameManager().LoadGame(_saves.Library.Open(SaveName.Parse("Champions")));
        Assert.True(loaded.Season.IsComplete);
    }

    [Fact]
    public async Task SavingAndLeavingForTheMenuWaitForADayBeingPlayed()
    {
        using var engine = new GatedMatchSimulator();
        var shell = new GameShellViewModel(new GameSession(StartManager(new GameManager(engine)), "Career"), saves: _saves.Library);

        var advancing = shell.AdvanceDayCommand.ExecuteAsync(null);
        await engine.WaitUntilPlayingAsync();

        Assert.False(shell.SaveGameCommand.CanExecute(null));
        Assert.False(shell.ShowMainMenuCommand.CanExecute(null));

        engine.Release();
        await advancing;

        Assert.True(shell.SaveGameCommand.CanExecute(null));
        Assert.True(shell.ShowMainMenuCommand.CanExecute(null));
    }

    [Fact]
    public void LoadingFromTheStartupMenuShowsTheSavedGameOnEveryPage()
    {
        var savedSnapshot = CreateSave("Midseason", days: 6);
        var main = new MainWindowViewModel(new GameManager(), _saves.Library);

        main.Startup.ShowLoadGameCommand.Execute(null);
        var load = main.LoadGame!;
        Assert.False(load.LoadCommand.CanExecute(null));
        load.SelectedSave = Assert.Single(load.Saves);
        load.LoadCommand.Execute(null);

        Assert.False(main.Confirmation.IsOpen);
        Assert.True(main.IsGameVisible);
        Assert.True(main.Startup.ContinueCommand.CanExecute(null));
        var shell = main.Game!;
        Assert.Equal("Midseason", shell.GameName);
        Assert.Equal("Saved", shell.SaveStatus);
        AssertShellShows(shell, savedSnapshot);
    }

    [Fact]
    public void LoadingOverUnsavedProgressAsksFirstAndCancellingKeepsTheGame()
    {
        var savedSnapshot = CreateSave("Other Career", days: 4, seed: 99, teamName: "Seattle Evergreens");
        var main = StartGame("Current Career");
        var current = main.Game!;
        var currentSnapshot = current.Session.Snapshot;
        current.ShowMainMenuCommand.Execute(null);

        main.Startup.ShowLoadGameCommand.Execute(null);
        var load = main.LoadGame!;
        load.SelectedSave = load.Saves.Single();
        load.LoadCommand.Execute(null);

        Assert.True(main.Confirmation.IsOpen);
        Assert.Equal("Discard unsaved progress?", main.Confirmation.Current!.Title);
        Assert.Equal(
            "'Current Career' has progress that has not been saved. Loading 'Other Career' will discard it.",
            main.Confirmation.Current.Message);

        main.Confirmation.CancelCommand.Execute(null);

        Assert.Same(load, main.CurrentScreen);
        Assert.Same(current, main.Game);
        Assert.Same(currentSnapshot, current.Session.Snapshot);
        Assert.Equal(GameTestData.ManagedTeamName, main.Game!.TeamName);

        load.LoadCommand.Execute(null);
        main.Confirmation.ConfirmCommand.Execute(null);

        Assert.True(main.IsGameVisible);
        Assert.NotSame(current, main.Game);
        Assert.Equal("Seattle Evergreens", main.Game!.TeamName);
        AssertShellShows(main.Game, savedSnapshot);
    }

    [Fact]
    public void LoadingOverASavedGameDoesNotAsk()
    {
        CreateSave("Other Career", days: 1);
        var main = StartGame("Current Career");
        Save(main.Game!, "Current Career");
        main.Game!.ShowMainMenuCommand.Execute(null);

        main.Startup.ShowLoadGameCommand.Execute(null);
        main.LoadGame!.SelectedSave = main.LoadGame.Saves.Single(save => save.DisplayName == "Other Career");
        main.LoadGame.LoadCommand.Execute(null);

        Assert.False(main.Confirmation.IsOpen);
        Assert.Equal("Other Career", main.Game!.GameName);
    }

    [Fact]
    public void ADamagedSaveIsReportedAndTheActiveGameIsKept()
    {
        Directory.CreateDirectory(_saves.Library.DirectoryPath);
        File.WriteAllText(_saves.PathFor("Damaged"), "not a save");

        var (main, gameManager, before) = TryLoadingOverASavedGame("Damaged");

        Assert.StartsWith("'Damaged' is damaged or is not a valid HockeySim save.", main.LoadGame!.ErrorMessage, StringComparison.Ordinal);
        Assert.EndsWith("Your current game is unchanged.", main.LoadGame.ErrorMessage, StringComparison.Ordinal);
        AssertActiveGameKept(main, gameManager, before);
    }

    [Fact]
    public void ASaveFromAnotherVersionIsReportedAndTheActiveGameIsKept()
    {
        WriteSaveDocument("Future", """{"format":"HockeySim save","formatVersion":999,"game":{}}""");

        var (main, gameManager, before) = TryLoadingOverASavedGame("Future");

        Assert.Equal(
            "'Future' was saved by a different version of HockeySim (save format 999). This version reads save format 6 only. Your current game is unchanged.",
            main.LoadGame!.ErrorMessage);
        AssertActiveGameKept(main, gameManager, before);
    }

    [Fact]
    public void ASaveThatCannotBeReadIsReportedAndTheActiveGameIsKept()
    {
        var gameManager = StartManager(new GameManager());
        var main = new MainWindowViewModel(gameManager, new FailingSaveLibrary(listedSave: "Locked"));
        main.Startup.ShowLoadGameCommand.Execute(null);
        var load = main.LoadGame!;

        load.SelectedSave = load.Saves.Single();
        load.LoadCommand.Execute(null);

        Assert.Equal($"'Locked' could not be loaded. {FailingSaveLibrary.Message}", load.ErrorMessage);
        Assert.False(main.IsGameVisible);
        Assert.Equal(GameTestData.ManagedTeamName, gameManager.GetSnapshot().League.Teams.Single(team => team.Id == gameManager.GetSnapshot().ManagedTeamId).Name);
    }

    [Fact]
    public void StartingANewGameOverUnsavedProgressAsksFirst()
    {
        var main = StartGame("Current Career");
        var current = main.Game!;
        current.ShowMainMenuCommand.Execute(null);

        main.Startup.ShowNewGameCommand.Execute(null);
        Assert.True(main.Confirmation.IsOpen);
        Assert.Contains("Starting a new game will discard it.", main.Confirmation.Current!.Message, StringComparison.Ordinal);

        main.Confirmation.CancelCommand.Execute(null);
        Assert.True(main.IsStartupVisible);
        Assert.Same(current, main.Game);

        main.Startup.ShowNewGameCommand.Execute(null);
        main.Confirmation.ConfirmCommand.Execute(null);
        Assert.True(main.IsNewGameVisible);
    }

    [Fact]
    public void StartingANewGameAfterSavingDoesNotAsk()
    {
        var main = StartGame();
        Save(main.Game!, "Kept");
        main.Game!.ShowMainMenuCommand.Execute(null);

        main.Startup.ShowNewGameCommand.Execute(null);

        Assert.False(main.Confirmation.IsOpen);
        Assert.True(main.IsNewGameVisible);
    }

    [Fact]
    public void ExitingWithUnsavedProgressAsksFirst()
    {
        var exits = 0;
        var main = StartGame(exitApplication: () => exits++);
        Assert.False(main.CanCloseWithoutConfirmation);

        main.RequestExit();
        Assert.True(main.Confirmation.IsOpen);
        Assert.Contains("Exiting will discard it.", main.Confirmation.Current!.Message, StringComparison.Ordinal);
        main.Confirmation.CancelCommand.Execute(null);
        Assert.Equal(0, exits);
        Assert.False(main.CanCloseWithoutConfirmation);

        main.Game!.ShowMainMenuCommand.Execute(null);
        main.Startup.ExitCommand.Execute(null);
        main.Confirmation.ConfirmCommand.Execute(null);
        Assert.Equal(1, exits);
        Assert.True(main.CanCloseWithoutConfirmation);
    }

    [Fact]
    public void ExitingAfterSavingDoesNotAsk()
    {
        var exits = 0;
        var main = StartGame(exitApplication: () => exits++);
        Save(main.Game!, "Kept");

        Assert.True(main.CanCloseWithoutConfirmation);
        main.RequestExit();

        Assert.False(main.Confirmation.IsOpen);
        Assert.Equal(1, exits);
    }

    [Fact]
    public async Task ALoadedGameContinuesExactlyAsTheSavedGameWouldHave()
    {
        var main = StartGame("Reproducible");
        var shell = main.Game!;
        for (var day = 0; day < 3; day++)
        {
            await shell.AdvanceDayCommand.ExecuteAsync(null);
        }

        Save(shell, "Checkpoint");
        var uninterrupted = await PlayOnAsync(shell);

        shell.ShowMainMenuCommand.Execute(null);
        main.Startup.ShowLoadGameCommand.Execute(null);
        main.LoadGame!.SelectedSave = main.LoadGame.Saves.Single();
        main.LoadGame.LoadCommand.Execute(null);
        main.Confirmation.ConfirmCommand.Execute(null);
        var resumed = await PlayOnAsync(main.Game!);

        Assert.Equal(Describe(uninterrupted), Describe(resumed));
    }

    private static async Task<GameSnapshot> PlayOnAsync(GameShellViewModel shell)
    {
        var lines = shell.Lines;
        lines.Lineup.StartingGoalie.SelectedPlayer = lines.Lineup.Scratches.Single(player => player.PositionAbbreviation == "G");
        lines.SaveCommand.Execute(null);
        for (var day = 0; day < 4; day++)
        {
            await shell.AdvanceDayCommand.ExecuteAsync(null);
        }

        return shell.Session.Snapshot;
    }

    private static IEnumerable<string> Describe(GameSnapshot snapshot) =>
        snapshot.Season.Results
            .Select(result => $"{result.Date} {result.Home.TeamId}:{result.Home.Score} {result.Away.TeamId}:{result.Away.Score} {result.Decision}")
            .Append($"{snapshot.Season.CurrentDate} {snapshot.RandomState}");

    private static void AssertShellShows(GameShellViewModel shell, GameSnapshot expected)
    {
        var session = shell.Session;
        var managedName = expected.League.Teams.Single(team => team.Id == expected.ManagedTeamId).Name;
        var played = expected.Season.Results.Count(result => result.Home.TeamId == expected.ManagedTeamId || result.Away.TeamId == expected.ManagedTeamId);

        Assert.Equal(expected.ManagedTeamId, session.Snapshot.ManagedTeamId);
        Assert.Equal(expected.RandomState, session.Snapshot.RandomState);
        Assert.Equal(managedName, shell.TeamName);
        Assert.Equal(expected.Season.Results.Count, session.Snapshot.Season.Results.Count);
        Assert.Contains(shell.Standings.Tables.SelectMany(table => table.Rows), row => row.TeamName == managedName && row.GamesPlayed == played);
        Assert.Equal(played, shell.Schedule.Matches.Count(match => match.IsCompleted));
        Assert.Equal(expected.League.Teams.Single(team => team.Id == expected.ManagedTeamId).Roster.Count, shell.Roster.Roster.Skaters.Count + shell.Roster.Roster.Goalies.Count);
        Assert.All(shell.Lines.Lineup.ForwardLines.SelectMany(line => new[] { line.LeftWing, line.Centre, line.RightWing }), slot =>
            Assert.Contains(slot.SelectedPlayer!.Id, expected.League.Teams.Single(team => team.Id == expected.ManagedTeamId).Lineup.DressedPlayerIds));
        Assert.Equal(expected.Inbox.Count, shell.Inbox.Messages.Count);
    }

    private (MainWindowViewModel Main, GameManager GameManager, GameSnapshot Before) TryLoadingOverASavedGame(string saveName)
    {
        var gameManager = new GameManager();
        var main = StartGame("Current Career", gameManager: gameManager);
        Save(main.Game!, "Current Career");
        var before = main.Game!.Session.Snapshot;
        main.Game.ShowMainMenuCommand.Execute(null);

        main.Startup.ShowLoadGameCommand.Execute(null);
        main.LoadGame!.SelectedSave = main.LoadGame.Saves.Single(save => save.DisplayName == saveName);
        main.LoadGame.LoadCommand.Execute(null);
        return (main, gameManager, before);
    }

    private static void AssertActiveGameKept(MainWindowViewModel main, GameManager gameManager, GameSnapshot before)
    {
        Assert.NotNull(main.LoadGame);
        Assert.Equal("Current Career", main.Game!.GameName);
        Assert.Same(before, main.Game.Session.Snapshot);
        Assert.Equal(Describe(before), Describe(gameManager.GetSnapshot()));

        main.LoadGame!.BackCommand.Execute(null);
        main.Startup.ContinueCommand.Execute(null);
        Assert.True(main.IsGameVisible);
    }

    private GameSnapshot CreateSave(
        string name,
        int days,
        ulong seed = 13579,
        string teamName = GameTestData.ManagedTeamName)
    {
        var manager = new GameManager();
        manager.StartNewGame(new NewGameCommand(2026, new RandomState(seed), teamName));
        for (var day = 0; day < days; day++)
        {
            manager.AdvanceDay();
        }

        manager.SaveGame(_saves.Library.Open(SaveName.Parse(name)));
        return manager.GetSnapshot();
    }

    private void WriteSaveDocument(string name, string json)
    {
        Directory.CreateDirectory(_saves.Library.DirectoryPath);
        using var file = File.Create(_saves.PathFor(name));
        using var compressed = new BrotliStream(file, CompressionLevel.Fastest);
        compressed.Write(Encoding.UTF8.GetBytes(json));
    }

    private MainWindowViewModel StartGame(
        string gameName = "Test Career",
        Action? exitApplication = null,
        GameManager? gameManager = null)
    {
        var main = new MainWindowViewModel(gameManager ?? new GameManager(), _saves.Library, exitApplication);
        main.Startup.ShowNewGameCommand.Execute(null);
        var newGame = main.NewGame!;
        newGame.GameName = gameName;
        newGame.Conferences
            .SelectMany(conference => conference.Divisions)
            .SelectMany(division => division.Teams)
            .Single(team => team.Name == GameTestData.ManagedTeamName)
            .SelectCommand.Execute(null);
        newGame.CreateGameCommand.Execute(null);
        return main;
    }

    private static GameManager StartManager(GameManager manager)
    {
        manager.StartNewGame(new NewGameCommand(2026, new RandomState(13579), GameTestData.ManagedTeamName));
        return manager;
    }

    private static void Save(GameShellViewModel shell, string name, bool confirmOverwrite = false)
    {
        shell.SaveGameCommand.Execute(null);
        shell.SaveDialog!.Name = name;
        shell.SaveDialog.SaveCommand.Execute(null);
        if (confirmOverwrite)
        {
            shell.Confirmation.ConfirmCommand.Execute(null);
        }

        Assert.False(shell.IsSaveDialogOpen);
        Assert.False(shell.Session.HasUnsavedChanges);
    }

    /// <summary>
    /// A save library whose storage always fails, as a full disk or a locked folder would.
    /// </summary>
    private sealed class FailingSaveLibrary(string? listedSave = null) : ISavedGameLibrary, IGameSaveStore
    {
        public const string Message = "The disk is not available.";

        public IReadOnlyList<SavedGameSummary> List() => listedSave is null
            ? throw new GameSaveStorageException(Message)
            : [new SavedGameSummary(SaveName.Parse(listedSave), DateTimeOffset.UnixEpoch)];

        public bool Contains(SaveName name) => false;

        public IGameSaveStore Open(SaveName name) => this;

        public void Save(GameSave save) => throw new GameSaveStorageException(Message);

        public GameSave Load() => throw new GameSaveStorageException(Message);
    }
}