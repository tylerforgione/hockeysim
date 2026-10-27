using System.Globalization;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.TeamStatistics;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class AdvancedStatisticsDisplayTests
{
    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(4, "4th")]
    [InlineData(11, "11th")]
    [InlineData(12, "12th")]
    [InlineData(13, "13th")]
    [InlineData(21, "21st")]
    [InlineData(22, "22nd")]
    [InlineData(23, "23rd")]
    [InlineData(101, "101st")]
    [InlineData(111, "111th")]
    [InlineData(112, "112th")]
    [InlineData(113, "113th")]
    public void OrdinalsUseTheRightSuffixIncludingTheTeens(int number, string expected)
    {
        Assert.Equal(expected, MatchDisplay.Ordinal(number));
    }

    [Theory]
    [InlineData(null, "—")]
    [InlineData(0.0, "0.0")]
    [InlineData(0.5234, "52.3")]
    [InlineData(1.0, "100.0")]
    public void PercentagesShowOneDecimalOrADashWhileUndefined(double? share, string expected)
    {
        Assert.Equal(expected, MatchDisplay.Percentage(share));
    }

    [Theory]
    [InlineData(null, "—")]
    [InlineData(0.0, "0.00")]
    [InlineData(2.714, "2.71")]
    public void GoalsAgainstAveragesShowTwoDecimalsOrADashWhileUndefined(double? average, string expected)
    {
        Assert.Equal(expected, MatchDisplay.GoalsAgainstAverage(average));
    }

    [Theory]
    [InlineData(1.234, "+1.23")]
    [InlineData(-1.1, "-1.10")]
    [InlineData(0.0, "0.00")]
    [InlineData(-0.001, "0.00")]
    public void GoalsSavedAboveExpectedShowsItsSign(double value, string expected)
    {
        Assert.Equal(expected, MatchDisplay.GoalsSavedAboveExpected(value));
    }

    [Theory]
    [InlineData(1, 0, "1st 0:00")]
    [InlineData(2, 845, "2nd 14:05")]
    [InlineData(3, 1200, "3rd 20:00")]
    [InlineData(4, 192, "OT 3:12")]
    [InlineData(5, 61, "2OT 1:01")]
    public void SummaryTimesNameThePeriod(int period, int seconds, string expected)
    {
        Assert.Equal(expected, MatchDisplay.PeriodTime(period, TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(GoalSituation.EvenStrength, false, "")]
    [InlineData(GoalSituation.PowerPlay, false, "PP")]
    [InlineData(GoalSituation.Shorthanded, false, "SH")]
    [InlineData(GoalSituation.PenaltyShot, false, "PS")]
    [InlineData(GoalSituation.EvenStrength, true, "EN")]
    [InlineData(GoalSituation.PowerPlay, true, "PP EN")]
    public void GoalsAreMarkedWithTheirSituation(GoalSituation situation, bool isEmptyNet, string expected)
    {
        Assert.Equal(expected, MatchDisplay.GoalSituationLabel(situation, isEmptyNet));
    }

    [Fact]
    public void EveryInfractionAndPenaltyKindHasADescription()
    {
        Assert.All(Enum.GetValues<Infraction>(), infraction => Assert.NotEmpty(MatchDisplay.InfractionName(infraction)));
        Assert.Equal("High-sticking", MatchDisplay.InfractionName(Infraction.HighSticking));
        Assert.Equal("2 min", MatchDisplay.PenaltyDescription(PenaltyKind.Minor, 2));
        Assert.Equal("5 min major", MatchDisplay.PenaltyDescription(PenaltyKind.Major, 5));
        Assert.Equal("Game misconduct", MatchDisplay.PenaltyDescription(PenaltyKind.GameMisconduct, 10));
        Assert.Equal("Penalty shot", MatchDisplay.PenaltyDescription(PenaltyKind.PenaltyShot, 0));
    }

    [Fact]
    public async Task BoxScoresListEveryGoalAndPenaltyWithTheRunningScore()
    {
        var session = await PlayDays(3);

        Assert.All(session.Snapshot.Season.Results, result =>
        {
            var detail = new MatchDetailViewModel(result, session.PlayersById, session.GetTeam);

            Assert.Equal(result.Goals.Count, detail.ScoringSummary.Count);
            Assert.Equal(result.Goals.Count == 0, detail.HasNoGoals);
            Assert.Equal(
                result.Goals.Select(goal => MatchDisplay.PeriodTime(goal.Period, goal.TimeInPeriod)),
                detail.ScoringSummary.Select(row => row.Time));
            Assert.Equal(
                result.Goals.Select(goal => PlayerDisplay.FullName(session.PlayersById[goal.ScorerId])),
                detail.ScoringSummary.Select(row => row.Scorer));
            Assert.All(
                result.Goals.Zip(detail.ScoringSummary),
                pair => Assert.Equal(pair.First.PrimaryAssistId is null, pair.Second.Assists == "Unassisted"));
            if (detail.ScoringSummary.Count > 0)
            {
                var playerGoals = (Away: result.Away.Skaters.Sum(skater => skater.Goals), Home: result.Home.Skaters.Sum(skater => skater.Goals));
                Assert.Equal($"{playerGoals.Away}–{playerGoals.Home}", detail.ScoringSummary[^1].Score);
            }

            Assert.Equal(result.Penalties.Count, detail.PenaltySummary.Count);
            Assert.Equal(
                result.Penalties.Select(penalty => MatchDisplay.InfractionName(penalty.Infraction)),
                detail.PenaltySummary.Select(row => row.Infraction));
        });
        Assert.Contains(session.Snapshot.Season.Results, result => result.Penalties.Count > 0);
    }

    [Fact]
    public async Task BoxScoresShowFiveOnFiveSharesAndGoalsSavedAboveExpected()
    {
        var session = await PlayDays(1);
        var result = session.Snapshot.Season.Results[0];
        var detail = new MatchDetailViewModel(result, session.PlayersById, session.GetTeam);

        foreach (var (side, snapshot) in new[] { (detail.Home, result.Home), (detail.Away, result.Away) })
        {
            Assert.Equal(
                snapshot.Skaters.Select(skater => (
                    MatchDisplay.Percentage(skater.OnIce.FiveOnFive.CorsiPercentage),
                    MatchDisplay.Percentage(skater.OnIce.FiveOnFive.ExpectedGoalsPercentage))),
                side.Skaters.Select(row => (row.CorsiPercentage, row.ExpectedGoalsPercentage)));
            Assert.Equal(
                MatchDisplay.GoalsSavedAboveExpected(snapshot.Goalie.ExpectedGoalsAgainst - snapshot.Goalie.GoalsAgainst),
                side.Goalie.GoalsSavedAboveExpected);
        }
    }

    [Fact]
    public async Task RosterBasicAndAdvancedViewsShowTheSeasonTotals()
    {
        var session = await PlayDays(2);
        var team = session.ManagedTeam;
        var roster = new TeamRosterViewModel(session, team.Id, columns: RosterColumns.Advanced);
        var season = session.Snapshot.Season;

        Assert.True(roster.ShowsAdvanced);
        Assert.All(roster.Skaters.Where(row => !row.IsScratched), row =>
        {
            var statistics = season.SkaterStatistics.Single(skater => skater.PlayerId == row.Player.Id);
            var fiveOnFive = statistics.OnIce.FiveOnFive;
            Assert.Equal(statistics.PowerPlayPoints, row.Season.PowerPlayPoints);
            Assert.Equal(statistics.ShorthandedPoints, row.Season.ShorthandedPoints);
            Assert.Equal((fiveOnFive.AttemptsFor, fiveOnFive.AttemptsAgainst), (row.Season.CorsiFor, row.Season.CorsiAgainst));
            Assert.Equal((fiveOnFive.UnblockedAttemptsFor, fiveOnFive.UnblockedAttemptsAgainst), (row.Season.FenwickFor, row.Season.FenwickAgainst));
        });

        var starter = roster.Goalies.Single(row => row.Player.Id == team.Lineup.StartingGoalieId);
        var goalie = season.GoalieStatistics.Single(statistics => statistics.PlayerId == starter.Player.Id);
        Assert.Equal(goalie.Shutouts, starter.Season.Shutouts);

        // Formatted figures are pinned to the literal text for a known skater and goalie.
        var skater = roster.Skaters.Single(row => row.Name == "Anton Berglund");
        Assert.Equal(
            ["0", "0", "0", "11:42", "63.6", "68.0", "66.7", "0.76", "63.0", "0.18"],
            [
                skater.Season.PlusMinus,
                skater.Season.PowerPlayPoints.ToString(CultureInfo.InvariantCulture),
                skater.Season.ShorthandedPoints.ToString(CultureInfo.InvariantCulture),
                skater.Season.TimeOnIcePerGame,
                skater.Season.FaceoffPercentage,
                skater.Season.CorsiPercentage,
                skater.Season.FenwickPercentage,
                skater.Season.OnIceExpectedGoalsFor,
                skater.Season.OnIceExpectedGoalsPercentage,
                skater.Season.IndividualExpectedGoals,
            ]);
        Assert.Equal("Carter Bouchard", starter.Name);
        Assert.Equal(
            ["3.07", "3.26", "+0.26", "58:41", "58:41"],
            [
                starter.Season.GoalsAgainstAverage,
                starter.Season.ExpectedGoalsAgainst,
                starter.Season.GoalsSavedAboveExpected,
                starter.Season.TimeInNet,
                starter.Season.TimeOnIcePerGame,
            ]);

        // The backup has not started, so every average is undefined.
        var backup = roster.Goalies.Single(row => row.Player.Id == team.Lineup.BackupGoalieId);
        Assert.Equal(["—", "—", "—"], new[] { backup.Season.SavePercentage, backup.Season.GoalsAgainstAverage, backup.Season.TimeOnIcePerGame });
    }

    [Fact]
    public async Task TheProfileShowsTheFullStatisticLine()
    {
        var session = await PlayDays(2);
        var team = session.ManagedTeam;
        var centre = team.Lineup.ForwardLines[0].CentreId;
        var statistics = session.Snapshot.Season.SkaterStatistics.Single(skater => skater.PlayerId == centre);

        var line = PlayerProfileTests.Profile(session, centre).StatisticGroups.SelectMany(group => group.Statistics).ToDictionary(stat => stat.Label, stat => stat.Value);

        Assert.Equal(
            ["GP", "G", "A", "P", "+/-", "PIM", "ENG", "PPG", "PPA", "PPP", "SHG", "SHA", "SHP", "S", "SAT", "ixG", "HIT", "BLK", "TK", "GV",
                "FOW", "FOL", "FO%", "TOI", "TOI/GP", "CF", "CA", "CF%", "FF", "FA", "FF%", "SF", "SA", "SF%", "GF", "GA", "GF%", "xGF", "xGA", "xGF%"],
            line.Keys);
        Assert.Equal(Count(statistics.GamesPlayed), line["GP"]);
        Assert.Equal(Count(statistics.Hits), line["HIT"]);
        Assert.Equal(Count(statistics.FaceoffsWon), line["FOW"]);
        Assert.Equal(MatchDisplay.Percentage(statistics.FaceoffPercentage), line["FO%"]);
        Assert.Equal(MatchDisplay.TimeOnIce(statistics.TimeOnIce), line["TOI"]);
        Assert.Equal(Count(statistics.OnIce.FiveOnFive.ShotsFor), line["SF"]);
        Assert.Equal(Count(statistics.OnIce.FiveOnFive.GoalsAgainst), line["GA"]);
        Assert.Equal(MatchDisplay.Percentage(statistics.OnIce.FiveOnFive.GoalsPercentage), line["GF%"]);

        var goalie = PlayerProfileTests.Profile(session, team.Lineup.StartingGoalieId);
        Assert.Equal(
            ["GP", "SA", "SV", "GA", "SV%", "GAA", "SO", "xGA", "GSAx", "TOI", "TOI/GP"],
            goalie.StatisticGroups.SelectMany(group => group.Statistics).Select(stat => stat.Label));
    }

    [Fact]
    public async Task TeamStatisticsShowSpecialTeamsFaceoffsAndFiveOnFiveShares()
    {
        var session = GameTestData.StartSession();
        var opening = new TeamStatisticsPageViewModel(session);
        Assert.Equal(["PP%", "PK%", "FO%", "CF%", "FF%", "SF%", "xGF%"], opening.Statistics.Select(stat => stat.Label));
        Assert.All(opening.Statistics, stat => Assert.Equal("—", stat.Value));

        session = await PlayDays(3);
        var statistics = session.Snapshot.Season.TeamStatistics.Single(team => team.TeamId == session.Snapshot.ManagedTeamId);
        var strip = new TeamStatisticsPageViewModel(session).Statistics.ToDictionary(stat => stat.Label, stat => stat.Value);

        Assert.Equal(MatchDisplay.Percentage(statistics.PowerPlayPercentage), strip["PP%"]);
        Assert.Equal(MatchDisplay.Percentage(statistics.PenaltyKillPercentage), strip["PK%"]);
        Assert.Equal(MatchDisplay.Percentage(statistics.FaceoffPercentage), strip["FO%"]);
        Assert.Equal(MatchDisplay.Percentage(statistics.ShotTotals.FiveOnFive.CorsiPercentage), strip["CF%"]);
        Assert.Equal(MatchDisplay.Percentage(statistics.ShotTotals.FiveOnFive.FenwickPercentage), strip["FF%"]);
        Assert.Equal(MatchDisplay.Percentage(statistics.ShotTotals.FiveOnFive.ShotsPercentage), strip["SF%"]);
        Assert.Equal(MatchDisplay.Percentage(statistics.ShotTotals.FiveOnFive.ExpectedGoalsPercentage), strip["xGF%"]);
        Assert.NotEqual("—", strip["CF%"]);
    }

    private static string Count(int value) => value.ToString(CultureInfo.CurrentCulture);

    private static async Task<GameSession> PlayDays(int days)
    {
        var session = GameTestData.StartSession(new GameManager());
        var shell = new GameShellViewModel(session);
        for (var day = 0; day < days; day++)
        {
            await shell.AdvanceDayCommand.ExecuteAsync(null);
        }

        return session;
    }
}