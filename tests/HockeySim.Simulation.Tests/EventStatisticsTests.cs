using HockeySim.Domain;
using HockeySim.Simulation.Events;

using Xunit;

namespace HockeySim.Simulation.Tests;

/// <summary>
/// Checks that every individual and team statistic reconciles with the play-by-play and with the
/// time played.
/// </summary>
public sealed class EventStatisticsTests
{
    private const int SeedCount = 300;
    private const int RegulationSeconds = 3 * 20 * 60;

    private static readonly Match Match = TestMatches.EvenMatch();
    private static readonly List<MatchResult> Results = TestMatches.SimulateMany(Match, SeedCount);

    [Fact]
    public void SkaterStatisticsCountTheirEventsInThePlayByPlay()
    {
        Assert.All(Results, result =>
        {
            var events = result.Events;
            foreach (var skater in result.Home.Skaters.Concat(result.Away.Skaters))
            {
                var id = skater.PlayerId;
                var attempts = events.OfType<ShotAttemptEvent>().Where(attempt => attempt.ShooterId == id).ToList();
                var goals = result.Goals.Where(goal => goal.ScorerId == id).ToList();

                Assert.Equal(goals.Count, skater.Goals);
                Assert.Equal(result.Goals.Count(goal => goal.PrimaryAssistId == id || goal.SecondaryAssistId == id), skater.Assists);
                Assert.Equal(goals.Count + attempts.Count(attempt => attempt.IsOnGoal), skater.Shots);
                Assert.Equal(goals.Count + attempts.Count, skater.ShotAttempts);
                Assert.Equal(events.OfType<HitEvent>().Count(hit => hit.HitterId == id), skater.Hits);
                Assert.Equal(events.OfType<ShotAttemptEvent>().Count(attempt => attempt.BlockerId == id), skater.BlockedShots);
                Assert.Equal(events.OfType<FaceoffEvent>().Count(faceoff => faceoff.WinnerId == id), skater.FaceoffsWon);
                Assert.Equal(events.OfType<FaceoffEvent>().Count(faceoff => faceoff.LoserId == id), skater.FaceoffsLost);
                Assert.Equal(events.OfType<TakeawayEvent>().Count(takeaway => takeaway.PlayerId == id), skater.Takeaways);
                Assert.Equal(events.OfType<GiveawayEvent>().Count(giveaway => giveaway.PlayerId == id), skater.Giveaways);
                Assert.Equal(
                    goals.Sum(goal => goal.ExpectedGoals) + attempts.Sum(attempt => attempt.ExpectedGoals ?? 0),
                    skater.ExpectedGoals,
                    precision: 9);
                Assert.True(skater.Goals <= skater.Shots && skater.Shots <= skater.ShotAttempts);
            }
        });
    }

    [Fact]
    public void PlusMinusCountsEachEvenStrengthGoalForEverySkaterOnTheIce()
    {
        Assert.All(Results, result =>
        {
            foreach (var (team, isHome) in new[] { (result.Home, true), (result.Away, false) })
            {
                foreach (var skater in team.Skaters)
                {
                    var expected = result.Goals.Sum(goal =>
                    {
                        var (ownSkaters, scoredByOwnTeam) = isHome
                            ? (goal.OnIce.HomeSkaters, goal.TeamId == result.Home.TeamId)
                            : (goal.OnIce.AwaySkaters, goal.TeamId == result.Away.TeamId);
                        return ownSkaters.Contains(skater.PlayerId) ? (scoredByOwnTeam ? 1 : -1) : 0;
                    });
                    Assert.Equal(expected, skater.PlusMinus);
                }

                // Every goal is at even strength, so each team's plus/minus nets to its goal
                // difference times the skaters on the ice.
                var regulationGoals = result.Goals.Where(goal => goal.Period <= MatchResult.RegulationPeriodCount);
                var overtimeGoals = result.Goals.Where(goal => goal.Period > MatchResult.RegulationPeriodCount);
                int Net(IEnumerable<GoalEvent> goals) => goals.Sum(goal => goal.TeamId == team.TeamId ? 1 : -1);
                Assert.Equal((5 * Net(regulationGoals)) + (3 * Net(overtimeGoals)), team.Skaters.Sum(skater => skater.PlusMinus));
            }
        });
    }

    [Fact]
    public void TimeOnIceAddsUpToTheSkatersOnTheIceForTheTimePlayed()
    {
        Assert.All(Results, result =>
        {
            var overtimeSeconds = (int)result.PlayingTime.TotalSeconds - RegulationSeconds;
            Assert.InRange(overtimeSeconds, 0, 5 * 60);
            switch (result.Decision)
            {
                case MatchDecision.Regulation:
                    Assert.Equal(0, overtimeSeconds);
                    break;
                case MatchDecision.Overtime:
                    Assert.Equal(result.Goals[^1].TimeInPeriod.TotalSeconds, overtimeSeconds);
                    break;
                case MatchDecision.Shootout:
                    Assert.Equal(5 * 60, overtimeSeconds);
                    break;
            }

            foreach (var team in new[] { result.Home, result.Away })
            {
                Assert.Equal(
                    (5 * RegulationSeconds) + (3 * overtimeSeconds),
                    team.Skaters.Sum(skater => skater.TimeOnIce.TotalSeconds));
                Assert.All(team.Skaters, skater => Assert.InRange(skater.TimeOnIce, TimeSpan.FromSeconds(1), result.PlayingTime));
                Assert.Equal(result.PlayingTime, team.Goalie.TimeOnIce);
            }
        });
    }

    [Fact]
    public void TeamAndGoalieTotalsReconcileWithTheOpponentsSkaters()
    {
        Assert.All(Results, result =>
        {
            foreach (var (team, opponent) in new[] { (result.Home, result.Away), (result.Away, result.Home) })
            {
                Assert.Equal(team.Skaters.Sum(skater => skater.Shots), team.Shots);
                Assert.Equal(opponent.Shots, team.Goalie.ShotsAgainst);
                Assert.Equal(opponent.Skaters.Sum(skater => skater.Goals), team.Goalie.GoalsAgainst);
                Assert.Equal(opponent.Skaters.Sum(skater => skater.ExpectedGoals), team.Goalie.ExpectedGoalsAgainst, precision: 9);
                Assert.Equal(team.Skaters.Sum(skater => skater.FaceoffsWon), opponent.Skaters.Sum(skater => skater.FaceoffsLost));
                Assert.True(
                    team.Skaters.Sum(skater => skater.BlockedShots)
                    <= opponent.Skaters.Sum(skater => skater.ShotAttempts - skater.Shots));
            }
        });
    }

    [Fact]
    public void OnlyCentresTakeFaceoffsInRegulation()
    {
        var centres = Match.Home.Lineup.ForwardLines.Concat(Match.Away.Lineup.ForwardLines)
            .Select(line => line.Centre.Id)
            .ToHashSet();

        Assert.All(
            Results.SelectMany(result => result.Events).OfType<FaceoffEvent>().Where(faceoff => faceoff.Period <= MatchResult.RegulationPeriodCount),
            faceoff =>
            {
                Assert.Contains(faceoff.WinnerId, centres);
                Assert.Contains(faceoff.LoserId, centres);
            });
    }

    [Fact]
    public void AggregateTotalsArePlausibleForEvenlyMatchedTeams()
    {
        // Wide bands that catch broken tuning; calibration to NHL averages is #53.
        double PerTeam(Func<MatchTeamResult, double> total) => Results.Average(result => (total(result.Home) + total(result.Away)) / 2);

        Assert.InRange(PerTeam(team => team.Skaters.Sum(skater => skater.Goals)), 2.0, 4.5);
        Assert.InRange(PerTeam(team => team.Shots), 20, 40);
        Assert.InRange(PerTeam(team => team.Skaters.Sum(skater => skater.ShotAttempts)), 40, 75);
        Assert.InRange(PerTeam(team => team.Skaters.Sum(skater => skater.BlockedShots)), 6, 22);
        Assert.InRange(PerTeam(team => team.Skaters.Sum(skater => skater.Hits)), 8, 35);
        Assert.InRange(PerTeam(team => team.Skaters.Sum(skater => skater.Takeaways)), 2, 15);
        Assert.InRange(PerTeam(team => team.Skaters.Sum(skater => skater.Giveaways)), 3, 18);
        Assert.InRange(PerTeam(team => team.Goalie.ExpectedGoalsAgainst), 2.0, 4.5);
        Assert.InRange(Results.Average(result => result.Events.OfType<FaceoffEvent>().Count()), 40, 85);

        var saves = Results.Sum(result => result.Home.Goalie.Saves + result.Away.Goalie.Saves);
        var shotsAgainst = Results.Sum(result => result.Home.Goalie.ShotsAgainst + result.Away.Goalie.ShotsAgainst);
        Assert.InRange(saves / (double)shotsAgainst, 0.87, 0.93);
    }

    [Fact]
    public void ScoringTracksExpectedGoalsForReferenceRatedPlayers()
    {
        // With every player at the reference rating, rested players score at the xG rate; fatigue
        // costs a little, so goals may trail xG slightly but never by much.
        var goals = Results.Sum(result => result.Goals.Count);
        var expected = Results.Sum(result => result.Home.Goalie.ExpectedGoalsAgainst + result.Away.Goalie.ExpectedGoalsAgainst);

        Assert.InRange(goals / expected, 0.85, 1.1);
    }
}