using HockeySim.Infrastructure.Saves;
using HockeySim.Management.GameManagement;
using HockeySim.Management.Saves;

using Xunit;

using static HockeySim.Infrastructure.Tests.SaveTestGames;

namespace HockeySim.Infrastructure.Tests;

/// <summary>
/// Keeps named saves in a real folder, each test in its own temporary directory.
/// </summary>
public sealed class GameSaveDirectoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hockeysim-tests-{Guid.NewGuid():N}");

    private GameSaveDirectory Saves => new(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void AFolderThatDoesNotExistYetHoldsNoSaves()
    {
        Assert.Empty(Saves.List());
        Assert.False(Saves.Contains(SaveName.Parse("Dynasty")));
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public void EachNamedSaveIsKeptSeparatelyAndLoadsItsOwnGame()
    {
        var first = StartGame(seed: 1);
        var second = StartGame(seed: 2);
        var firstSaved = Advance(first, 3);
        var secondSaved = Advance(second, 5);

        first.SaveGame(Saves.Open(SaveName.Parse("Halifax Rebuild")));
        second.SaveGame(Saves.Open(SaveName.Parse("Second Career")));

        Assert.True(File.Exists(Path.Combine(_directory, "Halifax Rebuild.hockeysim")));
        Assert.Equal(
            ["Halifax Rebuild", "Second Career"],
            Saves.List().Select(save => save.Name.Value).Order(StringComparer.Ordinal));
        Assert.Equal(Describe(firstSaved), Describe(new GameManager().LoadGame(Saves.Open(SaveName.Parse("Halifax Rebuild")))));
        Assert.Equal(Describe(secondSaved), Describe(new GameManager().LoadGame(Saves.Open(SaveName.Parse("Second Career")))));
    }

    [Fact]
    public void SavesAreListedMostRecentlySavedFirstWithTheirSaveTime()
    {
        var manager = StartGame();
        manager.SaveGame(Saves.Open(SaveName.Parse("Older")));
        manager.SaveGame(Saves.Open(SaveName.Parse("Newer")));
        File.SetLastWriteTimeUtc(Path.Combine(_directory, "Older.hockeysim"), new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(Path.Combine(_directory, "Newer.hockeysim"), new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc));

        var listed = Saves.List();

        Assert.Equal(["Newer", "Older"], listed.Select(save => save.Name.Value));
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero), listed[0].SavedAt);
    }

    [Fact]
    public void ANameDifferingOnlyInCaseFindsAndReplacesTheExistingSave()
    {
        var manager = StartGame();
        manager.SaveGame(Saves.Open(SaveName.Parse("Dynasty")));
        var advanced = Advance(manager, 2);

        Assert.True(Saves.Contains(SaveName.Parse("DYNASTY")));
        manager.SaveGame(Saves.Open(SaveName.Parse("dynasty")));

        var listed = Assert.Single(Saves.List());
        Assert.Equal(SaveName.Parse("Dynasty"), listed.Name);
        Assert.Equal(Describe(advanced), Describe(new GameManager().LoadGame(Saves.Open(SaveName.Parse("Dynasty")))));
    }

    [Fact]
    public void FilesThatAreNotNamedSavesAreNotListed()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "notes.txt"), "not a save");
        File.WriteAllText(Path.Combine(_directory, "Backup.hockeysim.old"), "not a save");
        File.WriteAllText(Path.Combine(_directory, "CON.hockeysim"), "not a valid save name");
        StartGame().SaveGame(Saves.Open(SaveName.Parse("Real Save")));

        Assert.Equal(["Real Save"], Saves.List().Select(save => save.Name.Value));
    }

    [Fact]
    public void SavingWhereTheFolderCannotBeCreatedIsReportedAsAStorageFailure()
    {
        // A file where the save folder should be cannot be listed as a folder.
        File.WriteAllText(_directory, "not a folder");
        try
        {
            Assert.Empty(new GameSaveDirectory(_directory).List());
            Assert.Throws<GameSaveStorageException>(
                () => StartGame().SaveGame(new GameSaveDirectory(_directory).Open(SaveName.Parse("Dynasty"))));
        }
        finally
        {
            File.Delete(_directory);
        }
    }

    [Fact]
    public void TheDefaultFolderIsInTheUsersApplicationData()
    {
        var path = GameSaveDirectory.DefaultPath();

        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), path, StringComparison.Ordinal);
        Assert.EndsWith(Path.Combine("HockeySim", "Saves"), path, StringComparison.Ordinal);
    }
}