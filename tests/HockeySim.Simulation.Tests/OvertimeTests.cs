using HockeySim.Domain;
using HockeySim.Simulation.Events;

using Xunit;

namespace HockeySim.Simulation.Tests;

public sealed class OvertimeTests
{
    private const int SeedCount = 400;

    [Fact]
    public void RegularSeasonOvertimeIsThreeOnThreeWithTheThreeOnThreeUnits()
    {
        var match = TestMatches.EvenMatch();
        var homeUnits = UnitPlayerSets(match.Home);
        var awayUnits = UnitPlayerSets(match.Away);
        var overtimeEvents = ThreeOnThreeOvertimeEvents(TestMatches.SimulateMany(match, SeedCount), homeUnits.Concat(awayUnits))
            .ToList();

        Assert.NotEmpty(overtimeEvents);
        Assert.All(overtimeEvents, matchEvent =>
        {
            Assert.Contains(homeUnits, unit => unit.SetEquals(matchEvent.OnIce.HomeSkaters));
            Assert.Contains(awayUnits, unit => unit.SetEquals(matchEvent.OnIce.AwaySkaters));
        });

        var unitCentres = new[] { match.Home, match.Away }
            .SelectMany(team => team.Lineup.UnitsFor(SpecialSituation.ThreeOnThree))
            .Select(unit => unit.Centre.Id)
            .ToHashSet();
        Assert.All(overtimeEvents.OfType<FaceoffEvent>(), faceoff =>
        {
            Assert.Contains(faceoff.WinnerId, unitCentres);
            Assert.Contains(faceoff.LoserId, unitCentres);
        });
    }

    [Fact]
    public void OnlyTheThreeOnThreeUnitsChosenInTheLineupPlayOvertime()
    {
        var home = TestTeams.Create("Home");
        var fourthLine = home.Lineup.ForwardLines[3];
        var thirdPair = home.Lineup.DefencePairs[2];
        var units = home.Lineup.SpecialSituationUnits
            .Where(unit => unit.Situation != SpecialSituation.ThreeOnThree)
            .Concat(Enumerable.Repeat(
                new SpecialSituationUnit(SpecialSituation.ThreeOnThree, [fourthLine.Centre, fourthLine.LeftWing, thirdPair.LeftDefence]),
                SpecialSituationFormat.For(SpecialSituation.ThreeOnThree).UnitCount));
        home.SetLineup(new Lineup(
            home.Lineup.ForwardLines,
            home.Lineup.DefencePairs,
            home.Lineup.StartingGoalie,
            home.Lineup.BackupGoalie,
            units,
            home.Lineup.ExtraAttackers));
        var expected = new[] { fourthLine.Centre.Id, fourthLine.LeftWing.Id, thirdPair.LeftDefence.Id }.ToHashSet();

        var overtimeEvents = ThreeOnThreeOvertimeEvents(
                TestMatches.SimulateMany(TestTeams.CreateMatch(home, TestTeams.Create("Away")), SeedCount),
                [expected])
            .ToList();

        Assert.NotEmpty(overtimeEvents);
        Assert.All(overtimeEvents, matchEvent => Assert.True(expected.SetEquals(matchEvent.OnIce.HomeSkaters)));
    }

    [Fact]
    public void AShootoutFollowsOnlyAFullScorelessOvertime()
    {
        var shootouts = TestMatches.SimulateMany(TestMatches.EvenMatch(), SeedCount)
            .Where(result => result.Decision == MatchDecision.Shootout)
            .ToList();

        Assert.NotEmpty(shootouts);
        Assert.All(shootouts, result =>
        {
            Assert.DoesNotContain(result.Goals, goal => goal.Period == MatchResult.OvertimePeriod);
            Assert.Contains(result.Events, matchEvent => matchEvent.Period == MatchResult.OvertimePeriod);
            Assert.Equal(TimeSpan.FromMinutes(65), result.PlayingTime);
        });
    }

    [Fact]
    public void PlayoffOvertimeIsUnlimitedFiveASideSuddenDeathWithoutAShootout()
    {
        var match = TestMatches.EvenMatch();
        var results = TestMatches.SimulateMany(match, SeedCount, OvertimeFormat.Playoff);
        var overtimeGames = results.Where(result => result.Decision == MatchDecision.Overtime).ToList();

        Assert.DoesNotContain(results, result => result.Decision == MatchDecision.Shootout);
        Assert.All(results, result => Assert.Null(result.Shootout));
        Assert.NotEmpty(overtimeGames);

        // Some games need a second overtime period, so overtime is not limited to one.
        Assert.Contains(overtimeGames, result => result.Goals[^1].Period > MatchResult.OvertimePeriod);

        Assert.All(overtimeGames, result =>
        {
            var winningGoal = result.Goals[^1];
            Assert.Same(winningGoal, result.Events[^1]);
            Assert.Equal(result.WinnerId, winningGoal.TeamId);
            Assert.Single(result.Goals, goal => goal.Period >= MatchResult.OvertimePeriod);
            Assert.Equal(1, Math.Abs(result.Home.Score - result.Away.Score));

            var periods = winningGoal.Period - MatchResult.RegulationPeriodCount;
            Assert.Equal(
                TimeSpan.FromMinutes(60 + (20 * (periods - 1))) + winningGoal.TimeInPeriod,
                result.PlayingTime);

            // Overtime is five a side, less any penalties being served.
            var replay = new ManpowerReplay(result, OvertimeFormat.Playoff);
            Assert.All(replay.Checkpoints.Where(checkpoint => checkpoint.Event.Period >= MatchResult.OvertimePeriod), checkpoint =>
            {
                Assert.Equal(checkpoint.ExpectedStrength, checkpoint.Event.Strength);
                Assert.InRange(checkpoint.Event.TimeInPeriod, TimeSpan.Zero, TimeSpan.FromMinutes(20));
            });

            foreach (var team in new[] { result.Home, result.Away })
            {
                Assert.Equal(
                    replay.ManpowerSeconds(team.TeamId) + (result.PlayingTime - team.Goalie.TimeOnIce).TotalSeconds,
                    team.Skaters.Sum(skater => skater.TimeOnIce.TotalSeconds));
            }
        });
    }

    [Fact]
    public void RegulationIsPlayedTheSameWayWhateverTheOvertimeFormat()
    {
        var match = TestMatches.EvenMatch();
        var regularSeason = TestMatches.SimulateMany(match, 100);
        var playoff = TestMatches.SimulateMany(match, 100, OvertimeFormat.Playoff);

        foreach (var (season, playoffs) in regularSeason.Zip(playoff))
        {
            Assert.Equal(
                Describe(season.Events.Where(matchEvent => matchEvent.Period <= MatchResult.RegulationPeriodCount)),
                Describe(playoffs.Events.Where(matchEvent => matchEvent.Period <= MatchResult.RegulationPeriodCount)));
            if (season.Decision == MatchDecision.Regulation)
            {
                Assert.Equal(MatchDecision.Regulation, playoffs.Decision);
            }
        }
    }

    [Fact]
    public void BetterShootoutShootersWinMoreShootouts()
    {
        // Shootout shooting uses accuracy and puck control; both teams keep average goalies.
        var shooters = TestTeams.Create("Shooters", (role, rating) =>
            role.Kind != LineupRoleKind.StartingGoalie && rating is Rating.ShotAccuracy or Rating.PuckControl ? 95 : TestTeams.AverageRating);
        var opponent = TestTeams.Create("Opponent", (role, rating) =>
            role.Kind != LineupRoleKind.StartingGoalie && rating is Rating.ShotAccuracy or Rating.PuckControl ? 35 : TestTeams.AverageRating);

        var shootouts = TestMatches.SimulateMany(TestTeams.CreateMatch(shooters, opponent), 1000)
            .Concat(TestMatches.SimulateMany(TestTeams.CreateMatch(opponent, shooters), 1000))
            .Where(result => result.Decision == MatchDecision.Shootout)
            .ToList();

        Assert.True(shootouts.Count >= 40, $"Only {shootouts.Count} shootouts.");
        Assert.True(shootouts.Count(result => result.WinnerId == shooters.Id) > shootouts.Count * 0.6);
    }

    /// <summary>
    /// Overtime events at three-on-three with both goalies in and none of the given units' skaters
    /// in the box, so no substitute has had to replace one.
    /// </summary>
    private static IEnumerable<MatchEvent> ThreeOnThreeOvertimeEvents(IEnumerable<MatchResult> results, IEnumerable<HashSet<PlayerId>> units)
    {
        var unitPlayers = units.SelectMany(unit => unit).ToHashSet();
        return results
            .SelectMany(result => new ManpowerReplay(result).Checkpoints)
            .Where(checkpoint => checkpoint.Event.Period == MatchResult.OvertimePeriod
                && checkpoint.Event.Strength == new StrengthState(3, 3)
                && checkpoint.Event.OnIce.HomeGoalie is not null
                && checkpoint.Event.OnIce.AwayGoalie is not null
                && !checkpoint.Unavailable.Overlaps(unitPlayers))
            .Select(checkpoint => checkpoint.Event);
    }

    private static List<HashSet<PlayerId>> UnitPlayerSets(Team team) =>
        team.Lineup.UnitsFor(SpecialSituation.ThreeOnThree)
            .Select(unit => unit.Players.Select(player => player.Id).ToHashSet())
            .ToList();

    private static string Describe(IEnumerable<MatchEvent> events) => string.Join(Environment.NewLine, events);
}