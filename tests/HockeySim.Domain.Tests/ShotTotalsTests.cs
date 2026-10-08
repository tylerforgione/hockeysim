using HockeySim.Domain;

using Xunit;

using static HockeySim.Domain.Tests.TestResults;

namespace HockeySim.Domain.Tests;

public sealed class ShotTotalsTests
{
    [Fact]
    public void SharesAreTheForCountsShareOfForAndAgainst()
    {
        var totals = Shots(
            attemptsFor: 30, attemptsAgainst: 20, unblockedFor: 24, unblockedAgainst: 16, shotsFor: 18, shotsAgainst: 12,
            goalsFor: 3, goalsAgainst: 1, expectedGoalsFor: 2.4, expectedGoalsAgainst: 1.6);

        Assert.Equal(0.6, totals.CorsiPercentage!.Value, precision: 10);
        Assert.Equal(0.6, totals.FenwickPercentage!.Value, precision: 10);
        Assert.Equal(0.6, totals.ShotsPercentage!.Value, precision: 10);
        Assert.Equal(0.75, totals.GoalsPercentage!.Value, precision: 10);
        Assert.Equal(0.6, totals.ExpectedGoalsPercentage!.Value, precision: 10);
    }

    [Fact]
    public void AShareIsUndefinedUntilEitherSideHasACount()
    {
        Assert.Null(ShotTotals.None.CorsiPercentage);
        Assert.Null(ShotTotals.None.FenwickPercentage);
        Assert.Null(ShotTotals.None.ShotsPercentage);
        Assert.Null(ShotTotals.None.GoalsPercentage);
        Assert.Null(ShotTotals.None.ExpectedGoalsPercentage);

        var onlyAgainst = Shots(attemptsAgainst: 2, shotsAgainst: 1, expectedGoalsAgainst: 0.1);
        Assert.Equal(0, onlyAgainst.CorsiPercentage);
        Assert.Equal(0, onlyAgainst.ShotsPercentage);
        Assert.Equal(0, onlyAgainst.ExpectedGoalsPercentage);
        Assert.Null(onlyAgainst.GoalsPercentage);
    }

    [Theory]
    [InlineData(5, 6, 4, 1)]
    [InlineData(5, 4, 5, 1)]
    [InlineData(5, 4, 3, 4)]
    [InlineData(-1, 0, 0, 0)]
    public void EachCountMustBeASubsetOfTheOneBeforeIt(int attempts, int unblocked, int shots, int goals)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Shots(
            attemptsFor: attempts, unblockedFor: unblocked, shotsFor: shots, goalsFor: goals));
        Assert.Throws<ArgumentOutOfRangeException>(() => Shots(
            attemptsAgainst: attempts, unblockedAgainst: unblocked, shotsAgainst: shots, goalsAgainst: goals));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-0.1)]
    public void ExpectedGoalsMustBeFiniteAndNonNegative(double expectedGoals)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Shots(expectedGoalsFor: expectedGoals));
        Assert.Throws<ArgumentOutOfRangeException>(() => Shots(expectedGoalsAgainst: expectedGoals));
    }

    [Fact]
    public void ReversingSwapsForAndAgainst()
    {
        var totals = Shots(
            attemptsFor: 9, attemptsAgainst: 7, unblockedFor: 8, unblockedAgainst: 5, shotsFor: 6, shotsAgainst: 4,
            goalsFor: 2, goalsAgainst: 1, expectedGoalsFor: 0.9, expectedGoalsAgainst: 0.4);

        Assert.Equal(
            Shots(
                attemptsFor: 7, attemptsAgainst: 9, unblockedFor: 5, unblockedAgainst: 8, shotsFor: 4, shotsAgainst: 6,
                goalsFor: 1, goalsAgainst: 2, expectedGoalsFor: 0.4, expectedGoalsAgainst: 0.9),
            totals.Reverse());
    }

    [Fact]
    public void ReversingSituationalTotalsAlsoSwapsThePowerPlayAndPenaltyKill()
    {
        var powerPlay = Shots(attemptsFor: 4, shotsFor: 2);
        var penaltyKill = Shots(attemptsAgainst: 3);
        var totals = new SituationalShotTotals(Shots(attemptsFor: 10), powerPlay, penaltyKill, Shots(attemptsAgainst: 1));

        var reversed = totals.Reverse();

        Assert.Equal(Shots(attemptsAgainst: 10), reversed.FiveOnFive);
        Assert.Equal(penaltyKill.Reverse(), reversed.PowerPlay);
        Assert.Equal(powerPlay.Reverse(), reversed.PenaltyKill);
        Assert.Equal(Shots(attemptsFor: 1), reversed.Other);
    }

    [Fact]
    public void SituationalTotalsAddBySituationAndSumToAll()
    {
        var first = new SituationalShotTotals(
            Shots(attemptsFor: 10, shotsFor: 5, expectedGoalsFor: 0.5),
            Shots(attemptsFor: 3),
            Shots(attemptsAgainst: 4),
            Shots(attemptsFor: 1));
        var second = In(StrengthSituation.FiveOnFive, Shots(attemptsFor: 2, attemptsAgainst: 6, expectedGoalsFor: 0.25));

        var sum = first.Add(second);

        Assert.Equal(Shots(attemptsFor: 12, attemptsAgainst: 6, shotsFor: 5, expectedGoalsFor: 0.75), sum.FiveOnFive);
        Assert.Equal(first.PowerPlay, sum.PowerPlay);
        Assert.Equal(Shots(attemptsFor: 16, attemptsAgainst: 10, shotsFor: 5, expectedGoalsFor: 0.75), sum.All);
        Assert.Equal(sum.PenaltyKill, sum[StrengthSituation.PenaltyKill]);
    }
}