using HockeySim.Domain;
using HockeySim.Simulation.Events;

using Xunit;

namespace HockeySim.Simulation.Tests;

/// <summary>
/// Checks the on-ice and team shot totals against a recount of the play-by-play, with each
/// attempt's strength situation taken from <see cref="ManpowerReplay"/>: the manpower the penalties
/// allow, not who is on the ice.
/// </summary>
public sealed class OnIceStatisticsTests
{
    private const int SeedCount = 200;

    private static readonly List<MatchResult> Results = TestMatches.SimulateMany(TestMatches.EvenMatch(), SeedCount);

    [Fact]
    public void OnIceAndTeamTotalsRecountEveryAttemptBySituation()
    {
        Assert.All(Results, result =>
        {
            var expected = Recount(result);
            foreach (var team in new[] { result.Home, result.Away })
            {
                AssertTotals(expected.Get(team.TeamId), team.ShotTotals);
                foreach (var skater in team.Skaters)
                {
                    AssertTotals(expected.Get(skater.PlayerId), skater.OnIce);
                }
            }
        });
    }

    [Fact]
    public void EachTeamsTotalsAreTheOpponentsReversedAndLeaveOutOnlyPenaltyShots()
    {
        Assert.All(Results, result =>
        {
            foreach (var (team, opponent) in new[] { (result.Home, result.Away), (result.Away, result.Home) })
            {
                var reversed = opponent.ShotTotals.Reverse();
                foreach (var situation in Enum.GetValues<StrengthSituation>())
                {
                    Assert.Equal(reversed[situation].AttemptsFor, team.ShotTotals[situation].AttemptsFor);
                    Assert.Equal(reversed[situation].GoalsAgainst, team.ShotTotals[situation].GoalsAgainst);
                    Assert.Equal(reversed[situation].ExpectedGoalsFor, team.ShotTotals[situation].ExpectedGoalsFor, precision: 9);
                }

                var penaltyShots = result.Events.OfType<ShotAttemptEvent>().Count(attempt => attempt.TeamId == team.TeamId && attempt.Context.IsPenaltyShot)
                    + result.Goals.Count(goal => goal.TeamId == team.TeamId && goal.Situation == GoalSituation.PenaltyShot);
                var all = team.ShotTotals.All;
                Assert.Equal(team.Skaters.Sum(skater => skater.ShotAttempts) - penaltyShots, all.AttemptsFor);
                Assert.Equal(team.Score - (result.Shootout?.WinnerId == team.TeamId ? 1 : 0)
                    - result.Goals.Count(goal => goal.TeamId == team.TeamId && goal.Situation == GoalSituation.PenaltyShot), all.GoalsFor);
            }
        });
    }

    [Fact]
    public void SpecialTeamsGoalsAreCountedInTheMatchingSituation()
    {
        Assert.All(Results, result =>
        {
            foreach (var team in new[] { result.Home, result.Away })
            {
                Assert.Equal(team.PowerPlayGoals, team.ShotTotals.PowerPlay.GoalsFor);
                Assert.Equal(team.Skaters.Sum(skater => skater.ShorthandedGoals), team.ShotTotals.PenaltyKill.GoalsFor);
            }
        });
    }

    [Fact]
    public void FiveOnFiveAttemptsHaveFiveSkatersASideOnTheIce()
    {
        Assert.All(Results, result =>
        {
            foreach (var team in new[] { result.Home, result.Away })
            {
                var fiveOnFive = team.ShotTotals.FiveOnFive;
                Assert.Equal(5 * fiveOnFive.AttemptsFor, team.Skaters.Sum(skater => skater.OnIce.FiveOnFive.AttemptsFor));
                Assert.Equal(5 * fiveOnFive.AttemptsAgainst, team.Skaters.Sum(skater => skater.OnIce.FiveOnFive.AttemptsAgainst));
            }
        });

        // Even matches spend most of their time at five-on-five.
        var shares = Results.Select(result => result.Home.ShotTotals.FiveOnFive.AttemptsFor / (double)result.Home.ShotTotals.All.AttemptsFor);
        Assert.InRange(shares.Average(), 0.6, 0.95);
    }

    private static void AssertTotals(Tally expected, SituationalShotTotals actual)
    {
        foreach (var situation in Enum.GetValues<StrengthSituation>())
        {
            var (want, got) = (expected.Totals[(int)situation], actual[situation]);
            Assert.Equal(
                (want.AttemptsFor, want.AttemptsAgainst, want.UnblockedFor, want.UnblockedAgainst, want.ShotsFor, want.ShotsAgainst, want.GoalsFor, want.GoalsAgainst),
                (got.AttemptsFor, got.AttemptsAgainst, got.UnblockedAttemptsFor, got.UnblockedAttemptsAgainst, got.ShotsFor, got.ShotsAgainst, got.GoalsFor, got.GoalsAgainst));
            Assert.Equal(want.ExpectedGoalsFor, got.ExpectedGoalsFor, precision: 9);
            Assert.Equal(want.ExpectedGoalsAgainst, got.ExpectedGoalsAgainst, precision: 9);
        }
    }

    /// <summary>
    /// Credits every attempt other than a penalty shot to the shooting team and its skaters on the
    /// ice, and against the defending team and its skaters, each in its own situation.
    /// </summary>
    private static Tallies Recount(MatchResult result)
    {
        var tallies = new Tallies();
        var homeId = result.Home.TeamId;
        var awayId = result.Away.TeamId;
        foreach (var checkpoint in new ManpowerReplay(result).Checkpoints)
        {
            var (teamId, isBlocked, isOnGoal, isGoal, expectedGoals, isPenaltyShot) = checkpoint.Event switch
            {
                ShotAttemptEvent attempt => (attempt.TeamId, attempt.BlockerId is not null, attempt.IsOnGoal, false, attempt.ExpectedGoals ?? 0, attempt.Context.IsPenaltyShot),
                GoalEvent goal => (goal.TeamId, false, true, true, goal.ExpectedGoals ?? 0, goal.Situation == GoalSituation.PenaltyShot),
                _ => (default(TeamId), false, false, false, 0.0, true),
            };
            if (isPenaltyShot)
            {
                continue;
            }

            var onIce = checkpoint.Event.OnIce;
            var shootingIsHome = teamId == homeId;
            var (shooting, defending) = shootingIsHome ? (checkpoint.HomeSkaters, checkpoint.AwaySkaters) : (checkpoint.AwaySkaters, checkpoint.HomeSkaters);
            var bothInNet = onIce.HomeGoalie is not null && onIce.AwayGoalie is not null;
            var situation = shooting > defending ? StrengthSituation.PowerPlay
                : shooting < defending ? StrengthSituation.PenaltyKill
                : shooting == 5 && bothInNet ? StrengthSituation.FiveOnFive
                : StrengthSituation.Other;
            var opposite = situation switch
            {
                StrengthSituation.PowerPlay => StrengthSituation.PenaltyKill,
                StrengthSituation.PenaltyKill => StrengthSituation.PowerPlay,
                _ => situation,
            };

            var (shooters, defenders) = shootingIsHome ? (onIce.HomeSkaters, onIce.AwaySkaters) : (onIce.AwaySkaters, onIce.HomeSkaters);
            foreach (var key in shooters.Select(id => (object)id).Append(teamId))
            {
                tallies.Get(key).Totals[(int)situation].AddFor(isBlocked, isOnGoal, isGoal, expectedGoals);
            }

            foreach (var key in defenders.Select(id => (object)id).Append(shootingIsHome ? awayId : homeId))
            {
                tallies.Get(key).Totals[(int)opposite].AddAgainst(isBlocked, isOnGoal, isGoal, expectedGoals);
            }
        }

        return tallies;
    }

    private sealed class Tallies
    {
        private readonly Dictionary<object, Tally> _tallies = [];

        public Tally Get(object key)
        {
            if (!_tallies.TryGetValue(key, out var tally))
            {
                tally = new Tally();
                _tallies[key] = tally;
            }

            return tally;
        }
    }

    private sealed class Tally
    {
        public Counts[] Totals { get; } = [new(), new(), new(), new()];
    }

    private sealed class Counts
    {
        public int AttemptsFor { get; private set; }

        public int AttemptsAgainst { get; private set; }

        public int UnblockedFor { get; private set; }

        public int UnblockedAgainst { get; private set; }

        public int ShotsFor { get; private set; }

        public int ShotsAgainst { get; private set; }

        public int GoalsFor { get; private set; }

        public int GoalsAgainst { get; private set; }

        public double ExpectedGoalsFor { get; private set; }

        public double ExpectedGoalsAgainst { get; private set; }

        public void AddFor(bool isBlocked, bool isOnGoal, bool isGoal, double expectedGoals)
        {
            AttemptsFor++;
            UnblockedFor += isBlocked ? 0 : 1;
            ShotsFor += isOnGoal ? 1 : 0;
            GoalsFor += isGoal ? 1 : 0;
            ExpectedGoalsFor += expectedGoals;
        }

        public void AddAgainst(bool isBlocked, bool isOnGoal, bool isGoal, double expectedGoals)
        {
            AttemptsAgainst++;
            UnblockedAgainst += isBlocked ? 0 : 1;
            ShotsAgainst += isOnGoal ? 1 : 0;
            GoalsAgainst += isGoal ? 1 : 0;
            ExpectedGoalsAgainst += expectedGoals;
        }
    }
}