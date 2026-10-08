using HockeySim.Domain;
using HockeySim.Simulation.Events;

using Xunit;

namespace HockeySim.Simulation.Tests;

/// <summary>
/// Checks pulling the goalie late in a match against <see cref="GoaliePullRule"/>: who pulls and
/// when, the goalie's return and second pulls, empty-net goals and the goalie's statistics, and
/// extra attackers on top of the strength the penalties allow.
/// </summary>
public sealed class GoaliePullTests
{
    private const int SeedCount = 500;

    private static readonly Match Match = TestMatches.EvenMatch();
    private static readonly List<MatchResult> Results = TestMatches.SimulateMany(Match, SeedCount);

    [Fact]
    public void OnlyATeamTrailingByOneOrTwoLateInTheThirdPeriodPullsItsGoalieAtAFaceoffOrConcedesIntoAnEmptyNet()
    {
        // A delayed penalty pulls a goalie only while play goes on, and the offenders cannot score
        // without touching the puck, so a pulled goalie at a faceoff, or an empty-net goal against,
        // can only be a pull to tie the match.
        var pulledFaceoffs = 0;
        foreach (var result in Results)
        {
            var rule = new GoaliePullRule(result);
            foreach (var matchEvent in result.Events)
            {
                foreach (var team in Teams(result))
                {
                    var pulledAtFaceoff = matchEvent is FaceoffEvent && GoaliePullRule.IsPulled(result, matchEvent, team);
                    var conceded = matchEvent is GoalEvent goal && goal.TeamId != team && goal.IsEmptyNet;
                    if (pulledAtFaceoff || conceded)
                    {
                        pulledFaceoffs += pulledAtFaceoff ? 1 : 0;
                        Assert.True(rule.MayPull(matchEvent, team), $"{matchEvent} at {GoaliePullRule.SecondsLeft(matchEvent)} s left, down {rule.Deficit(matchEvent, team)}.");
                    }
                }
            }
        }

        Assert.True(pulledFaceoffs > 0, "No faceoff was taken with a goalie pulled.");
    }

    [Fact]
    public void TrailingTeamsPullTheirGoalieEarlierTheFurtherBehindTheyAre()
    {
        var oneDown = (Chances: 0, Pulled: 0);
        var twoDown = (Chances: 0, Pulled: 0);
        foreach (var result in Results)
        {
            var rule = new GoaliePullRule(result);
            var third = result.Events.Where(matchEvent => matchEvent.Period == MatchResult.RegulationPeriodCount).ToList();
            foreach (var team in Teams(result))
            {
                bool PulledWhile(Func<MatchEvent, bool> window) =>
                    third.Any(matchEvent => window(matchEvent) && GoaliePullRule.IsPulled(result, matchEvent, team));

                // Trailing by one inside the last two minutes: almost always pulled.
                var oneDownLate = third.Where(matchEvent => rule.Deficit(matchEvent, team) == 1
                    && GoaliePullRule.SecondsLeft(matchEvent) <= GoaliePullRule.OneGoalDownSeconds).ToList();
                if (oneDownLate.Count >= 10)
                {
                    oneDown.Chances++;
                    oneDown.Pulled += PulledWhile(oneDownLate.Contains) ? 1 : 0;
                }

                // Trailing by two between three and a half and two minutes left: already pulled,
                // earlier than a team trailing by one ever is.
                var twoDownEarly = third.Where(matchEvent => rule.Deficit(matchEvent, team) == 2
                    && GoaliePullRule.SecondsLeft(matchEvent) is <= GoaliePullRule.TwoGoalsDownSeconds and > GoaliePullRule.OneGoalDownSeconds).ToList();
                if (twoDownEarly.Count >= 10)
                {
                    twoDown.Chances++;
                    twoDown.Pulled += PulledWhile(twoDownEarly.Contains) ? 1 : 0;
                }
            }
        }

        Assert.True(oneDown.Chances >= 30 && twoDown.Chances >= 15, $"Too few late deficits: {oneDown} {twoDown}.");
        Assert.InRange(oneDown.Pulled / (double)oneDown.Chances, 0.9, 1.0);
        Assert.InRange(twoDown.Pulled / (double)twoDown.Chances, 0.8, 1.0);
    }

    [Fact]
    public void AGoalPutsBothGoaliesBackForTheRestart()
    {
        Assert.All(Results, result =>
        {
            var events = result.Events;
            for (var index = 0; index < events.Count; index++)
            {
                if (events[index] is not GoalEvent goal)
                {
                    continue;
                }

                var restart = events.Skip(index + 1).FirstOrDefault(matchEvent => matchEvent is not PenaltyEvent);
                if (restart is FaceoffEvent { Period: var period } && period == goal.Period)
                {
                    Assert.NotNull(restart.OnIce.HomeGoalie);
                    Assert.NotNull(restart.OnIce.AwayGoalie);
                }
            }
        });
    }

    [Fact]
    public void APulledGoalieMayReturnAtAStoppageOrAfterAGoalAndBePulledAgain()
    {
        var againAfterAStoppage = 0;
        var againAfterAGoal = 0;
        foreach (var result in Results)
        {
            var rule = new GoaliePullRule(result);
            foreach (var team in Teams(result))
            {
                var late = result.Events
                    .Where(matchEvent => rule.MayPull(matchEvent, team))
                    .Select(matchEvent => (Event: matchEvent, Pulled: GoaliePullRule.IsPulled(result, matchEvent, team)))
                    .ToList();
                for (var index = 1; index < late.Count; index++)
                {
                    // Pulled, back in net for a faceoff, then pulled again.
                    if (late[index - 1].Pulled && !late[index].Pulled && late[index].Event is FaceoffEvent
                        && late.Skip(index + 1).Any(entry => entry.Pulled))
                    {
                        var goalBefore = late[index - 1].Event is GoalEvent
                            || result.Events.Any(matchEvent => matchEvent is GoalEvent && matchEvent.Period == late[index].Event.Period
                                && matchEvent.TimeInPeriod == late[index].Event.TimeInPeriod);
                        if (goalBefore)
                        {
                            againAfterAGoal++;
                        }
                        else
                        {
                            againAfterAStoppage++;
                        }
                    }
                }
            }
        }

        Assert.True(againAfterAStoppage > 0, "No goalie came back at a stoppage and was pulled again.");
        Assert.True(againAfterAGoal > 0, "No goalie was pulled again after a goal.");
    }

    [Fact]
    public void EmptyNetGoalsGoIntoAPulledGoaliesNetWithoutExpectedGoals()
    {
        var goals = Results.SelectMany(result => result.Goals.Select(goal => (Result: result, Goal: goal))).ToList();

        Assert.All(goals, entry =>
        {
            var (result, goal) = entry;
            var conceding = goal.TeamId == result.Home.TeamId ? result.Away.TeamId : result.Home.TeamId;
            Assert.Equal(GoaliePullRule.IsPulled(result, goal, conceding), goal.IsEmptyNet);
            Assert.Equal(goal.IsEmptyNet, goal.ExpectedGoals is null);
        });
        Assert.Contains(goals, entry => entry.Goal.IsEmptyNet);
        Assert.Contains(goals, entry => entry.Goal.IsEmptyNet && entry.Goal.Context.Danger == ShotDanger.Low);

        // The extra attacker scores too: goals by a team with its goalie pulled to tie the match.
        Assert.Contains(goals, entry =>
            GoaliePullRule.IsPulled(entry.Result, entry.Goal, entry.Goal.TeamId)
            && new GoaliePullRule(entry.Result).MayPull(entry.Goal, entry.Goal.TeamId));

        // Every attempt at an empty net scores if it reaches it.
        Assert.All(
            Results.SelectMany(result => result.Events).OfType<ShotAttemptEvent>().Where(attempt => attempt.Context.IsEmptyNet),
            attempt =>
            {
                Assert.NotEqual(ShotOutcome.Saved, attempt.Outcome);
                Assert.Null(attempt.ExpectedGoals);
            });
    }

    [Fact]
    public void AGoalieIsNotChargedWithEmptyNetGoalsOrTheTimePulled()
    {
        var pulledGoalies = 0;
        Assert.All(Results, result =>
        {
            foreach (var (team, opponent) in new[] { (result.Home, result.Away), (result.Away, result.Home) })
            {
                var emptyNetGoals = result.Goals.Count(goal => goal.TeamId == opponent.TeamId && goal.IsEmptyNet);
                Assert.Equal(emptyNetGoals, opponent.Skaters.Sum(skater => skater.EmptyNetGoals));
                Assert.Equal(opponent.Shots - emptyNetGoals, team.Goalie.ShotsAgainst);
                Assert.Equal(opponent.Skaters.Sum(skater => skater.Goals) - emptyNetGoals, team.Goalie.GoalsAgainst);

                // A goalie pulled at two consecutive events was off the ice between them: pulls and
                // returns happen only at a faceoff, a goal, a penalty call, or the end of a period.
                var pulledSeconds = 0;
                var events = result.Events;
                for (var index = 1; index < events.Count; index++)
                {
                    if (events[index].Period == events[index - 1].Period
                        && GoaliePullRule.IsPulled(result, events[index - 1], team.TeamId)
                        && GoaliePullRule.IsPulled(result, events[index], team.TeamId))
                    {
                        pulledSeconds += (int)(events[index].TimeInPeriod - events[index - 1].TimeInPeriod).TotalSeconds;
                    }
                }

                Assert.True(team.Goalie.TimeOnIce <= result.PlayingTime - TimeSpan.FromSeconds(pulledSeconds));
                if (pulledSeconds > 0)
                {
                    pulledGoalies++;
                }
            }
        });

        Assert.True(pulledGoalies > 0);
    }

    [Fact]
    public void AnExtraAttackerJoinsWhateverStrengthThePenaltiesAllow()
    {
        var onThePowerPlay = 0;
        var shorthanded = 0;
        foreach (var result in Results)
        {
            var rule = new GoaliePullRule(result);
            var lineups = new Dictionary<TeamId, Lineup> { [Match.Home.Id] = Match.Home.Lineup, [Match.Away.Id] = Match.Away.Lineup };
            foreach (var checkpoint in new ManpowerReplay(result).Checkpoints)
            {
                var matchEvent = checkpoint.Event;
                foreach (var team in Teams(result))
                {
                    if (!GoaliePullRule.IsPulled(result, matchEvent, team) || !rule.MayPull(matchEvent, team))
                    {
                        continue;
                    }

                    var isHome = team == result.Home.TeamId;
                    var (skaters, opponentSkaters) = isHome
                        ? (checkpoint.HomeSkaters, checkpoint.AwaySkaters)
                        : (checkpoint.AwaySkaters, checkpoint.HomeSkaters);
                    var onIce = isHome ? matchEvent.OnIce.HomeSkaters : matchEvent.OnIce.AwaySkaters;

                    // One skater more than the penalties allow, such as six-on-four on a power play.
                    Assert.Equal(skaters + 1, onIce.Count);
                    onThePowerPlay += skaters > opponentSkaters ? 1 : 0;
                    shorthanded += skaters < opponentSkaters ? 1 : 0;

                    // One of the lineup's extra attackers is on whenever either can play.
                    var extraAttackers = lineups[team].ExtraAttackers.Select(player => player.Id).ToList();
                    if (matchEvent is not PenaltyEvent && extraAttackers.Any(id => !checkpoint.Unavailable.Contains(id)))
                    {
                        Assert.Contains(onIce, extraAttackers.Contains);
                    }
                }
            }
        }

        Assert.True(onThePowerPlay > 0, "No goalie was pulled during a power play.");
        Assert.True(shorthanded > 0, "No shorthanded team pulled its goalie.");
    }

    private static TeamId[] Teams(MatchResult result) => [result.Home.TeamId, result.Away.TeamId];
}