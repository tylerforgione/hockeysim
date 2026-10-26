using System.Globalization;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.Standings;
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

        Assert.Equal("Thu 1 Oct 2026 · Next match today · Regular season ends in 166 days", shell.CalendarLabel);
        Assert.Equal("Continue", shell.ContinueLabel);
        Assert.True(shell.AdvanceDayCommand.CanExecute(null));
        Assert.Equal($"{MatchDisplay.ShortDate(OpeningDay)} · 16 league matches, including yours", shell.ContinueDescription);
        Assert.StartsWith("Today · ", shell.Home.TaleOfTheTape.Caption, StringComparison.Ordinal);
        Assert.Equal(MatchDisplay.ShortDate(OpeningDay), shell.Home.UpcomingGames.Rows[0].Date);

        // The latest preseason results are the managed team's, newest first, and open like any other.
        Assert.Equal(6, shell.Home.RecentResults.Rows.Count);
        Assert.All(shell.Home.RecentResults.Rows, result => Assert.True(result.OpenCommand.CanExecute(null)));
        Assert.All(shell.Home.Standings.Tables.Single().Rows, row => Assert.Equal(0, row.GamesPlayed));
    }

    [Fact]
    public async Task ThePreseasonIsShownAsSuchUntilOpeningDay()
    {
        var session = GameTestData.StartPreseasonSession();
        var shell = new GameShellViewModel(session);
        var firstDay = MatchDisplay.ShortDate(session.Snapshot.Season.CurrentDate);

        var daysToOpeningDay = OpeningDay.DayNumber - session.Snapshot.Season.CurrentDate.DayNumber;
        Assert.EndsWith($" · Regular season starts in {daysToOpeningDay} days", shell.CalendarLabel, StringComparison.Ordinal);
        Assert.Equal($"{firstDay} · 16 preseason matches, including yours", shell.ContinueDescription);
        Assert.EndsWith(" · Preseason", shell.Home.TaleOfTheTape.Caption, StringComparison.Ordinal);
        Assert.Equal(7, shell.Home.UpcomingGames.Rows.Count);
        Assert.All(shell.Home.UpcomingGames.Rows, row => Assert.Equal("PRE", row.Phase));
        Assert.Empty(shell.Home.RecentResults.Rows);
        Assert.Equal("No results", shell.Home.RecentResults.EmptyText);

        while (session.Snapshot.Season.Phase == SeasonPhase.Preseason)
        {
            await shell.AdvanceDayCommand.ExecuteAsync(null);
        }

        Assert.EndsWith(" · Regular season ends in 166 days", shell.CalendarLabel, StringComparison.Ordinal);
        Assert.Equal($"{MatchDisplay.ShortDate(OpeningDay)} · 16 league matches, including yours", shell.ContinueDescription);
        Assert.DoesNotContain("Preseason", shell.Home.TaleOfTheTape.Caption, StringComparison.Ordinal);
        Assert.All(shell.Home.Standings.Tables.Single().Rows, row => Assert.Equal(0, row.GamesPlayed));
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
        Assert.StartsWith("Fri 2 Oct 2026 · Next match ", shell.CalendarLabel, StringComparison.Ordinal);
        Assert.EndsWith("Regular season ends in 165 days", shell.CalendarLabel, StringComparison.Ordinal);

        var latest = shell.Home.RecentResults.Rows[0];
        Assert.Equal(MatchDisplay.ShortDate(OpeningDay), latest.Date);
        Assert.Equal(
            MatchDisplay.ResultFor(season.Results.Single(result => result.Home.TeamId == session.ManagedTeam.Id || result.Away.TeamId == session.ManagedTeam.Id), session.ManagedTeam.Id),
            latest.Result);
        var standings = shell.Home.Standings.Tables.Single().Rows;
        Assert.All(standings, row => Assert.Equal(1, row.GamesPlayed));
        Assert.Equal(1, standings[0].Rank);
        Assert.All(standings.Zip(standings.Skip(1)), pair =>
        {
            Assert.True(pair.First.Rank <= pair.Second.Rank);
            Assert.True(pair.First.Points >= pair.Second.Points);
        });

        var regularSeason = shell.Schedule.Matches.Where(match => match.Phase == SeasonPhase.RegularSeason).ToList();
        var firstMatch = regularSeason[0];
        Assert.True(firstMatch.IsCompleted);
        Assert.Equal(MatchDisplay.ResultFor(season.Results.Single(result =>
            result.Home.TeamId == session.ManagedTeam.Id || result.Away.TeamId == session.ManagedTeam.Id), session.ManagedTeam.Id), firstMatch.Result);
        Assert.True(regularSeason[1].IsNext);
        Assert.Equal($"{session.ManagedTeam.Name} · 1 of 84 regular-season matches played", shell.Schedule.Subtitle);
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
        Assert.Equal($"{MatchDisplay.ShortDate(offDay)} · No league matches", shell.ContinueDescription);
        Assert.DoesNotContain("Today", shell.Home.TaleOfTheTape.Caption, StringComparison.Ordinal);

        await shell.AdvanceDayCommand.ExecuteAsync(null);

        Assert.Null(shell.AdvanceError);
        Assert.Equal(OpeningDay.AddDays(2), session.Snapshot.Season.CurrentDate);
        Assert.Equal(16, session.Snapshot.Season.Results.Count);
        Assert.StartsWith("Today", shell.Home.TaleOfTheTape.Caption, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpeningAResultShowsItsSingleMatchBoxScore()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        await shell.AdvanceDayCommand.ExecuteAsync(null);
        var managedId = session.ManagedTeam.Id;
        var otherResult = session.Snapshot.Season.Results.First(candidate => candidate.Home.TeamId != managedId && candidate.Away.TeamId != managedId);

        // The home page only lists the managed team's matches; the league schedule opens any other.
        shell.OpenMatch(otherResult.Date, otherResult.Home.TeamId);

        var schedule = Assert.IsType<SchedulePageViewModel>(shell.CurrentPage);
        var detail = Assert.IsType<MatchDetailViewModel>(schedule.SelectedResult);
        var result = session.Snapshot.Season.Results.Single(candidate =>
            candidate.Date == detail.Date && candidate.Home.TeamId == detail.HomeTeamId);
        Assert.Equal(session.GetTeam(otherResult.Home.TeamId).Name, schedule.SelectedTeam.Name);
        Assert.Equal(session.GetTeam(otherResult.Home.TeamId).Name, detail.Home.TeamName);
        Assert.Equal(session.GetTeam(otherResult.Away.TeamId).Name, detail.Away.TeamName);
        Assert.Equal(MatchDisplay.FinalLabel(result.Decision), detail.FinalLabel);

        foreach (var (side, snapshot) in new[] { (detail.Home, result.Home), (detail.Away, result.Away) })
        {
            Assert.Equal(snapshot.Score, side.Score);
            Assert.Equal(snapshot.Skaters.Select(line => (line.Goals, line.Assists)), side.Skaters.Select(skater => (skater.Goals, skater.Assists)));
            Assert.Equal(
                snapshot.Skaters.Select(line => (
                    line.Shots,
                    line.ShotAttempts,
                    line.Hits,
                    line.BlockedShots,
                    line.Takeaways,
                    line.Giveaways,
                    line.PenaltyMinutes,
                    line.PowerPlayGoals,
                    line.ShorthandedGoals)),
                side.Skaters.Select(skater => (
                    skater.Shots,
                    skater.ShotAttempts,
                    skater.Hits,
                    skater.BlockedShots,
                    skater.Takeaways,
                    skater.Giveaways,
                    skater.PenaltyMinutes,
                    skater.PowerPlayGoals,
                    skater.ShorthandedGoals)));
            Assert.All(side.Skaters, skater => Assert.Equal(skater.Goals + skater.Assists, skater.Points));
            var shootoutGoal = result.Decision == MatchDecision.Shootout && result.WinnerId == snapshot.TeamId ? 1 : 0;
            Assert.Equal(side.Score - shootoutGoal, side.Skaters.Sum(skater => skater.Goals));
            Assert.Equal(side.Goalie.ShotsAgainst - side.Goalie.GoalsAgainst, side.Goalie.Saves);
        }

        // Formatted figures are pinned to the literal text for a known skater and goalie.
        var skater = detail.Home.Skaters[0];
        Assert.Equal("Cole Thompson", skater.Name);
        Assert.Equal(
            ["0", "18:02", "0.66", "—", "4", "8", "2", "2"],
            [skater.PlusMinus, skater.TimeOnIce, skater.ExpectedGoals, skater.Faceoffs, skater.Shots.ToString(CultureInfo.InvariantCulture), skater.ShotAttempts.ToString(CultureInfo.InvariantCulture), skater.Hits.ToString(CultureInfo.InvariantCulture), skater.BlockedShots.ToString(CultureInfo.InvariantCulture)]);
        Assert.Equal(["1.97", "59:32", ".800"], [detail.Home.Goalie.ExpectedGoalsAgainst, detail.Home.Goalie.TimeOnIce, detail.Home.Goalie.SavePercentage]);

        // The summary's totals are the match's, away first as on the scoreboard.
        var byPeriod = detail.PeriodTables.ToDictionary(table => table.Title);
        Assert.Equal(
            [result.Away.Score.ToString(CultureInfo.CurrentCulture), result.Home.Score.ToString(CultureInfo.CurrentCulture)],
            byPeriod["GOALS"].Rows.Select(row => row.Total));
        Assert.Equal(
            [result.Away.Shots.ToString(CultureInfo.CurrentCulture), result.Home.Shots.ToString(CultureInfo.CurrentCulture)],
            byPeriod["SHOTS"].Rows.Select(row => row.Total));
        Assert.Equal(
            [
                MatchDisplay.PowerPlay(result.Away.PowerPlayGoals, result.Away.PowerPlayOpportunities),
                MatchDisplay.PowerPlay(result.Home.PowerPlayGoals, result.Home.PowerPlayOpportunities),
            ],
            byPeriod["POWER PLAY"].Rows.Select(row => row.Total));
        var penaltyMinutes = detail.Comparison.Single(row => row.Label == "Penalty minutes");
        Assert.Equal(
            (result.Away.PenaltyMinutes.ToString(CultureInfo.CurrentCulture), result.Home.PenaltyMinutes.ToString(CultureInfo.CurrentCulture)),
            (penaltyMinutes.AwayValue, penaltyMinutes.HomeValue));
        Assert.Equal(3, detail.ThreeStars.Count);
        Assert.Equal(session.GetTeam(result.Home.TeamId).Arena, detail.Arena);
    }

    [Fact]
    public void ScheduleListsTheSelectedTeamsSeasonAndExplainsUnplayedMatches()
    {
        var session = GameTestData.StartSession();
        var schedule = new GameShellViewModel(session).Schedule;
        var regularSeason = schedule.Matches.Where(match => match.Phase == SeasonPhase.RegularSeason).ToList();

        Assert.Equal(84, regularSeason.Count);
        Assert.Equal(42, regularSeason.Count(match => match.Venue == "vs"));
        Assert.All(regularSeason, match => Assert.Equal("REG", match.PhaseLabel));
        Assert.True(regularSeason[0].IsNext);
        Assert.All(regularSeason, match => Assert.False(match.IsCompleted));
        Assert.DoesNotContain(schedule.Matches, match => match.Phase == SeasonPhase.Playoffs);

        schedule.SelectedMatch = regularSeason[3];

        Assert.Null(schedule.SelectedResult);
        Assert.EndsWith(MatchDisplay.LongDate(schedule.SelectedMatch!.Date), schedule.SelectionHint, StringComparison.Ordinal);

        var otherTeam = schedule.Teams.First(team => !team.IsManaged);
        schedule.SelectedTeam = otherTeam;

        Assert.Null(schedule.SelectedMatch);
        Assert.Equal(91, schedule.Matches.Count);
        Assert.All(schedule.Matches, match => Assert.NotEqual(otherTeam.Name, match.Opponent));
        Assert.StartsWith(otherTeam.Name, schedule.Subtitle, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePreseasonIsListedFirstOnTheScheduleAndItsResultsOpen()
    {
        var session = GameTestData.StartPreseasonSession();
        var shell = new GameShellViewModel(session);
        var schedule = shell.Schedule;

        Assert.Equal(91, schedule.Matches.Count);
        Assert.All(schedule.Matches.Take(7), match =>
        {
            Assert.Equal(SeasonPhase.Preseason, match.Phase);
            Assert.Equal("PRE", match.PhaseLabel);
        });
        Assert.All(schedule.Matches.Skip(7), match => Assert.Equal(SeasonPhase.RegularSeason, match.Phase));
        Assert.True(schedule.Matches[0].IsNext);
        Assert.Equal($"{session.ManagedTeam.Name} · 0 of 84 regular-season matches played", schedule.Subtitle);

        await shell.AdvanceDayCommand.ExecuteAsync(null);

        var played = schedule.Matches[0];
        Assert.True(played.IsCompleted);
        Assert.True(schedule.Matches[1].IsNext);
        var managedResult = shell.Home.RecentResults.Rows[0];
        managedResult.OpenCommand.Execute(null);

        Assert.Equal(ShellPage.TeamSchedule, shell.CurrentPageKind);
        Assert.Same(schedule.Matches[0], schedule.SelectedMatch);
        Assert.NotNull(schedule.SelectedResult);
        Assert.Equal(["PRE", "PRE", "PRE", "PRE", "PRE", "PRE", string.Empty], shell.Home.UpcomingGames.Rows.Select(row => row.Phase));
    }

    [Fact]
    public async Task AFailedDayIsReportedAndNothingIsApplied()
    {
        var session = GameTestData.StartPreseasonSession(new GameManager(new FailingMatchSimulator()));
        var shell = new GameShellViewModel(session);
        var before = session.Snapshot;

        await shell.AdvanceDayCommand.ExecuteAsync(null);

        Assert.True(shell.HasAdvanceError);
        Assert.Contains("no results were applied", shell.AdvanceError, StringComparison.Ordinal);
        Assert.Contains(FailingMatchSimulator.FailureMessage, shell.AdvanceError, StringComparison.Ordinal);
        Assert.Equal(before.Season.CurrentDate, session.Snapshot.Season.CurrentDate);
        Assert.Equal(before.RandomState, session.Snapshot.RandomState);
        Assert.Empty(session.Snapshot.Season.Results);
        Assert.Empty(shell.Home.RecentResults.Rows);
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
        var session = GameTestData.StartPreseasonSession(new GameManager(engine));
        var shell = new GameShellViewModel(session);
        var firstDay = session.Snapshot.Season.CurrentDate;

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
        Assert.Equal(16, session.Snapshot.Season.PreseasonResults.Count);
        Assert.Equal(firstDay.AddDays(1), session.Snapshot.Season.CurrentDate);
        Assert.True(shell.AdvanceDayCommand.CanExecute(null));
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