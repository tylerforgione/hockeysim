using System.Globalization;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Schedule;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

using Xunit;

namespace HockeySim.Desktop.Tests;

/// <summary>
/// The box score of a played match, taken from the preseason, whose 112 matches end in
/// regulation, overtime, and shootouts alike.
/// </summary>
public sealed class MatchDetailViewModelTests
{
    private static readonly Lazy<GameSession> Session = new(() => GameTestData.StartSession());

    [Theory]
    [InlineData(MatchDecision.Regulation, new[] { "1st", "2nd", "3rd" }, new[] { "1st", "2nd", "3rd" })]
    [InlineData(MatchDecision.Overtime, new[] { "1st", "2nd", "3rd", "OT" }, new[] { "1st", "2nd", "3rd", "OT" })]
    [InlineData(MatchDecision.Shootout, new[] { "1st", "2nd", "3rd", "OT", "SO" }, new[] { "1st", "2nd", "3rd", "OT" })]
    public void EachTableHasAColumnForEveryPeriodPlayedAndTheGoalsAShootoutColumn(
        MatchDecision decision,
        string[] goalColumns,
        string[] otherColumns)
    {
        var (result, detail) = FirstDecidedBy(decision);
        var tables = detail.PeriodTables.ToDictionary(table => table.Title);

        Assert.Equal(["GOALS", "SHOTS", "EXPECTED GOALS", "POWER PLAY"], detail.PeriodTables.Select(table => table.Title));
        Assert.Equal(goalColumns, tables["GOALS"].Periods);
        Assert.All(detail.PeriodTables.Skip(1), table => Assert.Equal(otherColumns, table.Periods));
        Assert.All(detail.PeriodTables, table =>
        {
            Assert.Equal([detail.Away.TeamInitials, detail.Home.TeamInitials], table.Rows.Select(row => row.Team));
            Assert.All(table.Rows, row => Assert.Equal(table.Periods.Count, row.Cells.Count));
        });
        Assert.Equal([Number(result.Away.Score), Number(result.Home.Score)], tables["GOALS"].Rows.Select(row => row.Total));
        Assert.Equal(
            [Number(result.Away.Shots), Number(result.Home.Shots)],
            tables["SHOTS"].Rows.Select(row => row.Total));
        Assert.Equal(
            [result.Away.Periods.Sum(period => period.Shots), result.Home.Periods.Sum(period => period.Shots)],
            tables["SHOTS"].Rows.Select(row => row.Cells.Sum(int.Parse)));
    }

    [Fact]
    public void GoalsByPeriodCountTheScoringSummaryAndTheShootoutWinnersDecidingGoal()
    {
        var (result, detail) = FirstDecidedBy(MatchDecision.Shootout);
        var goals = detail.PeriodTables.Single(table => table.Title == "GOALS");

        foreach (var (row, side) in goals.Rows.Zip([result.Away, result.Home]))
        {
            Assert.Equal(
                Enumerable.Range(1, side.Periods.Count)
                    .Select(period => Number(result.Goals.Count(goal => goal.TeamId == side.TeamId && goal.Period == period)))
                    .Append(Number(result.WinnerId == side.TeamId ? 1 : 0)),
                row.Cells);
        }
    }

    [Fact]
    public void ThePowerPlayByPeriodIsGoalsOfOpportunities()
    {
        var (result, detail) = Session.Value.Snapshot.Season.PreseasonResults
            .Where(result => result.Home.PowerPlayGoals > 0)
            .Select(result => (result, Detail(result)))
            .First();
        var powerPlay = detail.PeriodTables.Single(table => table.Title == "POWER PLAY");
        var home = powerPlay.Rows[1];

        Assert.Equal(
            result.Home.Periods.Select(period => MatchDisplay.PowerPlay(
                result.Goals.Count(goal => goal.TeamId == result.Home.TeamId && goal.Period == period.Period && goal.Situation == GoalSituation.PowerPlay),
                period.PowerPlayOpportunities)),
            home.Cells);
        Assert.Equal(MatchDisplay.PowerPlay(result.Home.PowerPlayGoals, result.Home.PowerPlayOpportunities), home.Total);
    }

    [Fact]
    public void TheBannerShowsEachTeamsRecordColoursAndTheArena()
    {
        var session = Session.Value;
        var (result, detail) = FirstDecidedBy(MatchDecision.Overtime);
        var loserIsHome = result.WinnerId == result.Away.TeamId;
        var record = (loserIsHome ? result.Home : result.Away).RecordAfter;

        Assert.Equal(session.GetTeam(result.Home.TeamId).Arena, detail.Arena);
        Assert.Equal(session.GetTeam(result.Away.TeamId).Colours, detail.Away.Colours);
        Assert.Equal($"{record.Wins}-{record.Losses}-{record.OvertimeLosses}", (loserIsHome ? detail.Home : detail.Away).Record);
        Assert.True(record.OvertimeLosses > 0);
        Assert.Equal("Final · Overtime", detail.FinalLabel);

        // In the playoffs the record is the series record.
        var playoffGame = Detail(result with { Phase = SeasonPhase.Playoffs });
        Assert.Equal($"Series {record.Wins}–{record.Losses}", (loserIsHome ? playoffGame.Home : playoffGame.Away).Record);
    }

    [Fact]
    public void TheComparisonGivesEachFigureWithTheAwayTeamsShare()
    {
        var (result, detail) = FirstDecidedBy(MatchDecision.Regulation);

        Assert.Equal(
            ["Shots", "Shot attempts", "Expected goals", "5-on-5 xGF%", "Faceoffs won", "Hits", "Blocked shots", "Giveaways", "Penalty minutes"],
            detail.Comparison.Select(row => row.Label));
        var shots = detail.Comparison[0];
        Assert.Equal((Number(result.Away.Shots), Number(result.Home.Shots)), (shots.AwayValue, shots.HomeValue));
        Assert.Equal(result.Away.Shots / (double)(result.Away.Shots + result.Home.Shots), shots.AwayShare, precision: 10);
        Assert.All(detail.Comparison, row => Assert.InRange(row.AwayShare, 0, 1));
        var share = detail.Comparison.Single(row => row.Label == "5-on-5 xGF%");
        Assert.Equal(MatchDisplay.Percentage(result.Away.ShotTotals.FiveOnFive.ExpectedGoalsPercentage), share.AwayValue);
    }

    [Fact]
    public void TheSummaryNamesTheThreeStarsInOrderAndBothGoalies()
    {
        var session = Session.Value;
        var (result, detail) = FirstDecidedBy(MatchDecision.Regulation);

        Assert.Equal(["1st star", "2nd star", "3rd star"], detail.ThreeStars.Select(star => star.Star));
        Assert.Equal(
            result.ThreeStars.Select(star => session.PlayersById[star.PlayerId].FirstName + " " + session.PlayersById[star.PlayerId].LastName),
            detail.ThreeStars.Select(star => star.Name));
        Assert.Equal(
            result.ThreeStars.Select(star => star.Score.ToString("0.00", CultureInfo.CurrentCulture)),
            detail.ThreeStars.Select(star => star.GameScore));
        Assert.Equal([detail.Away.TeamInitials, detail.Home.TeamInitials], detail.Goalies.Select(goalie => goalie.Team));
        Assert.Equal([detail.Away.Goalie, detail.Home.Goalie], detail.Goalies.Select(goalie => goalie.Goalie));
    }

    [Fact]
    public void TheBoxScoreOpensOnItsSummaryAndSwitchesToThePlayers()
    {
        var (_, detail) = FirstDecidedBy(MatchDecision.Regulation);

        Assert.True(detail.IsSummaryTab);
        detail.SelectTabCommand.Execute(MatchDetailTab.Players);
        Assert.True(detail.IsPlayersTab);
        Assert.False(detail.IsSummaryTab);
        detail.SelectTabCommand.Execute(MatchDetailTab.Summary);
        Assert.True(detail.IsSummaryTab);
    }

    private static (CompletedMatchSnapshot Result, MatchDetailViewModel Detail) FirstDecidedBy(MatchDecision decision)
    {
        var result = Session.Value.Snapshot.Season.PreseasonResults.First(result => result.Decision == decision);
        return (result, Detail(result));
    }

    private static MatchDetailViewModel Detail(CompletedMatchSnapshot result) =>
        new(result, Session.Value.PlayersById, Session.Value.GetTeam);

    private static string Number(int value) => value.ToString(CultureInfo.CurrentCulture);
}