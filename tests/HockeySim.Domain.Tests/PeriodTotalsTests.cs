using Xunit;

namespace HockeySim.Domain.Tests;

public sealed class PeriodTotalsTests
{
    private static readonly League League = TestLeague.Create();
    private static readonly ScheduledMatch Scheduled = new(new DateOnly(2026, 10, 1), League.Teams[0].Id, League.Teams[1].Id);

    [Fact]
    public void AMatchKeepsEachTeamsShotsExpectedGoalsAndPowerPlaysByPeriod()
    {
        var match = TestResults.Create(League, Scheduled, 2, 1);

        foreach (var side in new[] { match.Home, match.Away })
        {
            Assert.Equal([1, 2, 3], side.Periods.Select(period => period.Period));
            Assert.Equal(side.Shots, side.Periods.Sum(period => period.Shots));
            Assert.Equal(side.ExpectedGoals, side.Periods.Sum(period => period.ExpectedGoals), precision: 10);
            Assert.Equal(side.PowerPlayOpportunities, side.Periods.Sum(period => period.PowerPlayOpportunities));
        }
    }

    [Fact]
    public void PeriodTotalsCannotBeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeriodTotals(0, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeriodTotals(1, -1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeriodTotals(1, 0, -0.1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeriodTotals(1, 0, double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeriodTotals(1, 0, 0, -1));
    }

    [Fact]
    public void PeriodTotalsMustCoverRegulationInOrderFromTheFirst()
    {
        var side = TestResults.Create(League, Scheduled, 2, 1).Home;
        var periods = side.Periods;

        Assert.Throws<ArgumentException>(() => WithPeriods(side, [Merged(periods[0], periods[2], 1), periods[1]]));
        Assert.Throws<ArgumentException>(() => WithPeriods(side, [periods[0], periods[2], periods[1]]));
        Assert.Throws<ArgumentException>(() => WithPeriods(
            side, periods.Select(period => new PeriodTotals(period.Period + 1, period.Shots, period.ExpectedGoals, period.PowerPlayOpportunities))));
    }

    [Fact]
    public void PeriodTotalsMustAddUpToTheTeamsTotals()
    {
        var side = TestResults.Create(League, Scheduled, 2, 1).Home;
        var first = side.Periods[0];
        PeriodTotals[] Changed(PeriodTotals replacement) => [replacement, side.Periods[1], side.Periods[2]];

        Assert.NotNull(WithPeriods(side, side.Periods));
        Assert.Throws<ArgumentException>(() => WithPeriods(
            side, Changed(new PeriodTotals(1, first.Shots + 1, first.ExpectedGoals, first.PowerPlayOpportunities))));
        Assert.Throws<ArgumentException>(() => WithPeriods(
            side, Changed(new PeriodTotals(1, first.Shots, first.ExpectedGoals + 0.1, first.PowerPlayOpportunities))));
        Assert.Throws<ArgumentException>(() => WithPeriods(
            side, Changed(new PeriodTotals(1, first.Shots, first.ExpectedGoals, first.PowerPlayOpportunities + 1))));
    }

    [Fact]
    public void OvertimeIsPlayedOnlyInAMatchDecidedAfterRegulation()
    {
        var regulation = TestResults.Create(League, Scheduled, 2, 1);
        var overtime = TestResults.Create(League, Scheduled, 2, 1, MatchDecision.Overtime);

        Assert.Equal(4, overtime.Home.Periods.Count);
        Assert.Equal(4, TestResults.Create(League, Scheduled, 1, 1, MatchDecision.Shootout).Away.Periods.Count);
        Assert.Throws<ArgumentException>(() => TestResults.Match(Scheduled, overtime.Home, overtime.Away, MatchDecision.Regulation));
        Assert.Throws<ArgumentException>(() => TestResults.Match(Scheduled, regulation.Home, regulation.Away, MatchDecision.Overtime));
        Assert.Throws<ArgumentException>(() => TestResults.Match(Scheduled, overtime.Home, regulation.Away, MatchDecision.Overtime));
    }

    [Fact]
    public void TheSummariesCannotIncludeAPeriodThatWasNotPlayed()
    {
        var match = TestResults.Create(League, Scheduled, 2, 1);
        var lateGoal = match.Goals[^1];
        var goals = match.Goals.Take(match.Goals.Count - 1).Append(new MatchGoal(
            4,
            TimeSpan.FromMinutes(1),
            lateGoal.TeamId,
            lateGoal.ScorerId,
            lateGoal.PrimaryAssistId,
            lateGoal.SecondaryAssistId,
            lateGoal.Situation,
            lateGoal.IsEmptyNet));

        Assert.Throws<ArgumentException>(() => new CompletedMatch(
            Scheduled, match.Home, match.Away, MatchDecision.Regulation, goals, match.Penalties));
    }

    [Fact]
    public void ATeamCannotScoreMoreInAPeriodThanItShot()
    {
        var match = TestResults.Create(League, Scheduled, 2, 1);
        var home = match.Home;
        var shotless = WithPeriods(home,
        [
            new PeriodTotals(1, 0, home.Periods[0].ExpectedGoals, home.Periods[0].PowerPlayOpportunities),
            home.Periods[1],
            new PeriodTotals(3, home.Periods[2].Shots + home.Periods[0].Shots, 0, 0),
        ]);

        Assert.Throws<ArgumentException>(() => TestResults.Match(Scheduled, shotless, match.Away));
    }

    private static PeriodTotals Merged(PeriodTotals first, PeriodTotals second, int period) =>
        new(
            period,
            first.Shots + second.Shots,
            first.ExpectedGoals + second.ExpectedGoals,
            first.PowerPlayOpportunities + second.PowerPlayOpportunities);

    private static CompletedMatchTeam WithPeriods(CompletedMatchTeam side, IEnumerable<PeriodTotals> periods) =>
        new(side.TeamId, side.Score, side.Shots, side.PowerPlayOpportunities, side.Skaters, side.Goalie, side.ShotTotals, periods);
}