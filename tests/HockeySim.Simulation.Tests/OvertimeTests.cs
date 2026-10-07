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
        var overtimeEvents = TestMatches.SimulateMany(match, SeedCount)
            .SelectMany(result => result.Events)
            .Where(matchEvent => matchEvent.Period == MatchResult.OvertimePeriod)
            .ToList();
        var homeUnits = UnitPlayerSets(match.Home);
        var awayUnits = UnitPlayerSets(match.Away);

        Assert.NotEmpty(overtimeEvents);
        Assert.All(overtimeEvents, matchEvent =>
        {
            Assert.Equal(new StrengthState(3, 3), matchEvent.Strength);
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

        var overtimeEvents = TestMatches.SimulateMany(TestTeams.CreateMatch(home, TestTeams.Create("Away")), SeedCount)
            .SelectMany(result => result.Events)
            .Where(matchEvent => matchEvent.Period == MatchResult.OvertimePeriod)
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
    public void PlayoffOvertimeIsUnlimitedFiveOnFiveSuddenDeathWithoutAShootout()
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

            Assert.All(result.Events.Where(matchEvent => matchEvent.Period >= MatchResult.OvertimePeriod), matchEvent =>
            {
                Assert.Equal(new StrengthState(5, 5), matchEvent.Strength);
                Assert.InRange(matchEvent.TimeInPeriod, TimeSpan.Zero, TimeSpan.FromMinutes(20) - TimeSpan.FromSeconds(1));
            });

            foreach (var team in new[] { result.Home, result.Away })
            {
                Assert.Equal(5 * result.PlayingTime.TotalSeconds, team.Skaters.Sum(skater => skater.TimeOnIce.TotalSeconds));
                Assert.Equal(result.PlayingTime, team.Goalie.TimeOnIce);
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

    private static List<HashSet<PlayerId>> UnitPlayerSets(Team team) =>
        team.Lineup.UnitsFor(SpecialSituation.ThreeOnThree)
            .Select(unit => unit.Players.Select(player => player.Id).ToHashSet())
            .ToList();

    private static string Describe(IEnumerable<MatchEvent> events) => string.Join(Environment.NewLine, events);
}