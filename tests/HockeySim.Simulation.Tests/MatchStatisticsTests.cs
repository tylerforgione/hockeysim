using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

using Xunit;

namespace HockeySim.Simulation.Tests;

public sealed class MatchStatisticsTests
{
    private const int SeedCount = 500;

    [Fact]
    public void IndividualStatisticsReconcileWithTheScoreForEveryDecision()
    {
        var match = TestTeams.CreateMatch(TestTeams.Create("Home"), TestTeams.Create("Away"));
        var simulator = new MatchSimulator();
        var decisions = new HashSet<MatchDecision>();

        for (var seed = 0UL; seed < SeedCount; seed++)
        {
            var result = simulator.Simulate(match, new RandomState(seed));
            decisions.Add(result.Decision);

            AssertTeamStatisticsReconcile(result, result.Home, result.Away);
            AssertTeamStatisticsReconcile(result, result.Away, result.Home);
        }

        Assert.Equal(Enum.GetValues<MatchDecision>().ToHashSet(), decisions);
    }

    [Fact]
    public void AssistsGoToDistinctDressedTeammatesOfTheScorer()
    {
        var home = TestTeams.Create("Home");
        var away = TestTeams.Create("Away");
        var match = TestTeams.CreateMatch(home, away);
        var simulator = new MatchSimulator();

        for (var seed = 0UL; seed < SeedCount; seed++)
        {
            foreach (var goal in simulator.Simulate(match, new RandomState(seed)).Goals)
            {
                var team = goal.TeamId == home.Id ? home : away;
                var teammates = DressedSkaterIds(team);
                teammates.Remove(goal.ScorerId);

                if (goal.PrimaryAssistId is { } primary)
                {
                    Assert.Contains(primary, teammates);
                }
                else
                {
                    Assert.Null(goal.SecondaryAssistId);
                }

                if (goal.SecondaryAssistId is { } secondary)
                {
                    Assert.Contains(secondary, teammates);
                    Assert.NotEqual(goal.PrimaryAssistId, secondary);
                }
            }
        }
    }

    [Fact]
    public void EveryDressedSkaterAndOnlyTheStartingGoalieAppear()
    {
        var home = TestTeams.Create("Home");
        var away = TestTeams.Create("Away");
        var match = TestTeams.CreateMatch(home, away);
        var simulator = new MatchSimulator();

        for (var seed = 0UL; seed < 50; seed++)
        {
            var result = simulator.Simulate(match, new RandomState(seed));

            foreach (var (team, teamResult) in new[] { (home, result.Home), (away, result.Away) })
            {
                var appearing = teamResult.Skaters.Select(skater => skater.PlayerId).ToList();

                Assert.Equal(appearing.Count, appearing.Distinct().Count());
                Assert.Equal(DressedSkaterIds(team), appearing.ToHashSet());
                Assert.All(teamResult.Skaters, skater => Assert.Equal(1, skater.GamesPlayed));
                Assert.Equal(team.Lineup.StartingGoalie.Id, teamResult.Goalie.PlayerId);
                Assert.Equal(1, teamResult.Goalie.GamesPlayed);
            }
        }
    }

    [Fact]
    public void ShootoutAttemptsDoNotCountInIndividualStatistics()
    {
        var match = TestTeams.CreateMatch(TestTeams.Create("Home"), TestTeams.Create("Away"));
        var simulator = new MatchSimulator();
        var shootouts = Enumerable.Range(0, SeedCount)
            .Select(seed => simulator.Simulate(match, new RandomState((ulong)seed)))
            .Where(result => result.Decision == MatchDecision.Shootout)
            .ToList();

        Assert.NotEmpty(shootouts);
        Assert.Contains(shootouts, result => result.Shootout!.Attempts.Any(attempt => attempt.Scored));

        foreach (var result in shootouts)
        {
            foreach (var (team, opponent) in new[] { (result.Home, result.Away), (result.Away, result.Home) })
            {
                var playerGoals = result.Goals.Count(goal => goal.TeamId == team.TeamId);
                var opponentPlayerGoals = result.Goals.Count(goal => goal.TeamId == opponent.TeamId);

                Assert.Equal(playerGoals, team.Skaters.Sum(skater => skater.Goals));
                Assert.Equal(opponentPlayerGoals, team.Goalie.GoalsAgainst);
                Assert.Equal(opponent.Shots, team.Goalie.ShotsAgainst);
            }

            // Only the deciding goal separates the score from individual scoring.
            var winner = result.WinnerId == result.Home.TeamId ? result.Home : result.Away;
            Assert.Equal(winner.Score - 1, winner.Skaters.Sum(skater => skater.Goals));
        }
    }

    [Fact]
    public void GoalieFacingNoShotsHasNoSavePercentageAndSkatersWithoutProductionStillAppear()
    {
        // A team with no ability rarely gets a shot against a dominant one, so some seeds give
        // the dominant goalie a match without facing a shot.
        var dominant = TestTeams.Create("Dominant", _ => 100);
        var helpless = TestTeams.Create("Helpless", _ => 0);
        var match = TestTeams.CreateMatch(dominant, helpless);
        var simulator = new MatchSimulator();

        var shutout = Enumerable.Range(0, 200)
            .Select(seed => simulator.Simulate(match, new RandomState((ulong)seed)))
            .FirstOrDefault(result => result.Away.Shots == 0);

        Assert.NotNull(shutout);
        var goalie = shutout.Home.Goalie;
        Assert.Equal(0, goalie.ShotsAgainst);
        Assert.Equal(0, goalie.Saves);
        Assert.Equal(0, goalie.GoalsAgainst);
        Assert.Null(goalie.SavePercentage);
        Assert.Equal(1, goalie.GamesPlayed);

        Assert.Equal(DressedSkaterIds(helpless), shutout.Away.Skaters.Select(skater => skater.PlayerId).ToHashSet());
        Assert.All(shutout.Away.Skaters, skater =>
        {
            Assert.Equal(1, skater.GamesPlayed);
            Assert.Equal(0, skater.Goals);
            Assert.Equal(0, skater.Assists);
            Assert.Equal(0, skater.Points);
        });
    }

    [Fact]
    public void SavePercentageIsSavesOverShotsAgainst()
    {
        var goalie = new GoalieMatchStatistics(new PlayerId(Guid.NewGuid()), ShotsAgainst: 30, GoalsAgainst: 3);

        Assert.Equal(27, goalie.Saves);
        Assert.Equal(0.9, goalie.SavePercentage!.Value, precision: 10);
    }

    [Fact]
    public void MostGoalsAreAssistedAndHigherLinesProduceMorePoints()
    {
        var home = TestTeams.Create("Home");
        var match = TestTeams.CreateMatch(home, TestTeams.Create("Away"));
        var simulator = new MatchSimulator();
        var results = Enumerable.Range(0, 400)
            .Select(seed => simulator.Simulate(match, new RandomState((ulong)seed)))
            .ToList();

        var goals = results.Sum(result => result.Goals.Count);
        var assists = results.Sum(result => result.Home.Skaters.Concat(result.Away.Skaters).Sum(skater => skater.Assists));
        Assert.InRange(assists / (double)goals, 1.2, 1.9);

        var pointsById = results
            .SelectMany(result => result.Home.Skaters)
            .GroupBy(skater => skater.PlayerId)
            .ToDictionary(group => group.Key, group => group.Sum(skater => skater.Points));
        var linePoints = home.Lineup.ForwardLines
            .Select(line => line.Players.Sum(player => pointsById[player.Id]))
            .ToList();

        Assert.True(linePoints[0] > linePoints[^1] * 2, $"Line points: {string.Join(", ", linePoints)}");
    }

    /// <summary>
    /// Checks one team's individual statistics against its goal events, its score, and the
    /// opponent's totals.
    /// </summary>
    private static void AssertTeamStatisticsReconcile(MatchResult result, MatchTeamResult team, MatchTeamResult opponent)
    {
        var teamGoals = result.Goals.Where(goal => goal.TeamId == team.TeamId).ToList();
        var opponentGoalCount = result.Goals.Count(goal => goal.TeamId == opponent.TeamId);
        var assistCount = teamGoals.Count(goal => goal.PrimaryAssistId is not null)
            + teamGoals.Count(goal => goal.SecondaryAssistId is not null);
        var shootoutBonus = result.Shootout?.WinnerId == team.TeamId ? 1 : 0;

        Assert.Equal(teamGoals.Count, team.Skaters.Sum(skater => skater.Goals));
        Assert.Equal(team.Score, team.Skaters.Sum(skater => skater.Goals) + shootoutBonus);
        Assert.Equal(assistCount, team.Skaters.Sum(skater => skater.Assists));
        Assert.All(team.Skaters, skater =>
        {
            Assert.Equal(teamGoals.Count(goal => goal.ScorerId == skater.PlayerId), skater.Goals);
            Assert.Equal(skater.Goals + skater.Assists, skater.Points);
            Assert.True(skater.Goals >= 0 && skater.Assists >= 0);
        });

        var goalie = team.Goalie;
        Assert.Equal(opponent.Shots, goalie.ShotsAgainst);
        Assert.Equal(opponentGoalCount, goalie.GoalsAgainst);
        Assert.Equal(goalie.ShotsAgainst, goalie.Saves + goalie.GoalsAgainst);
        Assert.True(goalie.Saves >= 0);
        if (goalie.ShotsAgainst > 0)
        {
            Assert.InRange(goalie.SavePercentage!.Value, 0.0, 1.0);
        }
    }

    private static HashSet<PlayerId> DressedSkaterIds(Team team) =>
        team.Lineup.DressedPlayers
            .Where(player => player.Position != Position.Goalie)
            .Select(player => player.Id)
            .ToHashSet();
}