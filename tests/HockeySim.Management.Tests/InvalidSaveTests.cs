using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.Saves;

using Xunit;

using static HockeySim.Management.Tests.InvalidSaveTests.InvalidSave;
using static HockeySim.Management.Tests.SaveAndLoadTests;
using static HockeySim.Management.Tests.SaveTestData;
using static HockeySim.Management.Tests.SeasonAdvancementTests;

namespace HockeySim.Management.Tests;

public sealed class InvalidSaveTests(InvalidSaveTests.ControlGame control)
    : IClassFixture<InvalidSaveTests.ControlGame>
{
    public enum InvalidSave
    {
        NoGame,
        MissingConferences,
        MissingCompletedMatch,
        MissingResultBeforeCurrentDate,
        ResultOnOrAfterCurrentDate,
        DuplicateResult,
        CurrentDateBeforeTheFirstDay,
        MissingPreseasonSchedule,
        PreseasonMatchOnOpeningDay,
        PreseasonResultSavedWithTheRegularSeason,
        InjuryInAPreseasonMatch,
        MissingPlayoffMatches,
        RegularSeasonResultSavedWithThePlayoffs,
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
        MissingTimeOnIceBySituation,
        TimeOnIceBySituationNotSummingToTimeOnIce,
        MissingPeriodTotals,
        PeriodTotalsNotMatchingShots,
        DressedPlayerNotOnRoster,
        SkaterDressedInGoal,
        MalformedTeamColour,
        MissingTeamColour,
        MissingArena,
        MatchingTeamColours,
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

    [Theory]
    [MemberData(nameof(InvalidSaves))]
    public void AnInvalidSaveIsRejectedAndTheActiveGameContinuesUnchanged(InvalidSave invalidSave)
    {
        var manager = new GameManager();
        manager.LoadGame(new MemorySaveStore(control.Save));

        var exception = Record.Exception(() => manager.LoadGame(new MemorySaveStore(Corrupt(control.Save, invalidSave))));

        Assert.IsType<InvalidGameSaveException>(exception);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
        Assert.Equal(control.Before, Describe(manager.GetSnapshot()));
        Assert.Equal(control.Continuation, Describe(ContinueWithLineupChange(manager)));
    }

    public static TheoryData<InvalidSave> InvalidSaves() => new(Enum.GetValues<InvalidSave>());

    /// <summary>
    /// Damages a save taken after the preseason and five regular-season days (opening day, an
    /// empty day, and a third with matches), so it has results of both phases to break.
    /// </summary>
    private static GameSave Corrupt(GameSave save, InvalidSave invalidSave)
    {
        var firstResult = save.CompletedMatches[0];
        var firstPreseasonResult = save.PreseasonMatches[0];
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
            CurrentDateBeforeTheFirstDay => save with { CurrentDate = save.PreseasonSchedule[0].Date.AddDays(-1) },
            MissingPreseasonSchedule => save with { PreseasonSchedule = null! },
            PreseasonMatchOnOpeningDay => save with
            {
                PreseasonSchedule = [.. save.PreseasonSchedule.SkipLast(1), save.PreseasonSchedule[^1] with { Date = save.Schedule[0].Date }],
            },
            PreseasonResultSavedWithTheRegularSeason => save with
            {
                PreseasonMatches = save.PreseasonMatches.Skip(1).ToList(),
                CompletedMatches = [save.PreseasonMatches[0], .. save.CompletedMatches],
            },
            MissingPlayoffMatches => save with { PlayoffMatches = null! },
            RegularSeasonResultSavedWithThePlayoffs => save with
            {
                CompletedMatches = save.CompletedMatches.SkipLast(1).ToList(),
                PlayoffMatches = [save.CompletedMatches[^1]],
            },
            InjuryInAPreseasonMatch => save with
            {
                PreseasonMatches =
                [
                    firstPreseasonResult with
                    {
                        Injuries = [new SavedInjury(1, 60, firstPreseasonResult.Home.TeamId, firstPreseasonResult.Home.Skaters[0].PlayerId, InjuryType.BruisedFoot, InjuryCause.BlockedShot, 3)],
                    },
                    .. save.PreseasonMatches.Skip(1),
                ],
            },
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
                Injuries = [new SavedInjury(1, 60, firstResult.Home.TeamId, firstResult.Home.Skaters[0].PlayerId, InjuryType.Concussion, InjuryCause.Hit, 999)],
            }),
            InjuryToAPlayerWhoDidNotAppear => WithFirstResult(save, firstResult with
            {
                Injuries = [new SavedInjury(1, 60, firstResult.Home.TeamId, firstResult.Away.Skaters[0].PlayerId, InjuryType.BruisedFoot, InjuryCause.BlockedShot, 3)],
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
            MissingTimeOnIceBySituation => WithFirstResult(save, firstResult with
            {
                Home = firstResult.Home with
                {
                    Skaters = [firstResult.Home.Skaters[0] with { TimeOnIceBySituation = null! }, .. firstResult.Home.Skaters.Skip(1)],
                },
            }),
            TimeOnIceBySituationNotSummingToTimeOnIce => WithFirstResult(save, firstResult with
            {
                Home = firstResult.Home with
                {
                    Skaters =
                    [
                        firstResult.Home.Skaters[0] with
                        {
                            TimeOnIceBySituation = firstResult.Home.Skaters[0].TimeOnIceBySituation with
                            {
                                FiveOnFiveSeconds = firstResult.Home.Skaters[0].TimeOnIceBySituation.FiveOnFiveSeconds + 1,
                            },
                        },
                        .. firstResult.Home.Skaters.Skip(1),
                    ],
                },
            }),
            MissingPeriodTotals => WithFirstResult(save, firstResult with { Home = firstResult.Home with { Periods = null! } }),
            PeriodTotalsNotMatchingShots => WithFirstResult(save, firstResult with
            {
                Home = firstResult.Home with
                {
                    Periods = [firstResult.Home.Periods[0] with { Shots = firstResult.Home.Periods[0].Shots + 1 }, .. firstResult.Home.Periods.Skip(1)],
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
            MalformedTeamColour => WithFirstTeam(save, firstTeam with { PrimaryColour = "navy" }),
            MissingTeamColour => WithFirstTeam(save, firstTeam with { SecondaryColour = null! }),
            MissingArena => WithFirstTeam(save, firstTeam with { Arena = null! }),
            MatchingTeamColours => WithFirstTeam(save, firstTeam with { SecondaryColour = firstTeam.PrimaryColour }),
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

    /// <summary>
    /// The undamaged game every invalid-save case is compared with, played once and shared.
    /// </summary>
    /// <summary>
    /// A game played once and saved, which each case loads as its active game before trying to
    /// load a damaged copy, and how that game continues uninterrupted.
    /// </summary>
    public sealed class ControlGame
    {
        public ControlGame()
        {
            var manager = new GameManager();
            StartAtOpeningDay(manager);
            Before = Describe(Advance(manager, 5));
            var store = new MemorySaveStore();
            manager.SaveGame(store);
            Save = store.Saved!;
            Continuation = Describe(ContinueWithLineupChange(manager));
        }

        public GameSave Save { get; }

        public IReadOnlyList<string> Before { get; }

        public IReadOnlyList<string> Continuation { get; }
    }
}