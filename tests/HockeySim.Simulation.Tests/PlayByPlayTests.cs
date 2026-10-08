using HockeySim.Domain;
using HockeySim.Simulation.Events;

using Xunit;

namespace HockeySim.Simulation.Tests;

public sealed class PlayByPlayTests
{
    private const int SeedCount = 300;

    private static readonly Match Match = TestMatches.EvenMatch();
    private static readonly List<MatchResult> Results = TestMatches.SimulateMany(Match, SeedCount);

    [Fact]
    public void EventsAreInChronologicalOrderWithinTheirPeriods()
    {
        Assert.All(Results, result =>
        {
            Assert.NotEmpty(result.Events);
            Assert.All(result.Events, matchEvent =>
            {
                var periodLength = matchEvent.Period == MatchResult.OvertimePeriod
                    ? TimeSpan.FromMinutes(5)
                    : TimeSpan.FromMinutes(20);
                Assert.InRange(matchEvent.Period, 1, MatchResult.OvertimePeriod);

                // Only a delayed penalty called as the period ends is recorded at its full length.
                var latest = matchEvent is PenaltyEvent ? periodLength : periodLength - TimeSpan.FromSeconds(1);
                Assert.InRange(matchEvent.TimeInPeriod, TimeSpan.Zero, latest);
                Assert.Equal(0, matchEvent.TimeInPeriod.Ticks % TimeSpan.TicksPerSecond);
            });

            var ordered = result.Events.OrderBy(matchEvent => matchEvent.Period).ThenBy(matchEvent => matchEvent.TimeInPeriod);
            Assert.Equal(ordered, result.Events);
        });
    }

    [Fact]
    public void EveryPeriodOpensAndPlayRestartsAfterEveryGoalWithAFaceoff()
    {
        Assert.All(Results, result =>
        {
            foreach (var period in result.Events.Select(matchEvent => matchEvent.Period).Distinct())
            {
                var first = result.Events.First(matchEvent => matchEvent.Period == period);
                Assert.IsType<FaceoffEvent>(first);
                Assert.Equal(TimeSpan.Zero, first.TimeInPeriod);
            }

            for (var index = 0; index < result.Events.Count - 1; index++)
            {
                if (result.Events[index] is not GoalEvent goal)
                {
                    continue;
                }

                // Penalties still standing from a delayed penalty are assessed before the restart.
                var next = index + 1;
                while (next < result.Events.Count && result.Events[next] is PenaltyEvent penalty && penalty.TimeInPeriod == goal.TimeInPeriod)
                {
                    next++;
                }

                if (next < result.Events.Count && result.Events[next].Period == goal.Period)
                {
                    var restart = Assert.IsType<FaceoffEvent>(result.Events[next]);
                    Assert.Equal(goal.TimeInPeriod, restart.TimeInPeriod);
                }
            }
        });
    }

    [Fact]
    public void EveryEventShowsDistinctDressedSkatersAndTheStartingGoalieOrAnExtraAttackerForEachSide()
    {
        var homeSkaters = TestMatches.DressedSkaterIds(Match.Home);
        var awaySkaters = TestMatches.DressedSkaterIds(Match.Away);

        Assert.All(Results.SelectMany(result => result.Events), matchEvent =>
        {
            var onIce = matchEvent.OnIce;

            Assert.Equal(new StrengthState(onIce.HomeSkaters.Count, onIce.AwaySkaters.Count), matchEvent.Strength);
            Assert.InRange(onIce.HomeSkaters.Count, 3, 6);
            Assert.InRange(onIce.AwaySkaters.Count, 3, 6);
            Assert.Equal(onIce.HomeSkaters.Count, onIce.HomeSkaters.Distinct().Count());
            Assert.Equal(onIce.AwaySkaters.Count, onIce.AwaySkaters.Distinct().Count());
            Assert.All(onIce.HomeSkaters, id => Assert.Contains(id, homeSkaters));
            Assert.All(onIce.AwaySkaters, id => Assert.Contains(id, awaySkaters));
            Assert.Contains(onIce.HomeGoalie, new PlayerId?[] { Match.Home.Lineup.StartingGoalie.Id, null });
            Assert.Contains(onIce.AwayGoalie, new PlayerId?[] { Match.Away.Lineup.StartingGoalie.Id, null });
        });

        // Most of regulation is five-on-five and regular-season overtime three-on-three.
        var regulation = Results.SelectMany(result => result.Events).Where(matchEvent => matchEvent.Period <= MatchResult.RegulationPeriodCount).ToList();
        Assert.True(regulation.Count(matchEvent => matchEvent.Strength == new StrengthState(5, 5)) > regulation.Count * 0.6);
    }

    [Fact]
    public void EveryEventInvolvesPlayersOnTheIceForTheRightTeam()
    {
        IReadOnlyList<PlayerId> SkatersFor(MatchEvent matchEvent, TeamId teamId) =>
            teamId == Match.Home.Id ? matchEvent.OnIce.HomeSkaters : matchEvent.OnIce.AwaySkaters;

        TeamId Opponent(TeamId teamId) => teamId == Match.Home.Id ? Match.Away.Id : Match.Home.Id;

        Assert.All(Results.SelectMany(result => result.Events), matchEvent =>
        {
            switch (matchEvent)
            {
                case FaceoffEvent faceoff:
                    Assert.Contains(faceoff.WinnerId, SkatersFor(faceoff, faceoff.WinnerTeamId));
                    Assert.Contains(faceoff.LoserId, SkatersFor(faceoff, Opponent(faceoff.WinnerTeamId)));
                    break;
                case ShotAttemptEvent attempt:
                    Assert.Contains(attempt.ShooterId, SkatersFor(attempt, attempt.TeamId));
                    if (attempt.BlockerId is { } blocker)
                    {
                        Assert.Contains(blocker, SkatersFor(attempt, Opponent(attempt.TeamId)));
                    }

                    break;
                case GoalEvent goal:
                    var scorers = SkatersFor(goal, goal.TeamId);
                    Assert.Contains(goal.ScorerId, scorers);
                    Assert.All(new[] { goal.PrimaryAssistId, goal.SecondaryAssistId }.OfType<PlayerId>(), assist =>
                    {
                        Assert.Contains(assist, scorers);
                        Assert.NotEqual(goal.ScorerId, assist);
                    });
                    if (goal.PrimaryAssistId is null)
                    {
                        Assert.Null(goal.SecondaryAssistId);
                    }
                    else
                    {
                        Assert.NotEqual(goal.PrimaryAssistId, goal.SecondaryAssistId);
                    }

                    break;
                case HitEvent hit:
                    Assert.Contains(hit.HitterId, SkatersFor(hit, hit.TeamId));
                    Assert.Contains(hit.HitPlayerId, SkatersFor(hit, Opponent(hit.TeamId)));
                    break;
                case TakeawayEvent takeaway:
                    Assert.Contains(takeaway.PlayerId, SkatersFor(takeaway, takeaway.TeamId));
                    break;
                case GiveawayEvent giveaway:
                    Assert.Contains(giveaway.PlayerId, SkatersFor(giveaway, giveaway.TeamId));
                    break;
                case PenaltyEvent penalty:
                    // A delayed penalty is recorded at the whistle, when the offender may be on the bench.
                    var team = penalty.TeamId == Match.Home.Id ? Match.Home : Match.Away;
                    Assert.Contains(penalty.PlayerId, TestMatches.DressedSkaterIds(team));
                    break;
                case InjuryEvent injury:
                    // A fighter is hurt after leaving for the box, so may no longer be on the ice.
                    var injuredTeam = injury.TeamId == Match.Home.Id ? Match.Home : Match.Away;
                    Assert.Contains(injury.PlayerId, injuredTeam.Lineup.DressedPlayers.Select(player => player.Id));
                    break;
                default:
                    Assert.Fail($"Unexpected event {matchEvent.GetType().Name}.");
                    break;
            }
        });
    }

    [Fact]
    public void EveryKindOfEventAndShotOutcomeOccurs()
    {
        var events = Results.SelectMany(result => result.Events).ToList();

        Assert.Contains(events, matchEvent => matchEvent is FaceoffEvent);
        Assert.Contains(events, matchEvent => matchEvent is GoalEvent);
        Assert.Contains(events, matchEvent => matchEvent is HitEvent);
        Assert.Contains(events, matchEvent => matchEvent is TakeawayEvent);
        Assert.Contains(events, matchEvent => matchEvent is GiveawayEvent);
        Assert.Contains(events, matchEvent => matchEvent is PenaltyEvent);
        Assert.Equal(
            Enum.GetValues<ShotOutcome>().ToHashSet(),
            events.OfType<ShotAttemptEvent>().Select(attempt => attempt.Outcome).ToHashSet());
    }

    [Fact]
    public void OnlyUnblockedAttemptsAtAGoalieCarryAnExpectedGoalValue()
    {
        var attempts = Results.SelectMany(result => result.Events).OfType<ShotAttemptEvent>().ToList();

        Assert.All(attempts, attempt =>
        {
            if (attempt.Outcome == ShotOutcome.Blocked)
            {
                Assert.NotNull(attempt.BlockerId);
                Assert.Null(attempt.ExpectedGoals);
            }
            else
            {
                Assert.Null(attempt.BlockerId);
                if (attempt.Context.IsEmptyNet)
                {
                    Assert.Null(attempt.ExpectedGoals);
                }
                else
                {
                    Assert.InRange(attempt.ExpectedGoals!.Value, double.Epsilon, 1.0);
                }
            }

            Assert.Equal(attempt.Outcome == ShotOutcome.Saved, attempt.IsOnGoal);
        });
        Assert.All(Results.SelectMany(result => result.Goals), goal =>
        {
            if (goal.IsEmptyNet)
            {
                Assert.Null(goal.ExpectedGoals);
            }
            else
            {
                Assert.InRange(goal.ExpectedGoals!.Value, double.Epsilon, 1.0);
            }
        });
    }

    [Fact]
    public void ExpectedGoalsDependOnlyOnTheShotContextAndRiseWithDanger()
    {
        var unblocked = Results.SelectMany(result => result.Events)
            .Select(matchEvent => matchEvent switch
            {
                ShotAttemptEvent { ExpectedGoals: { } value } attempt => (attempt.Context, Value: value),
                GoalEvent { ExpectedGoals: { } value } goal => (goal.Context, Value: value),
                _ => ((ShotContext Context, double Value)?)null,
            })
            .OfType<(ShotContext Context, double Value)>()
            .ToList();

        var byContext = unblocked.GroupBy(shot => shot.Context).ToDictionary(group => group.Key, group => group.Select(shot => shot.Value).Distinct().ToList());
        Assert.All(byContext.Values, values => Assert.Single(values));

        double ValueOf(ShotContext context) => byContext[context][0];
        var low = ValueOf(new ShotContext(ShotDanger.Low, IsRebound: false, IsRush: false));
        var medium = ValueOf(new ShotContext(ShotDanger.Medium, IsRebound: false, IsRush: false));
        var high = ValueOf(new ShotContext(ShotDanger.High, IsRebound: false, IsRush: false));
        var rebound = ValueOf(new ShotContext(ShotDanger.High, IsRebound: true, IsRush: false));
        var highRush = ValueOf(new ShotContext(ShotDanger.High, IsRebound: false, IsRush: true));

        Assert.True(low < medium && medium < high && high < rebound, $"{low} {medium} {high} {rebound}");
        Assert.True(highRush > high);
        Assert.All(byContext.Keys.Where(context => context.IsRebound), context => Assert.Equal(ShotDanger.High, context.Danger));
    }

    [Fact]
    public void GoalsAreTheGoalEventsOfThePlayByPlay()
    {
        Assert.All(Results, result => Assert.Equal(result.Events.OfType<GoalEvent>(), result.Goals));
    }
}