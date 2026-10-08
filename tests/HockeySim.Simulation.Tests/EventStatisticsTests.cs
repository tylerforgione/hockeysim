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
                    goals.Sum(goal => goal.ExpectedGoals ?? 0) + attempts.Sum(attempt => attempt.ExpectedGoals ?? 0),
                    skater.ExpectedGoals,
                    precision: 9);
                Assert.True(skater.Goals <= skater.Shots && skater.Shots <= skater.ShotAttempts);

                Assert.Equal(events.OfType<PenaltyEvent>().Where(penalty => penalty.PlayerId == id).Sum(penalty => penalty.Minutes), skater.PenaltyMinutes);
                Assert.Equal(goals.Count(goal => goal.Situation == GoalSituation.PowerPlay), skater.PowerPlayGoals);
                Assert.Equal(goals.Count(goal => goal.Situation == GoalSituation.Shorthanded), skater.ShorthandedGoals);
                Assert.Equal(goals.Count(goal => goal.IsEmptyNet), skater.EmptyNetGoals);
                Assert.Equal(AssistsOn(result, id, GoalSituation.PowerPlay), skater.PowerPlayAssists);
                Assert.Equal(AssistsOn(result, id, GoalSituation.Shorthanded), skater.ShorthandedAssists);
                Assert.Equal(skater.PowerPlayGoals + skater.PowerPlayAssists, skater.PowerPlayPoints);
                Assert.Equal(skater.ShorthandedGoals + skater.ShorthandedAssists, skater.ShorthandedPoints);
            }

            foreach (var team in new[] { result.Home, result.Away })
            {
                Assert.Equal(result.Goals.Count(goal => goal.TeamId == team.TeamId && goal.Situation == GoalSituation.PowerPlay), team.PowerPlayGoals);
                Assert.Equal(new ManpowerReplay(result).PowerPlayOpportunities(team.TeamId), team.PowerPlayOpportunities);
            }
        });
    }

    [Fact]
    public void PlusMinusCountsEachEvenStrengthAndShorthandedGoalForEverySkaterOnTheIce()
    {
        Assert.All(Results, result =>
        {
            // Power-play and penalty-shot goals do not count.
            var counted = result.Goals
                .Where(goal => goal.Situation is GoalSituation.EvenStrength or GoalSituation.Shorthanded)
                .ToList();
            foreach (var (team, isHome) in new[] { (result.Home, true), (result.Away, false) })
            {
                foreach (var skater in team.Skaters)
                {
                    var expected = counted.Sum(goal =>
                    {
                        var (ownSkaters, scoredByOwnTeam) = isHome
                            ? (goal.OnIce.HomeSkaters, goal.TeamId == result.Home.TeamId)
                            : (goal.OnIce.AwaySkaters, goal.TeamId == result.Away.TeamId);
                        return ownSkaters.Contains(skater.PlayerId) ? (scoredByOwnTeam ? 1 : -1) : 0;
                    });
                    Assert.Equal(expected, skater.PlusMinus);
                }

                // Each counted goal moves the team's plus/minus by the number of its skaters on the ice.
                Assert.Equal(
                    counted.Sum(goal => (goal.TeamId == team.TeamId ? 1 : -1)
                        * (isHome ? goal.OnIce.HomeSkaters.Count : goal.OnIce.AwaySkaters.Count)),
                    team.Skaters.Sum(skater => skater.PlusMinus));
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

            // The skaters share the time each player the penalties allowed was on the ice, plus the
            // time an extra attacker replaced the goalie.
            var replay = new ManpowerReplay(result);
            foreach (var team in new[] { result.Home, result.Away })
            {
                var pulledSeconds = (result.PlayingTime - team.Goalie.TimeOnIce).TotalSeconds;
                Assert.Equal(
                    replay.ManpowerSeconds(team.TeamId) + pulledSeconds,
                    team.Skaters.Sum(skater => skater.TimeOnIce.TotalSeconds));
                Assert.All(team.Skaters, skater => Assert.InRange(skater.TimeOnIce, TimeSpan.FromSeconds(1), result.PlayingTime));
                Assert.InRange(team.Goalie.TimeOnIce, result.PlayingTime - TimeSpan.FromMinutes(5), result.PlayingTime);
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
                // The goalie is not charged with goals scored into the empty net while pulled.
                var emptyNetGoals = opponent.Skaters.Sum(skater => skater.EmptyNetGoals);
                Assert.Equal(result.Goals.Count(goal => goal.TeamId == opponent.TeamId && goal.IsEmptyNet), emptyNetGoals);
                Assert.Equal(opponent.Shots - emptyNetGoals, team.Goalie.ShotsAgainst);
                Assert.Equal(opponent.Skaters.Sum(skater => skater.Goals) - emptyNetGoals, team.Goalie.GoalsAgainst);
                Assert.Equal(opponent.Skaters.Sum(skater => skater.ExpectedGoals), team.Goalie.ExpectedGoalsAgainst, precision: 9);
                Assert.Equal(team.Skaters.Sum(skater => skater.FaceoffsWon), opponent.Skaters.Sum(skater => skater.FaceoffsLost));
                Assert.True(
                    team.Skaters.Sum(skater => skater.BlockedShots)
                    <= opponent.Skaters.Sum(skater => skater.ShotAttempts - skater.Shots));
            }
        });
    }

    [Fact]
    public void CentresTakeEvenStrengthFaceoffsUnlessOneIsInTheBox()
    {
        var centres = Match.Home.Lineup.ForwardLines.Concat(Match.Away.Lineup.ForwardLines)
            .Select(line => line.Centre.Id)
            .ToHashSet();
        var checkpoints = Results.SelectMany(result => new ManpowerReplay(result).Checkpoints)
            .Where(checkpoint => checkpoint.Event is FaceoffEvent { Period: <= MatchResult.RegulationPeriodCount }
                && checkpoint.Event.Strength == new StrengthState(5, 5))
            .ToList();

        // With every centre available, a line's centre takes its faceoffs; a forward stands in for
        // one who is serving a penalty.
        var allCentresAvailable = checkpoints.Where(checkpoint => !checkpoint.Unavailable.Overlaps(centres)).ToList();
        Assert.NotEmpty(allCentresAvailable);
        Assert.All(allCentresAvailable, checkpoint =>
        {
            var faceoff = (FaceoffEvent)checkpoint.Event;
            Assert.Contains(faceoff.WinnerId, centres);
            Assert.Contains(faceoff.LoserId, centres);
        });
    }

    private static int AssistsOn(MatchResult result, PlayerId playerId, GoalSituation situation) =>
        result.Goals.Count(goal => goal.Situation == situation && (goal.PrimaryAssistId == playerId || goal.SecondaryAssistId == playerId));

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
        // costs a little, so goals may trail xG slightly but never by much. Empty-net goals have no xG.
        var goals = Results.Sum(result => result.Goals.Count(goal => !goal.IsEmptyNet));
        var expected = Results.Sum(result => result.Home.Goalie.ExpectedGoalsAgainst + result.Away.Goalie.ExpectedGoalsAgainst);

        Assert.InRange(goals / expected, 0.85, 1.1);
    }
}