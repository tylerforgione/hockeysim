using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Saves;

using Xunit;

using static HockeySim.Management.Tests.SaveTestData;
using static HockeySim.Management.Tests.SeasonAdvancementTests;

namespace HockeySim.Management.Tests;

public sealed class SaveAndLoadTests
{
    [Fact]
    public void ANewGameLoadsExactlyAsSaved()
    {
        var manager = new GameManager();
        var saved = StartAtOpeningDay(manager);

        var loaded = SaveAndLoadIntoNewManager(manager).Snapshot;

        Assert.Equal(Describe(saved), Describe(loaded));
    }

    [Fact]
    public void AMidseasonGameLoadsExactlyAsSaved()
    {
        var manager = new GameManager();
        StartAtOpeningDay(manager);
        manager.SetLineup(SwapGoalies(ManagedTeam(manager.GetSnapshot())));
        manager.SetLineup(ReshuffleUnits(ManagedTeam(manager.GetSnapshot())));
        var readId = manager.GetSnapshot().Inbox[1].Id;
        manager.MarkInboxMessageRead(readId);
        var saved = Advance(manager, 9);

        var loaded = SaveAndLoadIntoNewManager(manager).Snapshot;

        Assert.NotEmpty(saved.Season.Results);
        Assert.Equal(Describe(saved), Describe(loaded));
        Assert.True(loaded.Inbox.Single(message => message.Id == readId).IsRead);
    }


    /// <summary>
    /// Saving and reloading must not reroll anything: the same commands after a reload produce
    /// the same results as uninterrupted play.
    /// </summary>
    [Fact]
    public void ContinuingALoadedGameMatchesUninterruptedPlay()
    {
        var uninterrupted = new GameManager();
        StartAtOpeningDay(uninterrupted);
        Advance(uninterrupted, 7);
        var store = new MemorySaveStore();
        uninterrupted.SaveGame(store);
        var resumed = new GameManager();
        resumed.LoadGame(store);

        var expected = ContinueWithLineupChange(uninterrupted);
        var actual = ContinueWithLineupChange(resumed);

        Assert.Equal(Describe(expected), Describe(actual));
        Assert.NotEqual(store.Saved!.RandomState, actual.RandomState);
    }

    [Fact]
    public void InjuriesAndWearAreSavedAndRebuiltFromTheCompletedMatches()
    {
        var manager = new GameManager();
        StartAtOpeningDay(manager);
        Advance(manager, 15);
        var first = new MemorySaveStore();
        manager.SaveGame(first);

        var (loaded, _) = SaveAndLoadIntoNewManager(manager);
        var second = new MemorySaveStore();
        loaded.SaveGame(second);

        Assert.Contains(first.Saved!.CompletedMatches, result => result.Injuries.Count > 0);
        Assert.All(first.Saved.CompletedMatches, result => Assert.NotEmpty(result.Wear));
        Assert.Equal(DescribeHealth(first.Saved), DescribeHealth(second.Saved!));
    }

    [Fact]
    public void LoadingReplacesADifferentActiveGame()
    {
        var source = new GameManager();
        StartAtOpeningDay(source);
        var saved = Advance(source, 3);
        var store = new MemorySaveStore();
        source.SaveGame(store);
        var target = new GameManager();
        StartAtOpeningDay(target, seed: 999);
        target.SelectManagedTeam(target.GetSnapshot().League.Teams[^1].Id);
        Advance(target, 5);

        var loaded = target.LoadGame(store);

        Assert.Equal(Describe(saved), Describe(loaded));
        Assert.Equal(Describe(ContinueWithLineupChange(source)), Describe(ContinueWithLineupChange(target)));
    }

    [Fact]
    public void EveryRatingIncludingHiddenDurabilityIsSavedAndRestored()
    {
        var manager = new GameManager();
        StartAtOpeningDay(manager);
        var store = new MemorySaveStore();
        manager.SaveGame(store);
        var loadedManager = new GameManager();
        loadedManager.LoadGame(store);
        var resaved = new MemorySaveStore();

        loadedManager.SaveGame(resaved);

        // Snapshots omit durability, so compare the saves themselves.
        Assert.All(
            SavedPlayers(store.Saved!),
            player => Assert.Equal(Enum.GetValues<Rating>().Order(), player.Ratings.Keys.Order()));
        Assert.Equal(DescribeRatings(store.Saved!), DescribeRatings(resaved.Saved!));
    }

    [Fact]
    public void ASaveIsACopyThatLaterPlayDoesNotChange()
    {
        var manager = new GameManager();
        var saved = StartAtOpeningDay(manager);
        var store = new MemorySaveStore();
        manager.SaveGame(store);

        Advance(manager, 3);
        manager.SetLineup(SwapGoalies(ManagedTeam(manager.GetSnapshot())));
        manager.MarkInboxMessageRead(manager.GetSnapshot().Inbox[0].Id);

        Assert.Empty(store.Saved!.CompletedMatches);
        Assert.Equal(Describe(saved), Describe(manager.LoadGame(store)));
    }

    [Fact]
    public void ALoadedGameAcceptsCommandsAgainstItsRestoredWorld()
    {
        var manager = new GameManager();
        StartAtOpeningDay(manager);
        Advance(manager, 2);
        var (loadedManager, loaded) = SaveAndLoadIntoNewManager(manager);
        var team = ManagedTeam(loaded);

        var afterLineup = loadedManager.SetLineup(SwapGoalies(team));
        var afterRead = loadedManager.MarkInboxMessageRead(loaded.Inbox[^1].Id);
        var otherTeam = loaded.League.Teams.First(candidate => candidate.Id != loaded.ManagedTeamId);
        var afterSelection = loadedManager.SelectManagedTeam(otherTeam.Id);

        Assert.Equal(team.Lineup.BackupGoalieId, ManagedTeam(afterLineup).Lineup.StartingGoalieId);
        Assert.True(afterRead.Inbox[^1].IsRead);
        Assert.Equal(otherTeam.Id, afterSelection.ManagedTeamId);
        Assert.Throws<ArgumentException>(() => loadedManager.SetLineup(
            CurrentLineup(team.Lineup) with { StartingGoalieId = team.Lineup.ForwardLines[0].CentreId }));
    }

    [Fact]
    public void SavingBeforeAGameStartsIsRejected()
    {
        var store = new MemorySaveStore();

        Assert.Throws<InvalidOperationException>(() => new GameManager().SaveGame(store));
        Assert.Null(store.Saved);
    }

    [Fact]
    public void LoadingIsRejectedFromInsideADayBeingPlayed()
    {
        var store = new MemorySaveStore();
        var source = new GameManager();
        StartAtOpeningDay(source);
        source.SaveGame(store);
        GameManager? manager = null;
        Exception? rejection = null;
        manager = new GameManager(new CallbackSimulator(() => rejection ??= Record.Exception(() => manager!.LoadGame(store))));
        StartAtOpeningDay(manager);

        manager.AdvanceDay();

        Assert.IsType<InvalidOperationException>(rejection);
    }


    [Fact]
    public void AStoreFailureLeavesTheActiveGameUnchanged()
    {
        var manager = new GameManager();
        StartAtOpeningDay(manager);
        var before = Advance(manager, 2);

        Assert.Throws<UnsupportedGameSaveVersionException>(() => manager.LoadGame(new FailingStore()));
        Assert.Equal(Describe(before), Describe(manager.GetSnapshot()));
    }


    private static IEnumerable<SavedPlayer> SavedPlayers(GameSave save) =>
        save.Conferences
            .SelectMany(conference => conference.Divisions)
            .SelectMany(division => division.Teams)
            .SelectMany(team => team.Roster);

    private static IEnumerable<string> DescribeHealth(GameSave save) =>
        save.CompletedMatches.SelectMany(result =>
            result.Injuries.Select(injury => $"{result.Date} {injury}")
                .Concat(result.Wear.Select(gain => $"{result.Date} {gain}")));

    private static IEnumerable<string> DescribeRatings(GameSave save) =>
        SavedPlayers(save).Select(player =>
            $"{player.Id} " + string.Join(",", player.Ratings.OrderBy(rating => rating.Key).Select(rating => $"{rating.Key}={rating.Value}")));

    private static (GameManager Manager, GameSnapshot Snapshot) SaveAndLoadIntoNewManager(GameManager manager)
    {
        var store = new MemorySaveStore();
        manager.SaveGame(store);
        var loadedManager = new GameManager();
        return (loadedManager, loadedManager.LoadGame(store));
    }

    internal static GameSnapshot Advance(GameManager manager, int days)
    {
        var snapshot = manager.GetSnapshot();
        for (var day = 0; day < days; day++)
        {
            snapshot = manager.AdvanceDayReplacingInjured();
        }

        return snapshot;
    }

    /// <summary>
    /// The management actions repeated after a reload: a lineup change, then more league days.
    /// </summary>
    internal static GameSnapshot ContinueWithLineupChange(GameManager manager)
    {
        manager.SetLineup(SwapGoalies(ManagedTeam(manager.GetSnapshot())));
        return Advance(manager, 6);
    }

    /// <summary>
    /// Reverses the first 5-on-4 unit's slots and makes a defence player the first extra attacker.
    /// </summary>
    private static Lineups.SetLineupCommand ReshuffleUnits(TeamSnapshot team)
    {
        var command = CurrentLineup(team.Lineup);
        var first = command.SpecialSituationUnits[0];
        return command with
        {
            SpecialSituationUnits = [first with { PlayerIds = first.PlayerIds.Reverse().ToList() }, .. command.SpecialSituationUnits.Skip(1)],
            ExtraAttackerIds = [team.Lineup.DefencePairs[2].RightDefenceId, command.ExtraAttackerIds[0]],
        };
    }

    internal static Lineups.SetLineupCommand SwapGoalies(TeamSnapshot team) =>
        CurrentLineup(team.Lineup) with
        {
            StartingGoalieId = team.Lineup.BackupGoalieId,
            BackupGoalieId = team.Lineup.StartingGoalieId,
        };

    private sealed class FailingStore : IGameSaveStore
    {
        public void Save(GameSave save) => throw new NotSupportedException();

        public GameSave Load() => throw new UnsupportedGameSaveVersionException(2, 1);
    }

    private sealed class CallbackSimulator(Action beforeMatch) : Simulation.IMatchSimulator
    {
        private readonly Simulation.MatchSimulator _engine = new();

        public Simulation.MatchResult Simulate(Match match, Simulation.OvertimeFormat overtime, Simulation.MatchHealth health, Simulation.Randomness.RandomState randomState)
        {
            beforeMatch();
            return _engine.Simulate(match, overtime, health, randomState);
        }
    }
}