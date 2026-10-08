using HockeySim.Desktop.Game;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Schedule;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class SeasonAdvancementTests
{
    private static readonly DateOnly OpeningDay = new(2026, 10, 1);

    [Fact]
    public void ShellShowsTheCurrentDateAndOffersToPlayIt()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());

        Assert.Equal($"Regular season · {MatchDisplay.ShortDate(OpeningDay)}", shell.PhaseLabel);
        Assert.Equal("Continue", shell.ContinueLabel);
        Assert.True(shell.AdvanceDayCommand.CanExecute(null));
        Assert.Equal($"Play {MatchDisplay.ShortDate(OpeningDay)}: 16 league matches, including yours.", shell.ContinueDescription);
        Assert.StartsWith("Today · ", shell.Home.NextMatchCaption, StringComparison.Ordinal);
        Assert.Empty(shell.Home.LatestResults);
        Assert.Contains("opens", shell.Home.LatestResultsCaption, StringComparison.Ordinal);
        Assert.All(shell.Home.DivisionStandings, row => Assert.Equal(0, row.GamesPlayed));
    }

    [Fact]
    public async Task AdvancingPlaysTheDayAndRefreshesEveryPage()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);

        await shell.AdvanceDayCommand.ExecuteAsync(null);

        var season = session.Snapshot.Season;
        Assert.Null(shell.AdvanceError);
        Assert.Equal(16, season.Results.Count);
        Assert.Equal(OpeningDay.AddDays(1), season.CurrentDate);
        Assert.Equal($"Regular season · {MatchDisplay.ShortDate(OpeningDay.AddDays(1))}", shell.PhaseLabel);

        Assert.Equal($"LEAGUE RESULTS · {MatchDisplay.ShortDate(OpeningDay).ToUpperInvariant()}", shell.Home.LatestResultsTitle);
        Assert.Equal(16, shell.Home.LatestResults.Count);
        Assert.Single(shell.Home.LatestResults, result => result.InvolvesManagedTeam);
        Assert.StartsWith("Last: ", shell.Home.LastResultCaption, StringComparison.Ordinal);
        var standings = shell.Home.DivisionStandings;
        Assert.All(standings, row => Assert.Equal(1, row.GamesPlayed));
        Assert.Equal(1, standings[0].Rank);
        Assert.All(standings.Zip(standings.Skip(1)), pair =>
        {
            Assert.True(pair.First.Rank <= pair.Second.Rank);
            Assert.True(pair.First.Points >= pair.Second.Points);
        });

        var firstMatch = shell.Schedule.Matches[0];
        Assert.True(firstMatch.IsCompleted);
        Assert.Equal(MatchDisplay.ResultFor(season.Results.Single(result =>
            result.Home.TeamId == session.ManagedTeam.Id || result.Away.TeamId == session.ManagedTeam.Id), session.ManagedTeam.Id), firstMatch.Result);
        Assert.True(shell.Schedule.Matches[1].IsNext);
        Assert.Equal($"{session.ManagedTeam.Name} · 1 of 84 played", shell.Schedule.Subtitle);
    }

    [Fact]
    public async Task ADayWithoutMatchesIsPresentedAsSuch()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        await shell.AdvanceDayCommand.ExecuteAsync(null);
        var offDay = OpeningDay.AddDays(1);

        // The current calendar plays a round every other day, so the day after opening night is
        // empty for the whole league, including the managed team.
        Assert.Equal($"No league matches on {MatchDisplay.ShortDate(offDay)}. Continue to the next day.", shell.ContinueDescription);
        Assert.DoesNotContain("Today", shell.Home.NextMatchCaption, StringComparison.Ordinal);

        await shell.AdvanceDayCommand.ExecuteAsync(null);

        Assert.Null(shell.AdvanceError);
        Assert.Equal(OpeningDay.AddDays(2), session.Snapshot.Season.CurrentDate);
        Assert.Equal(16, session.Snapshot.Season.Results.Count);
        Assert.False(shell.Home.HasLatestResults);
        Assert.Equal($"No league matches were scheduled on {MatchDisplay.LongDate(offDay)}.", shell.Home.LatestResultsCaption);
        Assert.StartsWith("Today", shell.Home.NextMatchCaption, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpeningAResultShowsItsSingleMatchBoxScore()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        await shell.AdvanceDayCommand.ExecuteAsync(null);
        var otherResult = shell.Home.LatestResults.First(result => !result.InvolvesManagedTeam);

        otherResult.OpenCommand.Execute(null);

        var schedule = Assert.IsType<SchedulePageViewModel>(shell.CurrentPage);
        var detail = Assert.IsType<MatchDetailViewModel>(schedule.SelectedResult);
        var result = session.Snapshot.Season.Results.Single(candidate =>
            candidate.Date == detail.Date && candidate.Home.TeamId == detail.HomeTeamId);
        Assert.Equal(otherResult.HomeTeamName, schedule.SelectedTeam.Name);
        Assert.Equal(otherResult.HomeTeamName, detail.Home.TeamName);
        Assert.Equal(otherResult.AwayTeamName, detail.Away.TeamName);
        Assert.Equal(MatchDisplay.FinalLabel(result.Decision), detail.FinalLabel);
        Assert.Equal(result.Decision == MatchDecision.Shootout, detail.IsShootout);

        foreach (var (side, snapshot) in new[] { (detail.Home, result.Home), (detail.Away, result.Away) })
        {
            Assert.Equal(snapshot.Score, side.Score);
            Assert.Equal(snapshot.Shots, side.Shots);
            Assert.Equal(snapshot.Skaters.Select(line => (line.Goals, line.Assists)), side.Skaters.Select(skater => (skater.Goals, skater.Assists)));
            Assert.Equal(
                snapshot.Skaters.Select(line => (
                    MatchDisplay.PlusMinus(line.PlusMinus),
                    MatchDisplay.TimeOnIce(line.TimeOnIce),
                    line.Shots,
                    line.ShotAttempts,
                    MatchDisplay.ExpectedGoals(line.ExpectedGoals),
                    line.Hits,
                    line.BlockedShots,
                    MatchDisplay.Faceoffs(line.FaceoffsWon, line.FaceoffsLost),
                    line.Takeaways,
                    line.Giveaways,
                    line.PenaltyMinutes,
                    line.PowerPlayGoals,
                    line.ShorthandedGoals)),
                side.Skaters.Select(skater => (
                    skater.PlusMinus,
                    skater.TimeOnIce,
                    skater.Shots,
                    skater.ShotAttempts,
                    skater.ExpectedGoals,
                    skater.Hits,
                    skater.BlockedShots,
                    skater.Faceoffs,
                    skater.Takeaways,
                    skater.Giveaways,
                    skater.PenaltyMinutes,
                    skater.PowerPlayGoals,
                    skater.ShorthandedGoals)));
            Assert.Equal(MatchDisplay.PowerPlay(snapshot.PowerPlayGoals, snapshot.PowerPlayOpportunities), side.PowerPlay);
            Assert.Equal(snapshot.Skaters.Sum(line => line.PenaltyMinutes), side.PenaltyMinutes);
            Assert.Equal(MatchDisplay.ExpectedGoals(snapshot.Goalie.ExpectedGoalsAgainst), side.Goalie.ExpectedGoalsAgainst);
            Assert.Equal(MatchDisplay.TimeOnIce(snapshot.Goalie.TimeOnIce), side.Goalie.TimeOnIce);
            Assert.All(side.Skaters, skater => Assert.Equal(skater.Goals + skater.Assists, skater.Points));
            var shootoutGoal = result.Decision == MatchDecision.Shootout && result.WinnerId == snapshot.TeamId ? 1 : 0;
            Assert.Equal(side.Score - shootoutGoal, side.Skaters.Sum(skater => skater.Goals));
            Assert.Equal(side.Goalie.ShotsAgainst - side.Goalie.GoalsAgainst, side.Goalie.Saves);
            Assert.Equal(MatchDisplay.SavePercentage(side.Goalie.Saves, side.Goalie.ShotsAgainst), side.Goalie.SavePercentage);
        }

    }

    [Fact]
    public void ScheduleListsTheSelectedTeamsSeasonAndExplainsUnplayedMatches()
    {
        var session = GameTestData.StartSession();
        var schedule = new GameShellViewModel(session).Schedule;

        Assert.Equal(84, schedule.Matches.Count);
        Assert.Equal(42, schedule.Matches.Count(match => match.Venue == "vs"));
        Assert.True(schedule.Matches[0].IsNext);
        Assert.All(schedule.Matches, match => Assert.False(match.IsCompleted));

        schedule.SelectedMatch = schedule.Matches[3];

        Assert.Null(schedule.SelectedResult);
        Assert.Contains("is scheduled for", schedule.SelectionHint, StringComparison.Ordinal);

        var otherTeam = schedule.Teams.First(team => !team.IsManaged);
        schedule.SelectedTeam = otherTeam;

        Assert.Null(schedule.SelectedMatch);
        Assert.Equal(84, schedule.Matches.Count);
        Assert.All(schedule.Matches, match => Assert.NotEqual(otherTeam.Name, match.Opponent));
        Assert.StartsWith(otherTeam.Name, schedule.Subtitle, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedDayIsReportedAndNothingIsApplied()
    {
        var session = GameTestData.StartSession(new GameManager(new FailingMatchSimulator()));
        var shell = new GameShellViewModel(session);
        var before = session.Snapshot;

        await shell.AdvanceDayCommand.ExecuteAsync(null);

        Assert.True(shell.HasAdvanceError);
        Assert.Contains("no results were applied", shell.AdvanceError, StringComparison.Ordinal);
        Assert.Contains(FailingMatchSimulator.FailureMessage, shell.AdvanceError, StringComparison.Ordinal);
        Assert.Equal(before.Season.CurrentDate, session.Snapshot.Season.CurrentDate);
        Assert.Equal(before.RandomState, session.Snapshot.RandomState);
        Assert.Empty(session.Snapshot.Season.Results);
        Assert.Empty(shell.Home.LatestResults);
        Assert.All(shell.Schedule.Matches, match => Assert.False(match.IsCompleted));
        Assert.False(session.IsAdvancing);
        Assert.True(shell.AdvanceDayCommand.CanExecute(null));

        shell.DismissAdvanceErrorCommand.Execute(null);

        Assert.False(shell.HasAdvanceError);
    }

    [Fact]
    public async Task OverlappingAdvancementIsPrevented()
    {
        using var engine = new GatedMatchSimulator();
        var session = GameTestData.StartSession(new GameManager(engine));
        var shell = new GameShellViewModel(session);

        var advancing = shell.AdvanceDayCommand.ExecuteAsync(null);
        await engine.WaitUntilPlayingAsync();

        Assert.True(session.IsAdvancing);
        Assert.Equal("Playing…", shell.ContinueLabel);
        Assert.False(shell.AdvanceDayCommand.CanExecute(null));
        await Assert.ThrowsAsync<InvalidOperationException>(session.AdvanceDayAsync);

        engine.Release();
        await advancing;

        Assert.Null(shell.AdvanceError);
        Assert.False(session.IsAdvancing);
        Assert.Equal("Continue", shell.ContinueLabel);
        Assert.Equal(16, session.Snapshot.Season.Results.Count);
        Assert.Equal(OpeningDay.AddDays(1), session.Snapshot.Season.CurrentDate);
        Assert.True(shell.AdvanceDayCommand.CanExecute(null));
    }

    [Fact]
    public async Task ACompletedSeasonCannotBeAdvancedButStaysBrowsable()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);

        // 84 rounds, every other day, finish 167 days after opening night. Injured players are
        // replaced whenever Continue waits for it, as the user would.
        for (var day = 0; day < 200 && !session.Snapshot.Season.IsComplete; day++)
        {
            if (shell.HasPlayersToReplace)
            {
                session.SetLineup(InjuredPlayerReplacement.ReplacingInjured(session.Snapshot));
            }

            await shell.AdvanceDayCommand.ExecuteAsync(null);
            Assert.Null(shell.AdvanceError);
        }

        Assert.True(session.Snapshot.Season.IsComplete);
        Assert.False(shell.AdvanceDayCommand.CanExecute(null));
        Assert.Equal("Regular season complete", shell.PhaseLabel);
        Assert.StartsWith("The regular season is complete", shell.ContinueDescription, StringComparison.Ordinal);
        Assert.Equal("Regular season complete", shell.Home.NextMatchTitle);
        Assert.Equal(16, shell.Home.LatestResults.Count);
        Assert.All(shell.Home.DivisionStandings, row => Assert.Equal(84, row.GamesPlayed));
        Assert.EndsWith("final standings", shell.Standings.Subtitle, StringComparison.Ordinal);
        Assert.All(shell.Standings.Tables.SelectMany(table => table.Rows), row => Assert.Equal(84, row.GamesPlayed));
        Assert.Equal(84, shell.Roster.Roster.Goalies.Sum(row => row.Season.GamesPlayed));
        Assert.All(shell.Schedule.Matches, match => Assert.True(match.IsCompleted));
        Assert.DoesNotContain(shell.Schedule.Matches, match => match.IsNext);

        shell.Navigate(ShellPage.Roster);
        Assert.IsType<RosterPageViewModel>(shell.CurrentPage);
        shell.Home.LatestResults[0].OpenCommand.Execute(null);
        Assert.NotNull(Assert.IsType<SchedulePageViewModel>(shell.CurrentPage).SelectedResult);
    }

    [Theory]
    [InlineData(0, 0, "—")]
    [InlineData(30, 30, "1.000")]
    [InlineData(28, 30, ".933")]
    public void SavePercentageIsOnlyShownOnceAGoalieHasFacedAShot(int saves, int shotsAgainst, string expected)
    {
        Assert.Equal(expected, MatchDisplay.SavePercentage(saves, shotsAgainst));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(65, "1:05")]
    [InlineData(1062, "17:42")]
    [InlineData(5400, "90:00")]
    public void TimeOnIceShowsMinutesAndSecondsAndKeepsCountingPastAnHour(int seconds, string expected)
    {
        Assert.Equal(expected, MatchDisplay.TimeOnIce(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(2, "+2")]
    [InlineData(0, "0")]
    [InlineData(-1, "-1")]
    public void PlusMinusShowsItsSign(int plusMinus, string expected)
    {
        Assert.Equal(expected, MatchDisplay.PlusMinus(plusMinus));
    }

    [Theory]
    [InlineData(0, 0, "0/0")]
    [InlineData(1, 3, "1/3")]
    [InlineData(2, 2, "2/2")]
    public void APowerPlayShowsGoalsOfOpportunities(int goals, int opportunities, string expected)
    {
        Assert.Equal(expected, MatchDisplay.PowerPlay(goals, opportunities));
    }

    [Theory]
    [InlineData(0, "0.00")]
    [InlineData(0.416, "0.42")]
    [InlineData(2.5, "2.50")]
    public void ExpectedGoalsShowTwoDecimalPlaces(double expectedGoals, string expected)
    {
        Assert.Equal(expected, MatchDisplay.ExpectedGoals(expectedGoals));
    }

    [Theory]
    [InlineData(0, 0, "—")]
    [InlineData(8, 5, "8–5")]
    [InlineData(0, 3, "0–3")]
    public void FaceoffsShowWonAndLostOrADashWhenNoneWereTaken(int won, int lost, string expected)
    {
        Assert.Equal(expected, MatchDisplay.Faceoffs(won, lost));
    }
}