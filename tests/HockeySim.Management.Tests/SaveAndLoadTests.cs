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
        ShotsNotMatchingSkaterShots,
        ExpectedGoalsAgainstNotMatchingOpponent,
        UnmatchedFaceoffs,
        NegativeTimeOnIce,
        NegativePenaltyMinutes,
        PowerPlayGoalWithoutOpportunity,
        ShorthandedGoalWithoutOpponentPowerPlay,
        EmptyNetGoalChargedToTheGoalie,
        GoalMissingFromSummary,
        PenaltyMissingFromSummary,
        MissingSummary,
        ShotTotalsNotMatchingOpponent,
        MissingOnIceShotTotals,
        DressedPlayerNotOnRoster,
        SkaterDressedInGoal,
        DuplicateRosterPlayer,
        RatingOutOfRange,
        MissingRating,
        MissingDurability,
        MissingBiography,
        MissingBirthplace,
        RegionInCountryWithoutRegions,
        UndefinedNationality,
        HeightOutOfRange,
        PlayerTooYoungOnOpeningDay,
        ManagedTeamNotInLeague,
        ScheduledTeamNotInLeague,
        MisnumberedInbox,
        ScratchedSkaterInUnit,
        MissingUnit,
        UndefinedSituation,
        MissingExtraAttackers,
        MissingInjuries,
        InjuryRecoveryOutsideItsRange,
        InjuryToAPlayerWhoDidNotAppear,
        UndefinedBodyPart,
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
        manager.SetLineup(ReshuffleUnits(ManagedTeam(manager.GetSnapshot())));
        var readId = manager.GetSnapshot().Inbox[1].Id;
        manager.MarkInboxMessageRead(readId);
        var saved = Advance(manager, 9);

        var loaded = SaveAndLoadIntoNewManager(manager).Snapshot;

        Assert.NotEmpty(saved.Season.Results);
        Assert.Equal(Describe(saved), Describe(loaded));
        Assert.True(loaded.Inbox.Single(message => message.Id == readId).IsRead);
    }

    [Fact]
    public void ACompletedSeasonLoadsAsSavedAndStillCannotAdvance()
    {
        var manager = new GameManager();
        StartGame(manager);
        var saved = manager.GetSnapshot();
        while (!saved.Season.IsComplete)
        {
            saved = manager.AdvanceDayReplacingInjured();
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
            manager.AdvanceDayReplacingInjured();
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
    public void InjuriesAndWearAreSavedAndRebuiltFromTheCompletedMatches()
    {
        var manager = new GameManager();
        StartGame(manager);
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
    public void EveryRatingIncludingHiddenDurabilityIsSavedAndRestored()
    {
        var manager = new GameManager();
        StartGame(manager);
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
        var homeScored = save.CompletedMatches.First(result => result.Home.Skaters.Any(skater => skater.Goals > 0));
        var homeScorer = homeScored.Home.Skaters.First(skater => skater.Goals > 0);
        var regulationResult = save.CompletedMatches.First(result => result.Decision == MatchDecision.Regulation);
        var penalized = save.CompletedMatches.First(result => result.Penalties.Any(penalty => penalty.Kind != PenaltyKind.PenaltyShot));
        var firstTeam = save.Conferences[0].Divisions[0].Teams[0];
        var firstPlayer = firstTeam.Roster[0];
        var unknownPlayerId = new PlayerId(Guid.NewGuid());
        var firstUnit = firstTeam.Lineup.SpecialSituationUnits[0];
        var dressedIds = firstTeam.Lineup.ForwardLines.SelectMany(line => new[] { line.LeftWingId, line.CentreId, line.RightWingId })
            .Concat(firstTeam.Lineup.DefencePairs.SelectMany(pair => new[] { pair.LeftDefenceId, pair.RightDefenceId }))
            .ToHashSet();
        var scratchedSkaterId = firstTeam.Roster
            .First(player => player.Position != Position.Goalie && !dressedIds.Contains(player.Id))
            .Id;
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
            ShotsNotMatchingSkaterShots => WithFirstResult(save, firstResult with
            {
                Home = firstResult.Home with { Shots = firstResult.Home.Shots + 1 },
                Away = firstResult.Away with { Goalie = firstResult.Away.Goalie with { ShotsAgainst = firstResult.Home.Shots + 1 } },
            }),
            ExpectedGoalsAgainstNotMatchingOpponent => WithFirstResult(save, firstResult with
            {
                Home = firstResult.Home with
                {
                    Goalie = firstResult.Home.Goalie with { ExpectedGoalsAgainst = firstResult.Home.Goalie.ExpectedGoalsAgainst + 0.5 },
                },
            }),
            UnmatchedFaceoffs => WithFirstResult(save, firstResult with
            {
                Home = firstResult.Home with
                {
                    Skaters =
                    [
                        firstResult.Home.Skaters[0] with { FaceoffsWon = firstResult.Home.Skaters[0].FaceoffsWon + 1 },
                        .. firstResult.Home.Skaters.Skip(1),
                    ],
                },
            }),
            NegativeTimeOnIce => WithFirstResult(save, firstResult with
            {
                Home = firstResult.Home with { Goalie = firstResult.Home.Goalie with { TimeOnIceSeconds = -1 } },
            }),
            NegativePenaltyMinutes => WithFirstResult(save, firstResult with
            {
                Home = firstResult.Home with
                {
                    Skaters = [firstResult.Home.Skaters[0] with { PenaltyMinutes = -2 }, .. firstResult.Home.Skaters.Skip(1)],
                },
            }),
            PowerPlayGoalWithoutOpportunity => WithResult(save, homeScored, homeScored with
            {
                Home = homeScored.Home with
                {
                    PowerPlayOpportunities = 0,
                    Skaters = homeScored.Home.Skaters
                        .Select(skater => skater == homeScorer ? skater with { PowerPlayGoals = 1, ShorthandedGoals = 0 } : skater)
                        .ToList(),
                },
            }),
            ShorthandedGoalWithoutOpponentPowerPlay => WithResult(save, homeScored, homeScored with
            {
                Home = homeScored.Home with
                {
                    Skaters = homeScored.Home.Skaters
                        .Select(skater => skater == homeScorer ? skater with { PowerPlayGoals = 0, ShorthandedGoals = 1 } : skater)
                        .ToList(),
                },
                Away = homeScored.Away with { PowerPlayOpportunities = 0 },
            }),
            EmptyNetGoalChargedToTheGoalie => WithResult(save, homeScored, homeScored with
            {
                // The goal becomes an empty-net goal, but the away goalie still has it against.
                Home = homeScored.Home with
                {
                    Skaters = homeScored.Home.Skaters
                        .Select(skater => skater == homeScorer ? skater with { EmptyNetGoals = skater.EmptyNetGoals + 1 } : skater)
                        .ToList(),
                },
            }),
            GoalMissingFromSummary => WithResult(save, homeScored, homeScored with { Goals = homeScored.Goals.Skip(1).ToList() }),
            PenaltyMissingFromSummary => WithResult(save, penalized, penalized with
            {
                // A penalty shot carries no minutes, so remove one that does.
                Penalties = penalized.Penalties
                    .Where(penalty => penalty != penalized.Penalties.First(timed => timed.Kind != PenaltyKind.PenaltyShot))
                    .ToList(),
            }),
            MissingSummary => WithFirstResult(save, firstResult with { Penalties = null! }),
            MissingInjuries => WithFirstResult(save, firstResult with { Injuries = null! }),
            InjuryRecoveryOutsideItsRange => WithFirstResult(save, firstResult with
            {
                Injuries = [new SavedInjury(1, 60, firstResult.Home.TeamId, firstResult.Home.Skaters[0].PlayerId, InjuryType.Concussion, 999)],
            }),
            InjuryToAPlayerWhoDidNotAppear => WithFirstResult(save, firstResult with
            {
                Injuries = [new SavedInjury(1, 60, firstResult.Home.TeamId, firstResult.Away.Skaters[0].PlayerId, InjuryType.BruisedFoot, 3)],
            }),
            UndefinedBodyPart => WithFirstResult(save, firstResult with
            {
                Wear = [new SavedWearGain(firstResult.Home.Skaters[0].PlayerId, (BodyPart)99, 1)],
            }),
            ShotTotalsNotMatchingOpponent => WithFirstResult(save, firstResult with
            {
                Home = firstResult.Home with
                {
                    ShotTotals = firstResult.Home.ShotTotals with
                    {
                        FiveOnFive = firstResult.Home.ShotTotals.FiveOnFive with
                        {
                            AttemptsAgainst = firstResult.Home.ShotTotals.FiveOnFive.AttemptsAgainst + 1,
                        },
                    },
                },
            }),
            MissingOnIceShotTotals => WithFirstResult(save, firstResult with
            {
                Home = firstResult.Home with
                {
                    Skaters = [firstResult.Home.Skaters[0] with { OnIce = null! }, .. firstResult.Home.Skaters.Skip(1)],
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
            MissingDurability => WithFirstPlayer(save, firstPlayer with
            {
                Ratings = firstPlayer.Ratings
                    .Where(rating => rating.Key != Rating.Durability)
                    .ToDictionary(rating => rating.Key, rating => rating.Value),
            }),
            MissingBiography => WithFirstPlayer(save, firstPlayer with { Biography = null! }),
            MissingBirthplace => WithFirstPlayer(save, firstPlayer with
            {
                Biography = firstPlayer.Biography with { Birthplace = null! },
            }),
            RegionInCountryWithoutRegions => WithFirstPlayer(save, firstPlayer with
            {
                Biography = firstPlayer.Biography with { Birthplace = new SavedBirthplace("Stockholm", "Uppland", Country.Sweden) },
            }),
            UndefinedNationality => WithFirstPlayer(save, firstPlayer with
            {
                Biography = firstPlayer.Biography with { Nationality = (Country)99 },
            }),
            HeightOutOfRange => WithFirstPlayer(save, firstPlayer with
            {
                Biography = firstPlayer.Biography with { HeightInches = 0 },
            }),
            PlayerTooYoungOnOpeningDay => WithFirstPlayer(save, firstPlayer with
            {
                Biography = firstPlayer.Biography with { BirthDate = save.Schedule[0].Date.AddYears(-15) },
            }),
            ManagedTeamNotInLeague => save with { ManagedTeamId = new TeamId(Guid.NewGuid()) },
            ScheduledTeamNotInLeague => save with
            {
                Schedule = [.. save.Schedule.SkipLast(1), save.Schedule[^1] with { HomeTeamId = new TeamId(Guid.NewGuid()) }],
            },
            MisnumberedInbox => save with { Inbox = save.Inbox.Reverse().ToList() },
            ScratchedSkaterInUnit => WithFirstTeam(save, firstTeam with
            {
                Lineup = firstTeam.Lineup with
                {
                    SpecialSituationUnits =
                    [
                        firstUnit with { PlayerIds = [scratchedSkaterId, .. firstUnit.PlayerIds.Skip(1)] },
                        .. firstTeam.Lineup.SpecialSituationUnits.Skip(1),
                    ],
                },
            }),
            MissingUnit => WithFirstTeam(save, firstTeam with
            {
                Lineup = firstTeam.Lineup with { SpecialSituationUnits = firstTeam.Lineup.SpecialSituationUnits.Skip(1).ToList() },
            }),
            UndefinedSituation => WithFirstTeam(save, firstTeam with
            {
                Lineup = firstTeam.Lineup with
                {
                    SpecialSituationUnits =
                    [
                        firstUnit with { Situation = (SpecialSituation)42 },
                        .. firstTeam.Lineup.SpecialSituationUnits.Skip(1),
                    ],
                },
            }),
            MissingExtraAttackers => WithFirstTeam(save, firstTeam with
            {
                Lineup = firstTeam.Lineup with { ExtraAttackerIds = null! },
            }),
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

    private static GameSnapshot Advance(GameManager manager, int days)
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
    private static GameSnapshot ContinueWithLineupChange(GameManager manager)
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

        public Simulation.MatchResult Simulate(Match match, Simulation.OvertimeFormat overtime, Simulation.MatchHealth health, Simulation.Randomness.RandomState randomState)
        {
            beforeMatch();
            return _engine.Simulate(match, overtime, health, randomState);
        }
    }
}