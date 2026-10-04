using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

using Xunit;

namespace HockeySim.Simulation.Tests;

/// <summary>
/// Statistical checks over many fixed seeds. Bands are deliberately wide and comparisons are
/// relative, so they catch broken tuning without breaking on deliberate rebalancing.
/// </summary>
public sealed class MatchStrengthTests
{
    private const int MatchCount = 400;

    [Fact]
    public void EvenlyMatchedTeamsProducePlausibleScoringAndSplitResults()
    {
        var home = TestTeams.Create("Home");
        var results = SimulateMany(home, TestTeams.Create("Away"));

        var averageGoals = results.Average(result => result.Goals.Count);
        var averageShots = results.Average(result => (result.Home.Shots + result.Away.Shots) / 2.0);
        var homeWinShare = results.Count(result => result.WinnerId == home.Id) / (double)MatchCount;

        Assert.InRange(averageGoals, 4.0, 8.0);
        Assert.InRange(averageShots, 20.0, 40.0);
        Assert.InRange(homeWinShare, 0.4, 0.6);
    }

    [Fact]
    public void MuchStrongerTeamWinsMostMatches()
    {
        var strong = TestTeams.Create("Strong", _ => 90);
        var weak = TestTeams.Create("Weak", _ => 40);

        Assert.True(WinShare(strong, weak) > 0.85);
    }

    [Fact]
    public void BetterStartingGoalieWinsMoreOften()
    {
        var betterGoalie = TestTeams.Create("Better goalie", role => role.Kind == LineupRoleKind.StartingGoalie ? 90 : TestTeams.AverageRating);
        var worseGoalie = TestTeams.Create("Worse goalie", role => role.Kind == LineupRoleKind.StartingGoalie ? 40 : TestTeams.AverageRating);

        Assert.True(WinShare(betterGoalie, worseGoalie) > 0.6);
    }

    [Fact]
    public void BackupGoalieAndScratchRatingsDoNotAffectTheResult()
    {
        var strongBench = TestTeams.Create(
            "Strong bench",
            role => role.Kind is LineupRoleKind.BackupGoalie or LineupRoleKind.Scratch ? 100 : TestTeams.AverageRating);
        var weakBench = TestTeams.Create(
            "Weak bench",
            role => role.Kind is LineupRoleKind.BackupGoalie or LineupRoleKind.Scratch ? 0 : TestTeams.AverageRating);
        var opponent = TestTeams.Create("Opponent");

        var withStrongBench = SimulateMany(strongBench, opponent);
        var withWeakBench = SimulateMany(weakBench, opponent);

        Assert.Equal(
            withStrongBench.Select(result => (result.Home.Score, result.Away.Score, result.Decision)),
            withWeakBench.Select(result => (result.Home.Score, result.Away.Score, result.Decision)));
    }

    [Fact]
    public void HigherForwardLinesAndDefencePairsAreMoreInvolved()
    {
        var home = TestTeams.Create("Home");
        var results = SimulateMany(home, TestTeams.Create("Away"));
        var homeGoals = results.SelectMany(result => result.Goals).Where(goal => goal.TeamId == home.Id).ToList();

        int GoalsBy(IEnumerable<Player> players)
        {
            var ids = players.Select(player => player.Id).ToHashSet();
            return homeGoals.Count(goal => ids.Contains(goal.ScorerId));
        }

        var lineGoals = home.Lineup.ForwardLines.Select(line => GoalsBy(line.Players)).ToList();
        var pairGoals = home.Lineup.DefencePairs.Select(pair => GoalsBy(pair.Players)).ToList();

        Assert.True(lineGoals[0] > lineGoals[^1] * 2, $"Line goals: {string.Join(", ", lineGoals)}");
        Assert.True(lineGoals[0] > lineGoals[2] && lineGoals[1] > lineGoals[^1], $"Line goals: {string.Join(", ", lineGoals)}");
        Assert.True(pairGoals[0] > pairGoals[^1], $"Pair goals: {string.Join(", ", pairGoals)}");
    }

    [Fact]
    public void StrengthOnTheTopLineMattersMoreThanOnTheFourthLine()
    {
        const int StrongRating = 90;
        var strongTopLine = TestTeams.Create(
            "Strong top line",
            role => role is { Kind: LineupRoleKind.ForwardLine, Index: 0 } ? StrongRating : TestTeams.AverageRating);
        var strongFourthLine = TestTeams.Create(
            "Strong fourth line",
            role => role is { Kind: LineupRoleKind.ForwardLine, Index: 3 } ? StrongRating : TestTeams.AverageRating);

        Assert.True(WinShare(strongTopLine, strongFourthLine) > 0.55);
    }

    [Fact]
    public void BetterSkatersWinMoreOftenWithEqualGoalies()
    {
        var betterSkaters = TestTeams.Create(
            "Better skaters",
            role => role.Kind == LineupRoleKind.StartingGoalie ? TestTeams.AverageRating : 75);
        var worseSkaters = TestTeams.Create(
            "Worse skaters",
            role => role.Kind == LineupRoleKind.StartingGoalie ? TestTeams.AverageRating : 55);

        Assert.True(WinShare(betterSkaters, worseSkaters) > 0.7);
    }

    /// <summary>
    /// Plays half the matches with each team at home, so the comparison is about the teams and
    /// not the side they play on.
    /// </summary>
    private static double WinShare(Team team, Team opponent)
    {
        var wins = SimulateMany(team, opponent, MatchCount / 2).Count(result => result.WinnerId == team.Id)
            + SimulateMany(opponent, team, MatchCount / 2).Count(result => result.WinnerId == team.Id);
        return wins / (double)MatchCount;
    }

    private static List<MatchResult> SimulateMany(Team home, Team away, int count = MatchCount)
    {
        var match = TestTeams.CreateMatch(home, away);
        var simulator = new MatchSimulator();
        return Enumerable.Range(0, count)
            .Select(seed => simulator.Simulate(match, new RandomState((ulong)seed)))
            .ToList();
    }
}