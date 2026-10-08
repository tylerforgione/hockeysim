using HockeySim.Domain;

using Xunit;

namespace HockeySim.Simulation.Tests;

public sealed class ShiftAndFatigueTests
{
    private const int MatchCount = 200;

    [Fact]
    public void HigherLinesAndPairsPlayMoreButEveryGroupPlays()
    {
        var match = TestMatches.EvenMatch();
        var results = TestMatches.SimulateMany(match, MatchCount);
        var lineup = match.Home.Lineup;

        var lineMinutes = lineup.ForwardLines.Select(line => AverageMinutes(results, line.Centre.Id)).ToList();
        var pairMinutes = lineup.DefencePairs.Select(pair => AverageMinutes(results, pair.LeftDefence.Id)).ToList();

        Assert.True(lineMinutes.SequenceEqual(lineMinutes.OrderDescending()), $"Line minutes: {string.Join(", ", lineMinutes)}");
        Assert.True(pairMinutes.SequenceEqual(pairMinutes.OrderDescending()), $"Pair minutes: {string.Join(", ", pairMinutes)}");
        Assert.InRange(lineMinutes[0], 14, 24);
        Assert.InRange(lineMinutes[^1], 5, 14);
        Assert.InRange(pairMinutes[0], 18, 28);
        Assert.InRange(pairMinutes[^1], 12, 22);
    }

    [Fact]
    public void ALowStaminaLineTiresSoonerAndPlaysLessThanAHighStaminaLine()
    {
        Team WithTopLineStamina(string name, int stamina) => TestTeams.Create(name, (role, rating) =>
            rating == Rating.Stamina && role is { Kind: LineupRoleKind.ForwardLine, Index: 0 } ? stamina : TestTeams.AverageRating);

        var tireless = WithTopLineStamina("Tireless", 100);
        var tiring = WithTopLineStamina("Tiring", 0);
        var tirelessMinutes = AverageMinutes(
            TestMatches.SimulateMany(TestTeams.CreateMatch(tireless, TestTeams.Create("Opponent")), MatchCount),
            tireless.Lineup.ForwardLines[0].Centre.Id);
        var tiringMinutes = AverageMinutes(
            TestMatches.SimulateMany(TestTeams.CreateMatch(tiring, TestTeams.Create("Opponent")), MatchCount),
            tiring.Lineup.ForwardLines[0].Centre.Id);

        Assert.True(tirelessMinutes > tiringMinutes + 2, $"Tireless {tirelessMinutes:F1}, tiring {tiringMinutes:F1} minutes.");
    }

    private static double AverageMinutes(List<MatchResult> results, PlayerId playerId) =>
        results.Average(result => result.Home.Skaters.Concat(result.Away.Skaters)
            .Single(skater => skater.PlayerId == playerId)
            .TimeOnIce.TotalMinutes);
}