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
        };

        Assert.All(negatives, create => Assert.Throws<ArgumentOutOfRangeException>(() => create()));
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

        Assert.Equal(15, new CompletedMatchTeam(League.Teams[0].Id, 1, 15, skaters, goalie).Shots);
        Assert.Throws<ArgumentException>(() => new CompletedMatchTeam(League.Teams[0].Id, 1, 16, skaters, goalie));
    }

    [Fact]
    public void AConsistentMatchIsAcceptedAndExposesTeamTotals()
    {
        var match = Build();

        Assert.Equal(30, match.Home.ShotAttempts);
        Assert.Equal(1.5, match.Home.ExpectedGoals, precision: 10);
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
        double homeGoalieExpectedGoalsAgainst = 1.25)
    {
        var home = new CompletedMatchTeam(
            League.Teams[0].Id,
            2,
            20,
            [
                TestResults.Skater(
                    Id(), 2, shots: 20, shotAttempts: 30, expectedGoals: 1.5, plusMinus: homePlusMinus,
                    blockedShots: homeBlockedShots, faceoffsWon: homeFaceoffsWon, faceoffsLost: 8),
            ],
            TestResults.Goalie(Id(), 15, 1, homeGoalieExpectedGoalsAgainst));
        var away = new CompletedMatchTeam(
            League.Teams[1].Id,
            1,
            15,
            [
                TestResults.Skater(
                    Id(), 1, shots: 15, shotAttempts: 25, expectedGoals: 1.25, plusMinus: -1,
                    blockedShots: 4, faceoffsWon: 8, faceoffsLost: 10),
            ],
            TestResults.Goalie(Id(), 20, 2, 1.5));

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
        double expectedGoals = 0) =>
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
            expectedGoals);

    private static PlayerId Id() => new(Guid.NewGuid());
}