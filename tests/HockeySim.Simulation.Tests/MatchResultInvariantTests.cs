using HockeySim.Domain;
using HockeySim.Simulation.Events;
using HockeySim.Simulation.Randomness;

using Xunit;

namespace HockeySim.Simulation.Tests;

public sealed class MatchResultInvariantTests
{
    private const int SeedCount = 500;

    private static readonly TimeSpan RegulationPeriodLength = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan OvertimeLength = TimeSpan.FromMinutes(5);

    [Fact]
    public void EveryResultIsValidAcrossManySeeds()
    {
        var home = TestTeams.Create("Home");
        var away = TestTeams.Create("Away");
        var match = TestTeams.CreateMatch(home, away);
        var simulator = new MatchSimulator();

        for (var seed = 0UL; seed < SeedCount; seed++)
        {
            var result = simulator.Simulate(match, OvertimeFormat.RegularSeason, MatchHealth.AllHealthy, new RandomState(seed));
            AssertValidResult(match, result);
        }
    }

    [Fact]
    public void RegulationOvertimeAndShootoutDecisionsAllOccur()
    {
        var match = TestTeams.CreateMatch(TestTeams.Create("Home"), TestTeams.Create("Away"));
        var simulator = new MatchSimulator();

        var decisions = Enumerable.Range(0, SeedCount)
            .Select(seed => simulator.Simulate(match, OvertimeFormat.RegularSeason, MatchHealth.AllHealthy, new RandomState((ulong)seed)).Decision)
            .ToHashSet();

        Assert.Equal(Enum.GetValues<MatchDecision>().ToHashSet(), decisions);
    }

    [Fact]
    public void BackupGoalieAndScratchedPlayersNeverAppear()
    {
        var home = TestTeams.Create("Home");
        var away = TestTeams.Create("Away");
        var match = TestTeams.CreateMatch(home, away);

        // The test teams scratch skaters and a third goalie, so both kinds of scratch are covered.
        Assert.All(new[] { home, away }, team =>
        {
            var scratched = team.Roster.Where(player => !team.Lineup.DressedPlayers.Contains(player)).ToList();
            Assert.Contains(scratched, player => player.Position == Position.Goalie);
            Assert.Contains(scratched, player => player.Position != Position.Goalie);
        });

        var excluded = new[] { home, away }
            .SelectMany(team => team.Roster
                .Where(player => !team.Lineup.DressedPlayers.Contains(player))
                .Append(team.Lineup.BackupGoalie))
            .Select(player => player.Id)
            .ToHashSet();
        var simulator = new MatchSimulator();

        for (var seed = 0UL; seed < SeedCount; seed++)
        {
            var result = simulator.Simulate(match, OvertimeFormat.RegularSeason, MatchHealth.AllHealthy, new RandomState(seed));
            var appearing = result.Goals
                .SelectMany(goal => new[] { goal.ScorerId, goal.PrimaryAssistId, goal.SecondaryAssistId })
                .OfType<PlayerId>()
                .Concat(result.Shootout?.Attempts.SelectMany(attempt => new[] { attempt.ShooterId, attempt.GoalieId }) ?? [])
                .Concat(new[] { result.Home, result.Away }.SelectMany(team =>
                    team.Skaters.Select(skater => skater.PlayerId).Append(team.Goalie.PlayerId)));

            Assert.DoesNotContain(appearing, excluded.Contains);
        }
    }

    private static void AssertValidResult(Match match, MatchResult result)
    {
        Assert.Equal(match.Home.Id, result.Home.TeamId);
        Assert.Equal(match.Away.Id, result.Away.TeamId);
        Assert.Equal(match.Home.Lineup.StartingGoalie.Id, result.Home.Goalie.PlayerId);
        Assert.Equal(match.Away.Lineup.StartingGoalie.Id, result.Away.Goalie.PlayerId);
        Assert.NotEqual(result.Home.Score, result.Away.Score);
        Assert.Equal(
            result.Home.Score > result.Away.Score ? match.Home.Id : match.Away.Id,
            result.WinnerId);

        AssertGoalsBelongToDressedSkaters(match.Home, result);
        AssertGoalsBelongToDressedSkaters(match.Away, result);
        AssertGoalsAreChronologicalWithinPeriods(result);

        var homeGoals = result.Goals.Count(goal => goal.TeamId == match.Home.Id);
        var awayGoals = result.Goals.Count(goal => goal.TeamId == match.Away.Id);
        Assert.Equal(result.Goals.Count, homeGoals + awayGoals);
        Assert.True(result.Home.Shots >= homeGoals);
        Assert.True(result.Away.Shots >= awayGoals);

        var regulationGoals = result.Goals.Where(goal => goal.Period <= MatchResult.RegulationPeriodCount).ToList();
        var overtimeGoals = result.Goals.Where(goal => goal.Period == MatchResult.OvertimePeriod).ToList();
        var regulationTied = regulationGoals.Count(goal => goal.TeamId == match.Home.Id)
            == regulationGoals.Count(goal => goal.TeamId == match.Away.Id);

        switch (result.Decision)
        {
            case MatchDecision.Regulation:
                Assert.False(regulationTied);
                Assert.Empty(overtimeGoals);
                Assert.Null(result.Shootout);
                Assert.Equal(homeGoals, result.Home.Score);
                Assert.Equal(awayGoals, result.Away.Score);
                break;

            case MatchDecision.Overtime:
                Assert.True(regulationTied);
                var winningGoal = Assert.Single(overtimeGoals);
                Assert.Same(winningGoal, result.Goals[^1]);
                Assert.Equal(result.WinnerId, winningGoal.TeamId);
                Assert.Null(result.Shootout);
                Assert.Equal(homeGoals, result.Home.Score);
                Assert.Equal(awayGoals, result.Away.Score);
                break;

            case MatchDecision.Shootout:
                Assert.True(regulationTied);
                Assert.Empty(overtimeGoals);
                AssertValidShootout(match, result);
                break;

            default:
                Assert.Fail($"Unexpected decision {result.Decision}.");
                break;
        }
    }

    private static void AssertValidShootout(Match match, MatchResult result)
    {
        var shootout = Assert.IsType<ShootoutResult>(result.Shootout);
        Assert.Equal(result.WinnerId, shootout.WinnerId);
        Assert.True(shootout.GoalsFor(shootout.WinnerId) > shootout.GoalsFor(LoserId(result)));

        // The shootout winner is credited exactly one goal that no player scored.
        Assert.Equal(1, Math.Abs(result.Home.Score - result.Away.Score));
        Assert.Equal(result.Goals.Count(goal => goal.TeamId == result.WinnerId) + 1, ScoreFor(result, result.WinnerId));
        Assert.Equal(result.Goals.Count(goal => goal.TeamId == LoserId(result)), ScoreFor(result, LoserId(result)));

        foreach (var attempt in shootout.Attempts)
        {
            var (shooting, defending) = attempt.TeamId == match.Home.Id
                ? (match.Home, match.Away)
                : (match.Away, match.Home);

            Assert.Contains(
                shooting.Lineup.DressedPlayers.Where(player => player.Position != Position.Goalie),
                player => player.Id == attempt.ShooterId);
            Assert.Equal(defending.Lineup.StartingGoalie.Id, attempt.GoalieId);
        }
    }

    private static void AssertGoalsBelongToDressedSkaters(Team team, MatchResult result)
    {
        var dressedSkaters = team.Lineup.DressedPlayers
            .Where(player => player.Position != Position.Goalie)
            .Select(player => player.Id)
            .ToHashSet();

        Assert.All(
            result.Goals.Where(goal => goal.TeamId == team.Id),
            goal => Assert.Contains(goal.ScorerId, dressedSkaters));
    }

    private static void AssertGoalsAreChronologicalWithinPeriods(MatchResult result)
    {
        Assert.All(result.Goals, goal =>
        {
            Assert.InRange(goal.Period, 1, MatchResult.OvertimePeriod);
            var periodLength = goal.Period == MatchResult.OvertimePeriod ? OvertimeLength : RegulationPeriodLength;
            Assert.InRange(goal.TimeInPeriod, TimeSpan.Zero, periodLength - TimeSpan.FromTicks(1));
        });

        var ordered = result.Goals.OrderBy(goal => goal.Period).ThenBy(goal => goal.TimeInPeriod);
        Assert.Equal(ordered, result.Goals);
    }

    private static TeamId LoserId(MatchResult result) =>
        result.WinnerId == result.Home.TeamId ? result.Away.TeamId : result.Home.TeamId;

    private static int ScoreFor(MatchResult result, TeamId teamId) =>
        result.Home.TeamId == teamId ? result.Home.Score : result.Away.Score;
}