using System.Text.Json.Nodes;

using HockeySim.Domain;
using HockeySim.Infrastructure.Saves;
using HockeySim.Management.GameManagement;
using HockeySim.Management.Lineups;
using HockeySim.Management.Saves;
using HockeySim.Simulation.Randomness;

using Xunit;

using static HockeySim.Infrastructure.Tests.SaveTestGames;

namespace HockeySim.Infrastructure.Tests;

/// <summary>
/// Saves and loads real files, each test in its own temporary directory.
/// </summary>
public sealed class GameSaveFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hockeysim-tests-{Guid.NewGuid():N}");

    private string SavePath => Path.Combine(_directory, "game.hockeysim");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void ASavedGameLoadsAsSavedAndContinuesAsIfUninterrupted(int daysPlayed)
    {
        var uninterrupted = StartGame();
        var saved = Advance(uninterrupted, daysPlayed);
        uninterrupted.SaveGame(new GameSaveFile(SavePath));

        var resumed = new GameManager();
        var loaded = resumed.LoadGame(new GameSaveFile(SavePath));

        Assert.Equal(Describe(saved), Describe(loaded));
        Assert.Equal(Describe(ContinueWithLineupChange(uninterrupted)), Describe(ContinueWithLineupChange(resumed)));
    }

    [Fact]
    public void ChangedUnitsAndExtraAttackersLoadAsSaved()
    {
        var manager = StartGame();
        var team = manager.GetSnapshot().League.Teams.Single(team => team.Id == manager.GetSnapshot().ManagedTeamId);
        var command = SetLineupCommand.From(team.Lineup);
        var penaltyKill = command.SpecialSituationUnits.First(unit => unit.Situation == SpecialSituation.PenaltyKill3On4);
        var saved = manager.SetLineup(command with
        {
            SpecialSituationUnits = command.SpecialSituationUnits
                .Select(unit => unit == penaltyKill ? unit with { PlayerIds = unit.PlayerIds.Reverse().ToList() } : unit)
                .ToList(),
            ExtraAttackerIds = [team.Lineup.DefencePairs[1].LeftDefenceId, team.Lineup.ForwardLines[2].RightWingId],
        });
        manager.SaveGame(new GameSaveFile(SavePath));

        var loaded = new GameManager().LoadGame(new GameSaveFile(SavePath));

        var loadedTeam = loaded.League.Teams.Single(other => other.Id == team.Id);
        Assert.Equal(penaltyKill.PlayerIds.Reverse(), loadedTeam.Lineup.UnitsFor(SpecialSituation.PenaltyKill3On4)[0].PlayerIds);
        Assert.Equal([team.Lineup.DefencePairs[1].LeftDefenceId, team.Lineup.ForwardLines[2].RightWingId], loadedTeam.Lineup.ExtraAttackerIds);
        Assert.Equal(Describe(saved), Describe(loaded));
    }

    [Fact]
    public void ACompletedSeasonLoadsAsSaved()
    {
        var manager = StartGame();
        var saved = Advance(manager, int.MaxValue);
        manager.SaveGame(new GameSaveFile(SavePath));

        var resumed = new GameManager();
        var loaded = resumed.LoadGame(new GameSaveFile(SavePath));

        Assert.True(loaded.Season.IsComplete);
        Assert.Equal(1344, loaded.Season.Results.Count);
        Assert.Equal(Describe(saved), Describe(loaded));
        Assert.Throws<InvalidOperationException>(resumed.AdvanceDay);
    }

    [Fact]
    public void TheFileNamesItsFormatAndVersionAheadOfTheGame()
    {
        StartGame().SaveGame(new GameSaveFile(SavePath));

        var document = SaveFileContents.Read(SavePath);

        Assert.Equal(["format", "formatVersion", "game"], document.Select(property => property.Key));
        Assert.Equal("HockeySim save", (string?)document["format"]);
        Assert.Equal(GameSaveFile.FormatVersion, (int?)document["formatVersion"]);
    }

    [Fact]
    public void SavingCreatesTheDirectoryAndReplacesAnEarlierSave()
    {
        var manager = StartGame();
        var store = new GameSaveFile(SavePath);
        manager.SaveGame(store);
        var later = Advance(manager, 3);

        manager.SaveGame(store);

        Assert.Equal(Describe(later), Describe(new GameManager().LoadGame(store)));
        Assert.Equal([SavePath], Directory.GetFiles(_directory));
    }

    [Fact]
    public void AFailedSaveLeavesTheEarlierSaveIntactAndNoPartialFile()
    {
        var manager = StartGame();
        var saved = Advance(manager, 3);
        var store = new GameSaveFile(SavePath);
        manager.SaveGame(store);
        var capture = new CapturingStore();
        manager.SaveGame(capture);

        // Serialization fails part-way through writing, after the temporary file has begun.
        var unwritable = capture.Saved! with { Inbox = null! };
        Assert.ThrowsAny<Exception>(() => store.Save(unwritable));

        Assert.Equal([SavePath], Directory.GetFiles(_directory));
        Assert.Equal(Describe(saved), Describe(new GameManager().LoadGame(store)));
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(ulong.MaxValue)]
    [InlineData(9_007_199_254_740_993UL)]
    public void TheRandomStateIsPreservedExactly(ulong value)
    {
        var capture = new CapturingStore();
        StartGame().SaveGame(capture);
        var store = new GameSaveFile(SavePath);

        store.Save(capture.Saved! with { RandomState = new RandomState(value) });

        Assert.Equal(new RandomState(value), store.Load().RandomState);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void ASaveFromAnotherFormatVersionIsRejectedAsUnsupported(int version)
    {
        StartGame().SaveGame(new GameSaveFile(SavePath));
        var document = SaveFileContents.Read(SavePath);
        document["formatVersion"] = version;
        SaveFileContents.Write(SavePath, document);
        var manager = StartGame(seed: 7);
        var before = Advance(manager, 2);

        var exception = Assert.Throws<UnsupportedGameSaveVersionException>(
            () => manager.LoadGame(new GameSaveFile(SavePath)));

        Assert.Equal(version, exception.Version);
        Assert.Equal(GameSaveFile.FormatVersion, exception.SupportedVersion);
        Assert.Contains($"version {version}", exception.Message, StringComparison.Ordinal);
        Assert.Equal(Describe(before), Describe(manager.GetSnapshot()));
    }

    [Theory]
    [InlineData("""[]""")]
    [InlineData("""{"formatVersion":2,"game":{}}""")]
    [InlineData("""{"format":"Another game","formatVersion":2,"game":{}}""")]
    [InlineData("""{"format":"HockeySim save","game":{}}""")]
    [InlineData("""{"format":"HockeySim save","formatVersion":"2","game":{}}""")]
    [InlineData("""{"format":"HockeySim save","formatVersion":2}""")]
    [InlineData("""{"format":"HockeySim save","formatVersion":2,"game":null}""")]
    [InlineData("""{"format":"HockeySim save","formatVersion":2,"game":{}}""")]
    [InlineData("""{"format":"HockeySim save","formatVersion":2,"game":{"seasonYear":2026""")]
    public void ADocumentThatIsNotAWholeSaveIsRejected(string json)
    {
        Directory.CreateDirectory(_directory);
        SaveFileContents.WriteCompressed(SavePath, json);

        Assert.Throws<InvalidGameSaveException>(() => new GameSaveFile(SavePath).Load());
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0x7B, 0x7D })]
    [InlineData(new byte[] { 0x00, 0xFF, 0x13, 0x37, 0x42, 0x99, 0x01, 0x02 })]
    public void AFileThatIsNotACompressedSaveIsRejected(byte[] contents)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(SavePath, contents);

        Assert.Throws<InvalidGameSaveException>(() => new GameSaveFile(SavePath).Load());
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(0.99)]
    public void ATruncatedSaveIsRejectedAndTheActiveGameIsUnchanged(double keptShare)
    {
        var source = StartGame();
        Advance(source, 5);
        source.SaveGame(new GameSaveFile(SavePath));
        var bytes = File.ReadAllBytes(SavePath);
        File.WriteAllBytes(SavePath, bytes[..(int)(bytes.Length * keptShare)]);
        var manager = StartGame(seed: 7);
        var before = Advance(manager, 2);

        Assert.Throws<InvalidGameSaveException>(() => manager.LoadGame(new GameSaveFile(SavePath)));
        Assert.Equal(Describe(before), Describe(manager.GetSnapshot()));
    }

    public static TheoryData<string> DamagedGames() =>
    [
        "missing inbox",
        "null conferences",
        "null roster player",
        "malformed team identity",
        "unknown position",
        "fractional rating",
        "negative random state",
        "unknown situation",
        "missing units",
        "null extra attacker",
    ];

    [Theory]
    [MemberData(nameof(DamagedGames))]
    public void AGameWithMissingOrMalformedValuesIsRejected(string damage)
    {
        var manager = StartGame();
        Advance(manager, 3);
        manager.SaveGame(new GameSaveFile(SavePath));
        var document = SaveFileContents.Read(SavePath);
        var game = document["game"]!.AsObject();
        var team = game["conferences"]![0]!["divisions"]![0]!["teams"]![0]!.AsObject();
        var player = team["roster"]![0]!.AsObject();
        var lineup = team["lineup"]!.AsObject();

        switch (damage)
        {
            case "missing inbox":
                game.Remove("inbox");
                break;
            case "null conferences":
                game["conferences"] = null;
                break;
            case "null roster player":
                team["roster"]![0] = null;
                break;
            case "malformed team identity":
                team["id"] = "not-a-team";
                break;
            case "unknown position":
                player["position"] = "Rover";
                break;
            case "fractional rating":
                player["ratings"]!["Skating"] = 55.5;
                break;
            case "negative random state":
                game["randomState"] = -1;
                break;
            case "unknown situation":
                lineup["specialSituationUnits"]![0]!["situation"] = "SixOnTwo";
                break;
            case "missing units":
                lineup.Remove("specialSituationUnits");
                break;
            case "null extra attacker":
                lineup["extraAttackerIds"]![0] = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(damage));
        }

        SaveFileContents.Write(SavePath, document);

        Assert.Throws<InvalidGameSaveException>(() => new GameManager().LoadGame(new GameSaveFile(SavePath)));
    }

    /// <summary>
    /// A well-formed file can still describe a game that breaks the game's rules. Management
    /// rejects it while rebuilding the world, so the active game is untouched.
    /// </summary>
    [Fact]
    public void AWellFormedSaveThatBreaksTheGamesRulesIsRejectedAndTheActiveGameIsUnchanged()
    {
        var source = StartGame();
        Advance(source, 3);
        source.SaveGame(new GameSaveFile(SavePath));
        var document = SaveFileContents.Read(SavePath);
        var firstResult = document["game"]!["completedMatches"]![0]!;
        firstResult["home"]!["shots"] = 0;
        SaveFileContents.Write(SavePath, document);
        var manager = StartGame(seed: 7);
        var before = Advance(manager, 2);

        Assert.Throws<InvalidGameSaveException>(() => manager.LoadGame(new GameSaveFile(SavePath)));
        Assert.Equal(Describe(before), Describe(manager.GetSnapshot()));
    }

    [Fact]
    public void LoadingAMissingSaveReportsTheMissingFile()
    {
        var store = new GameSaveFile(SavePath);

        var exception = Assert.Throws<GameSaveStorageException>(store.Load);
        Assert.Contains(SavePath, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASaveThatCannotBeWrittenIsReportedAsAStorageFailureAndLeavesTheEarlierSave()
    {
        var manager = StartGame();
        manager.SaveGame(new GameSaveFile(SavePath));
        var earlier = File.ReadAllBytes(SavePath);

        // A directory where the save file should be makes the final move fail on every platform.
        var blockedPath = Path.Combine(_directory, "blocked.hockeysim");
        Directory.CreateDirectory(blockedPath);

        Assert.Throws<GameSaveStorageException>(() => manager.SaveGame(new GameSaveFile(blockedPath)));
        Assert.Equal(earlier, File.ReadAllBytes(SavePath));
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void TheStoreRequiresAPath()
    {
        Assert.Throws<ArgumentException>(() => new GameSaveFile(" "));
        Assert.Equal(Path.GetFullPath(SavePath), new GameSaveFile(SavePath).FilePath);
    }

    [Fact]
    public void IdentitiesAndEnumsAreWrittenAsReadableValues()
    {
        StartGame().SaveGame(new GameSaveFile(SavePath));

        var game = SaveFileContents.Read(SavePath)["game"]!;
        var player = game["conferences"]![0]!["divisions"]![0]!["teams"]![0]!["roster"]![0]!;

        Assert.True(Guid.TryParse((string?)player["id"], out _));
        Assert.Contains((string?)player["position"], Enum.GetNames<Domain.Position>());
        var units = game["conferences"]![0]!["divisions"]![0]!["teams"]![0]!["lineup"]!["specialSituationUnits"]!.AsArray();
        Assert.Equal(
            Enum.GetNames<SpecialSituation>().SelectMany(name => Enumerable.Repeat(name, SpecialSituationFormat.For(Enum.Parse<SpecialSituation>(name)).UnitCount)),
            units.Select(unit => (string?)unit!["situation"]));
        Assert.Equal(
            Enum.GetNames<Domain.Rating>().Order(),
            player["ratings"]!.AsObject().Select(rating => rating.Key).Order());
    }
}