using System.Globalization;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Schedule;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class AdvancedStatisticsDisplayTests
{
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
        var teamNames = session.Snapshot.League.Teams.ToDictionary(team => team.Id, team => team.Name);

        Assert.All(session.Snapshot.Season.Results, result =>
        {
            var detail = new MatchDetailViewModel(result, session.PlayersById, id => teamNames[id]);

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
        var detail = new MatchDetailViewModel(result, session.PlayersById, _ => "Team");

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
            Assert.Equal(statistics.PlusMinus > 0 ? $"+{statistics.PlusMinus}" : statistics.PlusMinus.ToString(CultureInfo.CurrentCulture), row.Season.PlusMinus);
            Assert.Equal(statistics.PowerPlayPoints, row.Season.PowerPlayPoints);
            Assert.Equal(statistics.ShorthandedPoints, row.Season.ShorthandedPoints);
            Assert.Equal(MatchDisplay.TimeOnIce(statistics.TimeOnIcePerGame), row.Season.TimeOnIcePerGame);
            Assert.Equal(MatchDisplay.Percentage(statistics.FaceoffPercentage), row.Season.FaceoffPercentage);
            Assert.Equal((fiveOnFive.AttemptsFor, fiveOnFive.AttemptsAgainst), (row.Season.CorsiFor, row.Season.CorsiAgainst));
            Assert.Equal((fiveOnFive.UnblockedAttemptsFor, fiveOnFive.UnblockedAttemptsAgainst), (row.Season.FenwickFor, row.Season.FenwickAgainst));
            Assert.Equal(MatchDisplay.Percentage(fiveOnFive.CorsiPercentage), row.Season.CorsiPercentage);
            Assert.Equal(MatchDisplay.Percentage(fiveOnFive.FenwickPercentage), row.Season.FenwickPercentage);
            Assert.Equal(MatchDisplay.ExpectedGoals(fiveOnFive.ExpectedGoalsFor), row.Season.OnIceExpectedGoalsFor);
            Assert.Equal(MatchDisplay.Percentage(fiveOnFive.ExpectedGoalsPercentage), row.Season.OnIceExpectedGoalsPercentage);
            Assert.Equal(MatchDisplay.ExpectedGoals(statistics.ExpectedGoals), row.Season.IndividualExpectedGoals);
        });

        var starter = roster.Goalies.Single(row => row.Player.Id == team.Lineup.StartingGoalieId);
        var goalie = season.GoalieStatistics.Single(statistics => statistics.PlayerId == starter.Player.Id);
        Assert.Equal(MatchDisplay.GoalsAgainstAverage(goalie.GoalsAgainstAverage), starter.Season.GoalsAgainstAverage);
        Assert.Equal(goalie.Shutouts, starter.Season.Shutouts);
        Assert.Equal(MatchDisplay.ExpectedGoals(goalie.ExpectedGoalsAgainst), starter.Season.ExpectedGoalsAgainst);
        Assert.Equal(MatchDisplay.GoalsSavedAboveExpected(goalie.GoalsSavedAboveExpected), starter.Season.GoalsSavedAboveExpected);
        Assert.Equal(MatchDisplay.TimeOnIce(goalie.TimeOnIce), starter.Season.TimeInNet);
        Assert.Equal(MatchDisplay.TimeOnIce(goalie.TimeOnIcePerGame), starter.Season.TimeOnIcePerGame);

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
        var roster = new TeamRosterViewModel(session, team.Id, centre);

        var line = roster.SelectedPlayer!.SeasonStatisticGroups.SelectMany(group => group.Statistics).ToDictionary(stat => stat.Label, stat => stat.Value);

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

        var goalie = new TeamRosterViewModel(session, team.Id, team.Lineup.StartingGoalieId).SelectedPlayer!;
        Assert.Equal(
            ["GP", "SA", "SV", "GA", "SV%", "GAA", "SO", "xGA", "GSAx", "TOI", "TOI/GP"],
            goalie.SeasonStatisticGroups.SelectMany(group => group.Statistics).Select(stat => stat.Label));
    }

    [Fact]
    public async Task TheTeamStripShowsSpecialTeamsFaceoffsAndFiveOnFiveShares()
    {
        var session = GameTestData.StartSession();
        var opening = new TeamRosterViewModel(session, session.ManagedTeam.Id);
        Assert.Equal(["PP%", "PK%", "FO%", "CF%", "FF%", "SF%", "xGF%"], opening.TeamStatistics.Select(stat => stat.Label));
        Assert.All(opening.TeamStatistics, stat => Assert.Equal("—", stat.Value));

        session = await PlayDays(3);
        var other = session.Snapshot.League.Teams.First(team => team.Id != session.Snapshot.ManagedTeamId);
        var statistics = session.Snapshot.Season.TeamStatistics.Single(team => team.TeamId == other.Id);
        var strip = new TeamRosterViewModel(session, other.Id).TeamStatistics.ToDictionary(stat => stat.Label, stat => stat.Value);

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