using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Saves;

using Xunit;

using static HockeySim.Management.Tests.SaveAndLoadTests.InvalidSave;
using static HockeySim.Management.Tests.SaveTestData;
using static HockeySim.Management.Tests.SeasonAdvancementTests;

namespace HockeySim.Management.Tests;

public sealed class SaveAndLoadTests
{
    public enum InvalidSave
    {
        NoGame,
        MissingConferences,
        MissingCompletedMatch,
        MissingResultBeforeCurrentDate,
        ResultOnOrAfterCurrentDate,
        DuplicateResult,
        CurrentDateBeforeOpeningDay,
        ScoreNotMatchingGoals,
        UndefinedDecision,
        BoxScoreForUnrosteredPlayer,
        DressedPlayerNotOnRoster,
        SkaterDressedInGoal,
        DuplicateRosterPlayer,
        RatingOutOfRange,
        MissingRating,
        ManagedTeamNotInLeague,
        ScheduledTeamNotInLeague,
        MisnumberedInbox,
    }

    [Fact]
    public void ANewGameLoadsExactlyAsSaved()
    {
        var manager = new GameManager();
        var saved = StartGame(manager);

        var loaded = SaveAndLoadIntoNewManager(manager).Snapshot;

        Assert.Equal(Describe(saved), Describe(loaded));
    }

    [Fact]
    public void AMidseasonGameLoadsExactlyAsSaved()
    {
        var manager = new GameManager();
        StartGame(manager);
        manager.SetLineup(SwapGoalies(ManagedTeam(manager.GetSnapshot())));
        manager.MarkInboxMessageRead(manager.GetSnapshot().Inbox[1].Id);
        var saved = Advance(manager, 9);

        var loaded = SaveAndLoadIntoNewManager(manager).Snapshot;

        Assert.NotEmpty(saved.Season.Results);
        Assert.Equal(Describe(saved), Describe(loaded));
        Assert.True(loaded.Inbox[1].IsRead);
    }

    [Fact]
    public void ACompletedSeasonLoadsAsSavedAndStillCannotAdvance()
    {
        var manager = new GameManager();
        StartGame(manager);
        var saved = manager.GetSnapshot();
        while (!saved.Season.IsComplete)
        {
            saved = manager.AdvanceDay();
        }

        var (loadedManager, loaded) = SaveAndLoadIntoNewManager(manager);

        Assert.Equal(Describe(saved), Describe(loaded));
        Assert.Equal(1344, loaded.Season.Results.Count);
        Assert.Throws<InvalidOperationException>(loadedManager.AdvanceDay);
    }

    [Fact]
    public void ACompletedSeasonCannotBeSavedAsContinuingPastItsEnd()
    {
        var manager = new GameManager();
        StartGame(manager);
        while (!manager.GetSnapshot().Season.IsComplete)
        {
            manager.AdvanceDay();
        }

        var store = new MemorySaveStore();
        manager.SaveGame(store);
        var pastTheEnd = store.Saved! with { CurrentDate = store.Saved.CurrentDate.AddDays(1) };

        var exception = Assert.Throws<InvalidGameSaveException>(
            () => new GameManager().LoadGame(new MemorySaveStore(pastTheEnd)));
        Assert.Contains("after the end of the season", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Saving and reloading must not reroll anything: the same commands after a reload produce
    /// the same results as uninterrupted play.
    /// </summary>
    [Fact]
    public void ContinuingALoadedGameMatchesUninterruptedPlay()
    {
        var uninterrupted = new GameManager();
        StartGame(uninterrupted);
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
    public void LoadingReplacesADifferentActiveGame()
    {
        var source = new GameManager();
        StartGame(source);
        var saved = Advance(source, 3);
        var store = new MemorySaveStore();
        source.SaveGame(store);
        var target = new GameManager();
        StartGame(target, seed: 999);
        target.SelectManagedTeam(target.GetSnapshot().League.Teams[^1].Id);
        Advance(target, 5);

        var loaded = target.LoadGame(store);

        Assert.Equal(Describe(saved), Describe(loaded));
        Assert.Equal(Describe(ContinueWithLineupChange(source)), Describe(ContinueWithLineupChange(target)));
    }

    [Fact]
    public void ASaveIsACopyThatLaterPlayDoesNotChange()
    {
        var manager = new GameManager();
        var saved = StartGame(manager);
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
        StartGame(manager);
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
        StartGame(source);
        source.SaveGame(store);
        GameManager? manager = null;
        Exception? rejection = null;
        manager = new GameManager(new CallbackSimulator(() => rejection ??= Record.Exception(() => manager!.LoadGame(store))));
        StartGame(manager);

        manager.AdvanceDay();

        Assert.IsType<InvalidOperationException>(rejection);
    }

    [Theory]
    [MemberData(nameof(InvalidSaves))]
    public void AnInvalidSaveIsRejectedAndTheActiveGameContinuesUnchanged(InvalidSave invalidSave)
    {
        var control = new GameManager();
        StartGame(control);
        Advance(control, 5);
        var manager = new GameManager();
        StartGame(manager);
        var before = Advance(manager, 5);
        var store = new MemorySaveStore();
        manager.SaveGame(store);

        var exception = Record.Exception(() => manager.LoadGame(new MemorySaveStore(Corrupt(store.Saved!, invalidSave))));

        Assert.IsType<InvalidGameSaveException>(exception);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
        Assert.Equal(Describe(before), Describe(manager.GetSnapshot()));
        Assert.Equal(Describe(ContinueWithLineupChange(control)), Describe(ContinueWithLineupChange(manager)));
    }

    [Fact]
    public void AStoreFailureLeavesTheActiveGameUnchanged()
    {
        var manager = new GameManager();
        StartGame(manager);
        var before = Advance(manager, 2);

        Assert.Throws<UnsupportedGameSaveVersionException>(() => manager.LoadGame(new FailingStore()));
        Assert.Equal(Describe(before), Describe(manager.GetSnapshot()));
    }

    public static TheoryData<InvalidSave> InvalidSaves() => new(Enum.GetValues<InvalidSave>());

    /// <summary>
    /// Damages a save taken after five league days (opening day, an empty day, and a third with
    /// matches), so it has results to break and the current date is the fourth day.
    /// </summary>
    private static GameSave Corrupt(GameSave save, InvalidSave invalidSave)
    {
        var firstResult = save.CompletedMatches[0];
        var regulationResult = save.CompletedMatches.First(result => result.Decision == MatchDecision.Regulation);
        var firstTeam = save.Conferences[0].Divisions[0].Teams[0];
        var firstPlayer = firstTeam.Roster[0];
        var unknownPlayerId = new PlayerId(Guid.NewGuid());
        return invalidSave switch
        {
            NoGame => null!,
            MissingConferences => save with { Conferences = null! },
            MissingCompletedMatch => save with { CompletedMatches = [null!, .. save.CompletedMatches.Skip(1)] },
            MissingResultBeforeCurrentDate => save with { CompletedMatches = save.CompletedMatches.SkipLast(1).ToList() },
            ResultOnOrAfterCurrentDate => save with { CurrentDate = save.CompletedMatches[^1].Date },
            DuplicateResult => save with { CompletedMatches = [.. save.CompletedMatches, firstResult] },
            CurrentDateBeforeOpeningDay => save with { CurrentDate = save.Schedule[0].Date.AddDays(-1) },
            ScoreNotMatchingGoals => WithResult(save, regulationResult, regulationResult with
            {
                Home = regulationResult.Home with { Score = regulationResult.Home.Score + 10 },
            }),
            UndefinedDecision => WithFirstResult(save, firstResult with { Decision = (MatchDecision)7 }),
            BoxScoreForUnrosteredPlayer => WithFirstResult(save, firstResult with
            {
                Home = firstResult.Home with
                {
                    Skaters = [firstResult.Home.Skaters[0] with { PlayerId = unknownPlayerId }, .. firstResult.Home.Skaters.Skip(1)],
                },
            }),
            DressedPlayerNotOnRoster => WithFirstTeam(save, firstTeam with
            {
                Lineup = firstTeam.Lineup with { BackupGoalieId = unknownPlayerId },
            }),
            SkaterDressedInGoal => WithFirstTeam(save, firstTeam with
            {
                Lineup = firstTeam.Lineup with { BackupGoalieId = firstTeam.Lineup.ForwardLines[0].CentreId },
            }),
            DuplicateRosterPlayer => WithFirstTeam(save, firstTeam with
            {
                Roster = [firstPlayer, firstPlayer with { Number = 100 - firstPlayer.Number }, .. firstTeam.Roster.Skip(2)],
            }),
            RatingOutOfRange => WithFirstPlayer(save, firstPlayer with
            {
                Ratings = firstPlayer.Ratings.ToDictionary(rating => rating.Key, rating => 101),
            }),
            MissingRating => WithFirstPlayer(save, firstPlayer with
            {
                Ratings = firstPlayer.Ratings.Skip(1).ToDictionary(rating => rating.Key, rating => rating.Value),
            }),
            ManagedTeamNotInLeague => save with { ManagedTeamId = new TeamId(Guid.NewGuid()) },
            ScheduledTeamNotInLeague => save with
            {
                Schedule = [.. save.Schedule.SkipLast(1), save.Schedule[^1] with { HomeTeamId = new TeamId(Guid.NewGuid()) }],
            },
            MisnumberedInbox => save with { Inbox = save.Inbox.Reverse().ToList() },
            _ => throw new ArgumentOutOfRangeException(nameof(invalidSave)),
        };
    }

    private static GameSave WithFirstResult(GameSave save, SavedCompletedMatch result) =>
        WithResult(save, save.CompletedMatches[0], result);

    private static GameSave WithResult(GameSave save, SavedCompletedMatch original, SavedCompletedMatch replacement) =>
        save with
        {
            CompletedMatches = save.CompletedMatches.Select(result => result == original ? replacement : result).ToList(),
        };

    private static GameSave WithFirstPlayer(GameSave save, SavedPlayer player)
    {
        var team = save.Conferences[0].Divisions[0].Teams[0];
        return WithFirstTeam(save, team with { Roster = [player, .. team.Roster.Skip(1)] });
    }

    private static GameSave WithFirstTeam(GameSave save, SavedTeam team)
    {
        var conference = save.Conferences[0];
        var division = conference.Divisions[0];
        return save with
        {
            Conferences =
            [
                conference with { Divisions = [division with { Teams = [team, .. division.Teams.Skip(1)] }, .. conference.Divisions.Skip(1)] },
                .. save.Conferences.Skip(1),
            ],
        };
    }

    private static (GameManager Manager, GameSnapshot Snapshot) SaveAndLoadIntoNewManager(GameManager manager)
    {
        var store = new MemorySaveStore();
        manager.SaveGame(store);
        var loadedManager = new GameManager();
        return (loadedManager, loadedManager.LoadGame(store));
    }

    private static GameSnapshot Advance(GameManager manager, int days)
    {
        var snapshot = manager.GetSnapshot();
        for (var day = 0; day < days; day++)
        {
            snapshot = manager.AdvanceDay();
        }

        return snapshot;
    }

    /// <summary>
    /// The management actions repeated after a reload: a lineup change, then more league days.
    /// </summary>
    private static GameSnapshot ContinueWithLineupChange(GameManager manager)
    {
        manager.SetLineup(SwapGoalies(ManagedTeam(manager.GetSnapshot())));
        return Advance(manager, 6);
    }

    private static Lineups.SetLineupCommand SwapGoalies(TeamSnapshot team) =>
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

        public Simulation.MatchResult Simulate(Match match, Simulation.Randomness.RandomState randomState)
        {
            beforeMatch();
            return _engine.Simulate(match, randomState);
        }
    }
}