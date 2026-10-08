using HockeySim.Domain;
using HockeySim.Simulation.Events;

using Xunit;

namespace HockeySim.Simulation.Tests;

/// <summary>
/// Checks penalties, fighting, and special teams against <see cref="ManpowerReplay"/>, an
/// independent statement of the manpower rules, and checks that every rule's situation occurs.
/// </summary>
public sealed class PenaltyTests
{
    private const int SeedCount = 300;
    private const int Undisciplined = 20;

    private static readonly Match EvenMatch = TestMatches.EvenMatch();
    private static readonly Match RowdyMatch = TestTeams.CreateMatch(Rowdy("Home"), Rowdy("Away"));
    private static readonly List<MatchResult> EvenResults = TestMatches.SimulateMany(EvenMatch, SeedCount);
    private static readonly List<MatchResult> RowdyResults = TestMatches.SimulateMany(RowdyMatch, SeedCount);
    private static readonly List<MatchResult> RowdyPlayoffResults = TestMatches.SimulateMany(RowdyMatch, 100, OvertimeFormat.Playoff);

    private static readonly List<(MatchResult Result, ManpowerReplay Replay)> Replays = EvenResults
        .Concat(RowdyResults)
        .Select(result => (result, new ManpowerReplay(result)))
        .Concat(RowdyPlayoffResults.Select(result => (result, new ManpowerReplay(result, OvertimeFormat.Playoff))))
        .ToList();

    private static IEnumerable<ManpowerReplay.Checkpoint> Checkpoints =>
        Replays.SelectMany(replay => replay.Replay.Checkpoints);

    [Fact]
    public void EveryEventHasTheStrengthThePenaltiesBeingServedCallFor()
    {
        Assert.All(Checkpoints, checkpoint =>
        {
            Assert.Equal(checkpoint.ExpectedStrength, checkpoint.Event.Strength);
            Assert.InRange(checkpoint.HomeSkaters, 3, 5);
            Assert.InRange(checkpoint.AwaySkaters, 3, 5);
        });
    }

    [Fact]
    public void SkatersServingPenaltiesOrEjectedStayOffTheIce()
    {
        Assert.All(Checkpoints.Where(checkpoint => checkpoint.Event is not PenaltyEvent), checkpoint =>
        {
            var onIce = checkpoint.Event.OnIce.HomeSkaters.Concat(checkpoint.Event.OnIce.AwaySkaters);
            Assert.DoesNotContain(onIce, checkpoint.Unavailable.Contains);
        });
    }

    [Fact]
    public void PowerPlayOpportunitiesCountEachPenaltyThatGivesTheOtherTeamTheAdvantage()
    {
        Assert.All(Replays, replay =>
        {
            Assert.Equal(replay.Replay.PowerPlayOpportunities(replay.Result.Home.TeamId), replay.Result.Home.PowerPlayOpportunities);
            Assert.Equal(replay.Replay.PowerPlayOpportunities(replay.Result.Away.TeamId), replay.Result.Away.PowerPlayOpportunities);
        });
        Assert.Contains(EvenResults, result => result.Home.PowerPlayOpportunities > 0);
    }

    [Fact]
    public void GoalsAreClassifiedByThePenaltiesBeingServedNotByWhoIsOnTheIce()
    {
        var goals = Replays
            .SelectMany(replay => replay.Replay.Checkpoints
                .Where(checkpoint => checkpoint.Event is GoalEvent)
                .Select(checkpoint => (checkpoint, HomeTeamId: replay.Result.Home.TeamId)))
            .ToList();

        Assert.All(goals, entry =>
        {
            var (checkpoint, homeTeamId) = entry;
            var goal = (GoalEvent)checkpoint.Event;
            var (scoring, conceding) = goal.TeamId == homeTeamId
                ? (checkpoint.HomeSkaters, checkpoint.AwaySkaters)
                : (checkpoint.AwaySkaters, checkpoint.HomeSkaters);
            var expected = goal.Context.IsPenaltyShot ? GoalSituation.PenaltyShot
                : scoring > conceding ? GoalSituation.PowerPlay
                : scoring < conceding ? GoalSituation.Shorthanded
                : GoalSituation.EvenStrength;
            Assert.Equal(expected, goal.Situation);
        });
        Assert.All(Enum.GetValues<GoalSituation>(), situation =>
            Assert.Contains(goals, entry => ((GoalEvent)entry.checkpoint.Event).Situation == situation));

        // An extra attacker during a delayed penalty does not make a power play.
        Assert.Contains(goals, entry =>
            entry.checkpoint.Event is GoalEvent { Situation: GoalSituation.EvenStrength } && !entry.checkpoint.Event.Strength.IsEvenStrength);
    }

    [Fact]
    public void APowerPlayGoalEndsAMinorButNotAMajor()
    {
        var endedMinor = 0;
        var majorContinued = 0;
        foreach (var (result, replay) in Replays)
        {
            var checkpoints = replay.Checkpoints;
            for (var index = 0; index < checkpoints.Count - 1; index++)
            {
                if (checkpoints[index].Event is not GoalEvent { Situation: GoalSituation.PowerPlay } goal)
                {
                    continue;
                }

                var conceding = goal.TeamId == result.Home.TeamId ? result.Away.TeamId : result.Home.TeamId;
                var before = checkpoints[index].ManpowerPenaltiesFor(conceding);
                var after = checkpoints[index + 1].ManpowerPenaltiesFor(conceding);
                if (after < before)
                {
                    endedMinor++;
                }
                else if (result.Events.Take(index).OfType<PenaltyEvent>().Any(penalty => penalty.TeamId == conceding && penalty.Kind == PenaltyKind.Major))
                {
                    majorContinued++;
                }
            }
        }

        Assert.True(endedMinor > 0, "No power-play goal ended a minor.");
        Assert.True(majorContinued > 0, "No power-play goal was scored against a major.");
    }

    [Fact]
    public void ATeamServesAtMostTwoPenaltiesAtOnceAndFurtherOnesWait()
    {
        var checkpoints = Checkpoints.ToList();

        // A third penalty leaves the team a skater short only once one of the first two ends.
        Assert.Contains(checkpoints, checkpoint => checkpoint.ManpowerPenalties.Values.Any(count => count >= 3));
        Assert.Contains(checkpoints, checkpoint => checkpoint.Event.Period <= MatchResult.RegulationPeriodCount
            && Math.Min(checkpoint.HomeSkaters, checkpoint.AwaySkaters) == 3
            && Math.Max(checkpoint.HomeSkaters, checkpoint.AwaySkaters) == 5);
        Assert.DoesNotContain(checkpoints, checkpoint => Math.Min(checkpoint.HomeSkaters, checkpoint.AwaySkaters) < 3);
    }

    [Fact]
    public void CoincidentalMinorsAtFullStrengthPlayFourOnFourAndOtherCoincidentalPenaltiesLeaveTheStrength()
    {
        var fourOnFour = 0;
        var unchanged = 0;
        foreach (var (_, replay) in Replays)
        {
            foreach (var (penalties, before, after) in Batches(replay))
            {
                if (penalties.Select(penalty => penalty.TeamId).Distinct().Count() < 2)
                {
                    continue;
                }

                if (after is null)
                {
                    continue;
                }

                if (before.HomeSkaters == 5 && before.AwaySkaters == 5 && after.HomeSkaters == 4 && after.AwaySkaters == 4)
                {
                    fourOnFour++;
                }
                else
                {
                    Assert.Equal((before.HomeSkaters, before.AwaySkaters), (after.HomeSkaters, after.AwaySkaters));
                    unchanged++;
                }
            }
        }

        Assert.True(fourOnFour > 0, "No coincidental minors played four-on-four.");
        Assert.True(unchanged > 0, "No coincidental penalties left the strength unchanged.");
    }

    [Fact]
    public void FightsGiveBothFightersFiveMinuteMajorsInRegulationOnly()
    {
        var fights = Replays.SelectMany(replay => Batches(replay.Replay))
            .Where(batch => batch.Penalties.Any(penalty => penalty.Infraction == Infraction.Fighting))
            .ToList();

        Assert.NotEmpty(fights);
        Assert.All(fights, fight =>
        {
            var majors = fight.Penalties.Where(penalty => penalty.Infraction == Infraction.Fighting).ToList();
            Assert.Equal(2, majors.Count);
            Assert.All(majors, major =>
            {
                Assert.Equal(PenaltyKind.Major, major.Kind);
                Assert.Equal(5, major.Minutes);
            });
            Assert.NotEqual(majors[0].TeamId, majors[1].TeamId);
            Assert.InRange(majors[0].Period, 1, MatchResult.RegulationPeriodCount);
            Assert.Equal((fight.Before.HomeSkaters, fight.Before.AwaySkaters), (fight.After!.HomeSkaters, fight.After.AwaySkaters));
        });
    }

    [Fact]
    public void TougherSkatersFightMore()
    {
        Team WithToughness(string name, int toughness) =>
            TestTeams.Create(name, (_, rating) => rating == Rating.Toughness ? toughness : TestTeams.AverageRating);

        double FightsPerMatch(int toughness) => TestMatches
            .SimulateMany(TestTeams.CreateMatch(WithToughness("Home", toughness), WithToughness("Away", toughness)), SeedCount)
            .Average(result => result.Events.OfType<PenaltyEvent>().Count(penalty => penalty.Infraction == Infraction.Fighting) / 2.0);

        Assert.True(FightsPerMatch(90) > 2 * FightsPerMatch(40));
    }

    [Fact]
    public void UndisciplinedTeamsTakeMorePenalties()
    {
        var undisciplined = TestTeams.Create("Undisciplined", (_, rating) => rating == Rating.Discipline ? 30 : TestTeams.AverageRating);
        var disciplined = TestTeams.Create("Disciplined", (_, rating) => rating == Rating.Discipline ? 90 : TestTeams.AverageRating);
        var penalties = TestMatches.SimulateMany(TestTeams.CreateMatch(undisciplined, disciplined), SeedCount)
            .SelectMany(result => result.Events.OfType<PenaltyEvent>())
            .ToList();

        Assert.True(
            penalties.Count(penalty => penalty.TeamId == undisciplined.Id) > 2 * penalties.Count(penalty => penalty.TeamId == disciplined.Id));
    }

    [Fact]
    public void EveryKindOfPenaltyOccursWithItsMinutes()
    {
        var penalties = RowdyResults.SelectMany(result => result.Events).OfType<PenaltyEvent>().ToList();

        Assert.Equal(Enum.GetValues<PenaltyKind>().ToHashSet(), penalties.Select(penalty => penalty.Kind).ToHashSet());
        Assert.Equal(Enum.GetValues<Infraction>().ToHashSet(), penalties.Select(penalty => penalty.Infraction).ToHashSet());
        Assert.All(penalties, penalty => Assert.Equal(
            penalty.Kind switch
            {
                PenaltyKind.Minor => 2,
                PenaltyKind.DoubleMinor => 4,
                PenaltyKind.Major => 5,
                PenaltyKind.PenaltyShot => 0,
                _ => 10,
            },
            penalty.Minutes));

        // A major for anything but fighting carries a game misconduct, and an ejected skater does
        // not play again.
        Assert.All(RowdyResults, result =>
        {
            var events = result.Events;
            for (var index = 0; index < events.Count; index++)
            {
                if (events[index] is PenaltyEvent { Kind: PenaltyKind.Major } major && major.Infraction != Infraction.Fighting)
                {
                    Assert.Contains(events.Skip(index).OfType<PenaltyEvent>(), penalty =>
                        penalty.PlayerId == major.PlayerId && penalty.Kind == PenaltyKind.GameMisconduct && penalty.TimeInPeriod == major.TimeInPeriod);
                }

                if (events[index] is PenaltyEvent { Kind: PenaltyKind.GameMisconduct } ejection)
                {
                    Assert.DoesNotContain(events.Skip(index + 1).Where(matchEvent => matchEvent is not PenaltyEvent), matchEvent =>
                        matchEvent.OnIce.HomeSkaters.Contains(ejection.PlayerId) || matchEvent.OnIce.AwaySkaters.Contains(ejection.PlayerId));
                }
            }
        });
    }

    [Fact]
    public void ADelayedPenaltyLetsTheTeamWithThePuckPullItsGoalieUntilTheOffendersTouchThePuck()
    {
        var delays = 0;
        var goalsDuringDelay = 0;
        foreach (var result in EvenResults.Concat(RowdyResults))
        {
            var events = result.Events;
            for (var index = 0; index < events.Count; index++)
            {
                var pulledTeam = PulledTeam(result, events[index]);
                if (pulledTeam is null || (index > 0 && PulledTeam(result, events[index - 1]) == pulledTeam))
                {
                    continue;
                }

                delays++;
                var offenders = pulledTeam == result.Home.TeamId ? result.Away.TeamId : result.Home.TeamId;
                var end = index;
                while (end < events.Count && PulledTeam(result, events[end]) == pulledTeam)
                {
                    // Only the team with the puck plays while the penalty is delayed.
                    var matchEvent = events[end];
                    Assert.True(matchEvent is not FaceoffEvent);
                    Assert.False(matchEvent is ShotAttemptEvent attempt && attempt.TeamId == offenders);
                    Assert.False(matchEvent is GoalEvent offendersGoal && offendersGoal.TeamId == offenders);
                    Assert.False(matchEvent.OnIce.HomeGoalie is null && matchEvent.OnIce.AwayGoalie is null);
                    end++;
                }

                // The delay ends with the penalty called on the offenders, or a goal against them,
                // which wipes out a minor; only the remaining half of a high-sticking double minor
                // is then assessed as a minor.
                var last = events[end - 1];
                if (last is GoalEvent goal && goal.TeamId == pulledTeam)
                {
                    goalsDuringDelay++;
                    Assert.DoesNotContain(
                        events.Skip(end).TakeWhile(matchEvent => matchEvent is PenaltyEvent && matchEvent.TimeInPeriod == goal.TimeInPeriod),
                        matchEvent => matchEvent is PenaltyEvent { Kind: PenaltyKind.Minor } penalty
                            && penalty.TeamId == offenders
                            && penalty.Infraction != Infraction.HighSticking);
                }
                else if (end < events.Count)
                {
                    var called = Assert.IsType<PenaltyEvent>(events[end]);
                    Assert.Equal(offenders, called.TeamId);
                }
            }
        }

        Assert.True(delays > 0, "No penalty was delayed.");
        Assert.True(goalsDuringDelay > 0, "No goal was scored during a delayed penalty.");
    }

    [Fact]
    public void SomeFoulsOnARushAwardAnUnassistedPenaltyShot()
    {
        var penaltyShots = 0;
        foreach (var result in EvenResults.Concat(RowdyResults))
        {
            var events = result.Events;
            for (var index = 0; index < events.Count; index++)
            {
                if (events[index] is not PenaltyEvent { Kind: PenaltyKind.PenaltyShot } foul)
                {
                    continue;
                }

                penaltyShots++;
                var shot = events[index + 1];
                Assert.Equal(foul.TimeInPeriod, shot.TimeInPeriod);
                switch (shot)
                {
                    case GoalEvent goal:
                        Assert.NotEqual(foul.TeamId, goal.TeamId);
                        Assert.True(goal.Context.IsPenaltyShot);
                        Assert.Equal(GoalSituation.PenaltyShot, goal.Situation);
                        Assert.Null(goal.PrimaryAssistId);
                        break;
                    case ShotAttemptEvent attempt:
                        Assert.NotEqual(foul.TeamId, attempt.TeamId);
                        Assert.True(attempt.Context.IsPenaltyShot);
                        Assert.Equal(ShotOutcome.Saved, attempt.Outcome);
                        break;
                    default:
                        Assert.Fail($"A penalty shot was followed by {shot.GetType().Name}.");
                        break;
                }
            }
        }

        Assert.True(penaltyShots > 0, "No penalty shot was awarded.");
    }

    [Fact]
    public void APenaltyInThreeOnThreeOvertimeGivesTheOtherTeamAnExtraSkater()
    {
        var overtime = EvenResults.Concat(RowdyResults)
            .SelectMany(result => new ManpowerReplay(result).Checkpoints)
            .Where(checkpoint => checkpoint.Event.Period == MatchResult.OvertimePeriod && checkpoint.Event.OnIce.HomeGoalie is not null && checkpoint.Event.OnIce.AwayGoalie is not null)
            .ToList();

        Assert.Contains(overtime, checkpoint => (checkpoint.HomeSkaters, checkpoint.AwaySkaters) is (4, 3) or (3, 4));
        Assert.All(overtime, checkpoint => Assert.Equal(3, Math.Min(checkpoint.HomeSkaters, checkpoint.AwaySkaters)));
    }

    [Fact]
    public void EachStrengthStatePlaysTheLineupsMatchingGroups()
    {
        var checkedSpecial = 0;
        foreach (var (result, replay) in Replays)
        {
            var match = result.Home.TeamId == RowdyMatch.Home.Id ? RowdyMatch : EvenMatch;
            foreach (var checkpoint in replay.Checkpoints.Where(checkpoint => checkpoint.Event is not PenaltyEvent))
            {
                foreach (var (team, skaters, opponentSkaters, onIce, goalie) in new[]
                {
                    (match.Home, checkpoint.HomeSkaters, checkpoint.AwaySkaters, checkpoint.Event.OnIce.HomeSkaters, checkpoint.Event.OnIce.HomeGoalie),
                    (match.Away, checkpoint.AwaySkaters, checkpoint.HomeSkaters, checkpoint.Event.OnIce.AwaySkaters, checkpoint.Event.OnIce.AwayGoalie),
                })
                {
                    var groups = Groups(team, skaters, opponentSkaters);

                    // A substitute replaces anyone in the box, and an extra attacker joins a pulled goalie's team.
                    if (goalie is null || groups.Any(group => group.Overlaps(checkpoint.Unavailable)))
                    {
                        continue;
                    }

                    if ((skaters, opponentSkaters) == (5, 5))
                    {
                        Assert.Contains(groups, line => line.IsSubsetOf(onIce) && line.Count == 3);
                        Assert.Contains(groups, pair => pair.IsSubsetOf(onIce) && pair.Count == 2);
                    }
                    else
                    {
                        Assert.Contains(groups, unit => unit.SetEquals(onIce));
                        checkedSpecial++;
                    }
                }
            }
        }

        Assert.True(checkedSpecial > 0);
    }

    [Fact]
    public void PenaltiesAreRecordedWithDressedSkatersOfThePenalizedTeam()
    {
        foreach (var (result, match) in EvenResults.Select(result => (result, EvenMatch)).Concat(RowdyResults.Select(result => (result, RowdyMatch))))
        {
            Assert.All(result.Events.OfType<PenaltyEvent>(), penalty =>
            {
                var team = penalty.TeamId == match.Home.Id ? match.Home : match.Away;
                Assert.Contains(penalty.PlayerId, TestMatches.DressedSkaterIds(team));
                Assert.InRange(penalty.TimeInPeriod, TimeSpan.Zero, TimeSpan.FromMinutes(penalty.Period == MatchResult.OvertimePeriod ? 5 : 20));
            });
        }
    }

    [Fact]
    public void PenaltyRatesArePlausibleForEvenlyMatchedTeams()
    {
        // Wide bands that catch broken tuning; calibration to NHL averages is #53.
        var matches = (double)EvenResults.Count;
        var penalties = EvenResults.SelectMany(result => result.Events).OfType<PenaltyEvent>().ToList();
        var opportunities = EvenResults.Sum(result => result.Home.PowerPlayOpportunities + result.Away.PowerPlayOpportunities);
        var powerPlayGoals = EvenResults.SelectMany(result => result.Goals).Count(goal => goal.Situation == GoalSituation.PowerPlay);

        Assert.InRange(penalties.Sum(penalty => penalty.Minutes) / matches / 2, 4, 14);
        Assert.InRange(opportunities / matches / 2, 1.5, 4.5);
        Assert.InRange(powerPlayGoals / (double)opportunities, 0.12, 0.30);
        Assert.InRange(penalties.Count(penalty => penalty.Infraction == Infraction.Fighting) / matches / 2, 0.05, 0.5);
    }

    private static Team Rowdy(string name) => TestTeams.Create(name, (_, rating) => rating switch
    {
        Rating.Discipline => Undisciplined,
        Rating.Toughness => 85,
        _ => TestTeams.AverageRating,
    });

    private static TeamId? PulledTeam(MatchResult result, MatchEvent matchEvent) =>
        matchEvent.OnIce.HomeGoalie is null ? result.Home.TeamId
        : matchEvent.OnIce.AwayGoalie is null ? result.Away.TeamId
        : null;

    /// <summary>
    /// The groups a team plays in a strength state: its forward lines and defence pairs at
    /// five-on-five, otherwise its units for the special situation.
    /// </summary>
    private static List<HashSet<PlayerId>> Groups(Team team, int skaters, int opponentSkaters)
    {
        var lineup = team.Lineup;
        SpecialSituation? situation = (skaters, opponentSkaters) switch
        {
            (5, 5) => null,
            (5, 4) => SpecialSituation.PowerPlay5On4,
            (5, 3) => SpecialSituation.PowerPlay5On3,
            (4, 3) => SpecialSituation.PowerPlay4On3,
            (4, 5) => SpecialSituation.PenaltyKill4On5,
            (3, 5) => SpecialSituation.PenaltyKill3On5,
            (3, 4) => SpecialSituation.PenaltyKill3On4,
            (4, 4) => SpecialSituation.FourOnFour,
            _ => SpecialSituation.ThreeOnThree,
        };

        return situation is { } special
            ? lineup.UnitsFor(special).Select(unit => unit.Players.Select(player => player.Id).ToHashSet()).ToList()
            : lineup.ForwardLines.Select(line => line.Players.Select(player => player.Id).ToHashSet())
                .Concat(lineup.DefencePairs.Select(pair => pair.Players.Select(player => player.Id).ToHashSet()))
                .ToList();
    }

    /// <summary>Penalties assessed together, with the rules' state before them and at the next event.</summary>
    private static IEnumerable<(List<PenaltyEvent> Penalties, ManpowerReplay.Checkpoint Before, ManpowerReplay.Checkpoint? After)> Batches(ManpowerReplay replay)
    {
        var checkpoints = replay.Checkpoints;
        for (var index = 0; index < checkpoints.Count; index++)
        {
            if (checkpoints[index].Event is not PenaltyEvent first)
            {
                continue;
            }

            var start = index;
            var batch = new List<PenaltyEvent>();
            while (index < checkpoints.Count && checkpoints[index].Event is PenaltyEvent penalty
                && ManpowerReplay.GameSeconds(penalty) == ManpowerReplay.GameSeconds(first))
            {
                batch.Add(penalty);
                index++;
            }

            yield return (batch, checkpoints[start], index < checkpoints.Count ? checkpoints[index] : null);
            index--;
        }
    }
}