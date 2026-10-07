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
                Assert.InRange(matchEvent.TimeInPeriod, TimeSpan.Zero, periodLength - TimeSpan.FromSeconds(1));
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
                if (result.Events[index] is GoalEvent goal && result.Events[index + 1].Period == goal.Period)
                {
                    var restart = Assert.IsType<FaceoffEvent>(result.Events[index + 1]);
                    Assert.Equal(goal.TimeInPeriod, restart.TimeInPeriod);
                }
            }
        });
    }

    [Fact]
    public void EveryEventShowsFiveDressedSkatersAndTheStartingGoalieForEachSideInRegulation()
    {
        var homeSkaters = TestMatches.DressedSkaterIds(Match.Home);
        var awaySkaters = TestMatches.DressedSkaterIds(Match.Away);

        Assert.All(Results.SelectMany(result => result.Events), matchEvent =>
        {
            var onIce = matchEvent.OnIce;
            var skaters = matchEvent.Period <= MatchResult.RegulationPeriodCount ? 5 : 3;

            Assert.Equal(new StrengthState(skaters, skaters), matchEvent.Strength);
            Assert.True(matchEvent.Strength.IsEvenStrength);
            Assert.Equal(skaters, onIce.HomeSkaters.Distinct().Count());
            Assert.Equal(skaters, onIce.AwaySkaters.Distinct().Count());
            Assert.All(onIce.HomeSkaters, id => Assert.Contains(id, homeSkaters));
            Assert.All(onIce.AwaySkaters, id => Assert.Contains(id, awaySkaters));
            Assert.Equal(Match.Home.Lineup.StartingGoalie.Id, onIce.HomeGoalie);
            Assert.Equal(Match.Away.Lineup.StartingGoalie.Id, onIce.AwayGoalie);
        });
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
        Assert.Equal(
            Enum.GetValues<ShotOutcome>().ToHashSet(),
            events.OfType<ShotAttemptEvent>().Select(attempt => attempt.Outcome).ToHashSet());
    }

    [Fact]
    public void OnlyUnblockedAttemptsCarryAnExpectedGoalValue()
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
                Assert.InRange(attempt.ExpectedGoals!.Value, double.Epsilon, 1.0);
            }

            Assert.Equal(attempt.Outcome == ShotOutcome.Saved, attempt.IsOnGoal);
        });
        Assert.All(Results.SelectMany(result => result.Goals), goal => Assert.InRange(goal.ExpectedGoals, double.Epsilon, 1.0));
    }

    [Fact]
    public void ExpectedGoalsDependOnlyOnTheShotContextAndRiseWithDanger()
    {
        var unblocked = Results.SelectMany(result => result.Events)
            .Select(matchEvent => matchEvent switch
            {
                ShotAttemptEvent { ExpectedGoals: { } value } attempt => (attempt.Context, Value: value),
                GoalEvent goal => (goal.Context, Value: goal.ExpectedGoals),
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