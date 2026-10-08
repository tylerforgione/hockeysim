using Xunit;

namespace HockeySim.Domain.Tests;

public sealed class BoxScoreTests
{
    private static readonly League League = TestLeague.Create();
    private static readonly ScheduledMatch Scheduled = new(new DateOnly(2026, 10, 1), League.Teams[0].Id, League.Teams[1].Id);

    [Theory]
    [InlineData(2, 1, 3)]
    [InlineData(1, 3, 2)]
    public void ASkaterCannotScoreMoreThanTheirShotsOrShootMoreThanTheirAttempts(int goals, int shots, int shotAttempts)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TestResults.Skater(Id(), goals, shots: shots, shotAttempts: shotAttempts));
    }

    [Fact]
    public void SkaterCountsCannotBeNegative()
    {
        var negatives = new Func<SkaterBoxScore>[]
        {
            () => Skater(goals: -1),
            () => Skater(assists: -1),
            () => Skater(hits: -1),
            () => Skater(blockedShots: -1),
            () => Skater(faceoffsWon: -1),
            () => Skater(faceoffsLost: -1),
            () => Skater(takeaways: -1),
            () => Skater(giveaways: -1),
            () => Skater(penaltyMinutes: -1),
            () => Skater(goals: 1, powerPlayGoals: -1),
            () => Skater(assists: 1, powerPlayAssists: -1),
            () => Skater(goals: 1, shorthandedGoals: -1),
            () => Skater(assists: 1, shorthandedAssists: -1),
            () => Skater(goals: 1, emptyNetGoals: -1),
        };

        Assert.All(negatives, create => Assert.Throws<ArgumentOutOfRangeException>(() => create()));
    }

    [Fact]
    public void EmptyNetGoalsAreCountedAmongTheSkatersGoals()
    {
        Assert.Equal(1, Skater(goals: 2, emptyNetGoals: 1).EmptyNetGoals);
        Assert.Throws<ArgumentOutOfRangeException>(() => Skater(goals: 1, emptyNetGoals: 2));
    }

    [Fact]
    public void AGoalieIsNotChargedWithTheOpponentsEmptyNetGoals()
    {
        // One of the home team's two goals and twenty shots went into an empty net.
        var match = Build(homeEmptyNetGoals: 1, awayGoalieShotsAgainst: 19, awayGoalieGoalsAgainst: 1);
        Assert.Equal(1, match.Home.EmptyNetGoals);
        Assert.Equal(0, match.Away.EmptyNetGoals);
        Assert.Equal(18, match.Away.Goalie.Saves);

        Assert.Throws<ArgumentException>(() =>
            Build(homeEmptyNetGoals: 1, awayGoalieShotsAgainst: 20, awayGoalieGoalsAgainst: 2));
        Assert.Throws<ArgumentException>(() =>
            Build(homeEmptyNetGoals: 1, awayGoalieShotsAgainst: 20, awayGoalieGoalsAgainst: 1));
        Assert.Throws<ArgumentException>(() =>
            Build(homeEmptyNetGoals: 0, awayGoalieShotsAgainst: 19, awayGoalieGoalsAgainst: 1));
    }

    [Fact]
    public void ANegativePlusMinusIsAllowed()
    {
        Assert.Equal(-3, Skater(plusMinus: -3).PlusMinus);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(0.5)]
    public void TimeOnIceMustBeANonNegativeWholeNumberOfSeconds(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);

        Assert.Throws<ArgumentOutOfRangeException>(() => Skater(timeOnIce: time));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GoalieBoxScore(Id(), 0, 0, 0, time));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.1)]
    public void ExpectedGoalsMustBeFiniteAndNonNegative(double expectedGoals)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Skater(expectedGoals: expectedGoals));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GoalieBoxScore(Id(), 0, 0, expectedGoals, TimeSpan.Zero));
    }

    [Fact]
    public void ATeamsShotsMustEqualItsSkatersShotsOnGoal()
    {
        var skaters = new[] { TestResults.Skater(Id(), 1, shots: 10), TestResults.Skater(Id(), shots: 5) };
        var goalie = TestResults.Goalie(Id(), 20, 2);

        Assert.Equal(15, new CompletedMatchTeam(League.Teams[0].Id, 1, 15, 0, skaters, goalie).Shots);
        Assert.Throws<ArgumentException>(() => new CompletedMatchTeam(League.Teams[0].Id, 1, 16, 0, skaters, goalie));
    }

    [Fact]
    public void AConsistentMatchIsAcceptedAndExposesTeamTotals()
    {
        var match = Build();

        Assert.Equal(30, match.Home.ShotAttempts);
        Assert.Equal(1.5, match.Home.ExpectedGoals, precision: 10);
        Assert.Equal(3, match.Home.PowerPlayOpportunities);
        Assert.Equal(1, match.Home.PowerPlayGoals);
        Assert.Equal(0, match.Home.ShorthandedGoals);
        Assert.Equal(4, match.Home.PenaltyMinutes);
    }

    [Theory]
    [InlineData(2, 1, 0, 0)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(0, 0, 2, 0)]
    public void SpecialTeamsGoalsAndAssistsAreCountedAmongTheSkatersGoalsAndAssists(
        int powerPlayGoals,
        int shorthandedGoals,
        int powerPlayAssists,
        int shorthandedAssists)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Skater(
            goals: 2,
            assists: 1,
            powerPlayGoals: powerPlayGoals,
            shorthandedGoals: shorthandedGoals,
            powerPlayAssists: powerPlayAssists,
            shorthandedAssists: shorthandedAssists));
    }

    [Fact]
    public void ASkatersPowerPlayAndShorthandedPointsAddTheirGoalsAndAssists()
    {
        var skater = Skater(goals: 2, assists: 2, powerPlayGoals: 1, powerPlayAssists: 1, shorthandedGoals: 1);

        Assert.Equal(2, skater.PowerPlayPoints);
        Assert.Equal(1, skater.ShorthandedPoints);
    }

    [Fact]
    public void PowerPlayOpportunitiesCannotBeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CompletedMatchTeam(
            League.Teams[0].Id, 0, 0, -1, [Skater()], TestResults.Goalie(Id(), 0, 0)));
    }

    [Fact]
    public void APowerPlayGoalNeedsAPowerPlayOpportunity()
    {
        Assert.Equal(1, Build(homePowerPlayOpportunities: 1).Home.PowerPlayGoals);
        Assert.Throws<ArgumentException>(() => Build(homePowerPlayOpportunities: 0));
    }

    [Fact]
    public void AShorthandedGoalNeedsTheOpponentToHaveHadAPowerPlay()
    {
        Assert.Equal(1, Build(homePowerPlayGoals: 0, homeShorthandedGoals: 1).Home.ShorthandedGoals);
        Assert.Throws<ArgumentException>(() => Build(
            homePowerPlayGoals: 0, homeShorthandedGoals: 1, awayPowerPlayOpportunities: 0));
    }

    [Fact]
    public void ATeamCannotRecordMoreThanTwoAssistsPerPowerPlayOrShorthandedGoal()
    {
        var team = League.Teams[0].Id;
        var goalie = TestResults.Goalie(Id(), 0, 0);

        // Two goals allow four assists in all, but only one of the goals was on the power play.
        Assert.Throws<ArgumentException>(() => new CompletedMatchTeam(
            team,
            2,
            2,
            1,
            [
                TestResults.Skater(Id(), 2, shots: 2, powerPlayGoals: 1),
                TestResults.Skater(Id(), 0, 2, powerPlayAssists: 2),
                TestResults.Skater(Id(), 0, 1, powerPlayAssists: 1),
            ],
            goalie));
        Assert.Throws<ArgumentException>(() => new CompletedMatchTeam(
            team,
            2,
            2,
            1,
            [
                TestResults.Skater(Id(), 2, shots: 2, shorthandedGoals: 1),
                TestResults.Skater(Id(), 0, 2, shorthandedAssists: 2),
                TestResults.Skater(Id(), 0, 1, shorthandedAssists: 1),
            ],
            goalie));
    }

    [Fact]
    public void EachGoaliesExpectedGoalsAgainstMustMatchTheOpponentsExpectedGoals()
    {
        Assert.Throws<ArgumentException>(() => Build(homeGoalieExpectedGoalsAgainst: 1.35));

        // Totals summed in another order differ only by rounding, which is accepted.
        var match = Build(homeGoalieExpectedGoalsAgainst: 1.25 + 1e-12);
        Assert.Equal(1.25, match.Home.Goalie.ExpectedGoalsAgainst, precision: 10);
    }

    [Fact]
    public void EveryFaceoffWonByOneTeamMustBeLostByTheOther()
    {
        Assert.Throws<ArgumentException>(() => Build(homeFaceoffsWon: 11));
    }

    [Fact]
    public void ATeamCannotBlockMoreShotsThanTheOpponentFailedToGetOnGoal()
    {
        // The away team's 25 attempts include 15 shots on goal, so at most 10 can be blocked.
        Assert.Equal(10, Build(homeBlockedShots: 10).Home.Skaters.Sum(skater => skater.BlockedShots));
        Assert.Throws<ArgumentException>(() => Build(homeBlockedShots: 11));
    }

    [Fact]
    public void ASkatersPlusMinusCannotExceedTheGoalsScoredInTheMatch()
    {
        Assert.Equal(-3, Build(homePlusMinus: -3).Home.Skaters[0].PlusMinus);
        Assert.Throws<ArgumentException>(() => Build(homePlusMinus: 4));
    }

    /// <summary>
    /// A 2-1 regulation result whose statistics reconcile, apart from the one value a test changes.
    /// </summary>
    private static CompletedMatch Build(
        int homeFaceoffsWon = 10,
        int homeBlockedShots = 3,
        int homePlusMinus = 1,
        double homeGoalieExpectedGoalsAgainst = 1.25,
        int homePowerPlayGoals = 1,
        int homeShorthandedGoals = 0,
        int homePowerPlayOpportunities = 3,
        int awayPowerPlayOpportunities = 2,
        int homeEmptyNetGoals = 0,
        int awayGoalieShotsAgainst = 20,
        int awayGoalieGoalsAgainst = 2)
    {
        var home = new CompletedMatchTeam(
            League.Teams[0].Id,
            2,
            20,
            homePowerPlayOpportunities,
            [
                TestResults.Skater(
                    Id(), 2, shots: 20, shotAttempts: 30, expectedGoals: 1.5, plusMinus: homePlusMinus,
                    blockedShots: homeBlockedShots, faceoffsWon: homeFaceoffsWon, faceoffsLost: 8,
                    penaltyMinutes: 4, powerPlayGoals: homePowerPlayGoals, shorthandedGoals: homeShorthandedGoals,
                    emptyNetGoals: homeEmptyNetGoals),
            ],
            TestResults.Goalie(Id(), 15, 1, homeGoalieExpectedGoalsAgainst));
        var away = new CompletedMatchTeam(
            League.Teams[1].Id,
            1,
            15,
            awayPowerPlayOpportunities,
            [
                TestResults.Skater(
                    Id(), 1, shots: 15, shotAttempts: 25, expectedGoals: 1.25, plusMinus: -1,
                    blockedShots: 4, faceoffsWon: 8, faceoffsLost: 10),
            ],
            TestResults.Goalie(Id(), awayGoalieShotsAgainst, awayGoalieGoalsAgainst, 1.5));

        return new CompletedMatch(Scheduled, home, away, MatchDecision.Regulation);
    }

    private static SkaterBoxScore Skater(
        int goals = 0,
        int assists = 0,
        int plusMinus = 0,
        TimeSpan? timeOnIce = null,
        int hits = 0,
        int blockedShots = 0,
        int faceoffsWon = 0,
        int faceoffsLost = 0,
        int takeaways = 0,
        int giveaways = 0,
        double expectedGoals = 0,
        int penaltyMinutes = 0,
        int powerPlayGoals = 0,
        int powerPlayAssists = 0,
        int shorthandedGoals = 0,
        int shorthandedAssists = 0,
        int emptyNetGoals = 0) =>
        new(
            Id(),
            goals,
            assists,
            plusMinus,
            timeOnIce ?? TimeSpan.FromMinutes(12),
            shots: Math.Max(goals, 0),
            shotAttempts: Math.Max(goals, 0),
            hits,
            blockedShots,
            faceoffsWon,
            faceoffsLost,
            takeaways,
            giveaways,
            expectedGoals,
            penaltyMinutes,
            powerPlayGoals,
            powerPlayAssists,
            shorthandedGoals,
            shorthandedAssists,
            emptyNetGoals);

    private static PlayerId Id() => new(Guid.NewGuid());
}