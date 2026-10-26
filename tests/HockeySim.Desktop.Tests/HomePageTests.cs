using System.Globalization;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Home;
using HockeySim.Desktop.Standings;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;

using Xunit;

namespace HockeySim.Desktop.Tests;

/// <summary>
/// Shares the one played season between the playoff and home page tests, so it is played once.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PlayedSeasonCollection : ICollectionFixture<PlayoffDisplayTests.PlayedSeason>
{
    public const string Name = "Played season";
}

/// <summary>
/// The home page's panels against real games. Expected figures are worked out from the snapshot's
/// raw numbers, formatted independently of the display code, or written as literals.
/// </summary>
[Collection(PlayedSeasonCollection.Name)]
public sealed class HomePageTests(PlayoffDisplayTests.PlayedSeason playedSeason)
{
    private static readonly string[] RegularSeasonLabels =
    [
        "Points %", "Goals for / game", "Goals against / game", "Power play", "Penalty kill", "5v5 xGF%", "5v5 CF%", "Team save %", "Last 10",
    ];

    [Fact]
    public void OnOpeningDayNothingIsKnownSoTheTapeIsEvenWithDashes()
    {
        var session = GameTestData.StartSession();
        var tape = new GameShellViewModel(session).Home.TaleOfTheTape;

        Assert.True(tape.HasMatch);
        Assert.StartsWith("Today · ", tape.Caption, StringComparison.Ordinal);
        Assert.Equal(GameTestData.ManagedTeamName, tape.Ours!.Name);
        Assert.Equal(["0-0-0", "0-0-0"], [tape.Ours.Record, tape.Theirs!.Record]);
        Assert.Equal(["—", "—"], [tape.Ours.Standing, tape.Theirs.Standing]);
        Assert.Equal(RegularSeasonLabels, tape.Rows.Select(row => row.Label));
        Assert.All(tape.Rows, row =>
        {
            Assert.Equal(["—", "—"], [row.OursValue, row.TheirsValue]);
            Assert.Equal(0.5, row.OursShare);
            Assert.False(row.OursBetter);
            Assert.False(row.TheirsBetter);
        });
        Assert.Equal("Season series 0-0-0 ›", tape.ContextLinkText);
        Assert.Equal($"{tape.Theirs.Name} ›", tape.OpponentLinkText);
        Assert.All(tape.OurPlayers.Concat(tape.TheirPlayers).Where(player => player.Label == "Top scorer"), player =>
        {
            Assert.Equal("—", player.Name);
            Assert.Null(player.OpenCommand);
        });
        Assert.All(tape.OurPlayers.Concat(tape.TheirPlayers).Where(player => player.Label == "Goalie"), player =>
        {
            Assert.Equal("— · —", player.Line);
            Assert.NotNull(player.OpenCommand);
        });
    }

    [Fact]
    public async Task MidSeasonTheTapeComparesTheTeamsFigureByFigure()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        await PlayDays(shell, 50);
        var snapshot = shell.Session.Snapshot;
        var season = snapshot.Season;
        var tape = shell.Home.TaleOfTheTape;
        var managed = snapshot.ManagedTeamId;
        var next = snapshot.Schedule.Matches.First(match => match.Date >= season.CurrentDate && Involves(match, managed));
        var opponent = next.HomeTeamId == managed ? next.AwayTeamId : next.HomeTeamId;

        Assert.Equal(RegularSeasonLabels, tape.Rows.Select(row => row.Label));
        Assert.Equal(shell.Session.GetTeam(opponent).Name, tape.Theirs!.Name);
        Assert.Equal(Record(season, managed), tape.Ours!.Record);
        Assert.Equal(Record(season, opponent), tape.Theirs.Record);
        Assert.Matches(@"^\d(st|nd|rd|th) · \w+", tape.Ours.Standing);
        Assert.Equal(next.Date == season.CurrentDate, tape.Caption.StartsWith("Today", StringComparison.Ordinal));
        Assert.EndsWith(next.HomeTeamId == managed ? "Home" : "Away", tape.Caption, StringComparison.Ordinal);

        var (ourRecord, theirRecord) = (RecordOf(season, managed), RecordOf(season, opponent));
        var (ourStats, theirStats) = (StatsOf(season, managed), StatsOf(season, opponent));
        Row(tape, "Points %", ourRecord.Points / (2.0 * ourRecord.GamesPlayed), theirRecord.Points / (2.0 * theirRecord.GamesPlayed), false, Fixed3);
        Row(tape, "Goals for / game", ourRecord.GoalsFor / (double)ourRecord.GamesPlayed, theirRecord.GoalsFor / (double)theirRecord.GamesPlayed, false, Fixed2);
        Row(tape, "Goals against / game", ourRecord.GoalsAgainst / (double)ourRecord.GamesPlayed, theirRecord.GoalsAgainst / (double)theirRecord.GamesPlayed, true, Fixed2);
        Row(tape, "Power play", ourStats.PowerPlayPercentage, theirStats.PowerPlayPercentage, false, OneDecimalPercent);
        Row(tape, "Penalty kill", ourStats.PenaltyKillPercentage, theirStats.PenaltyKillPercentage, false, OneDecimalPercent);
        Row(tape, "5v5 xGF%", ourStats.ShotTotals.FiveOnFive.ExpectedGoalsPercentage, theirStats.ShotTotals.FiveOnFive.ExpectedGoalsPercentage, false, OneDecimalPercent);
        Row(tape, "5v5 CF%", ourStats.ShotTotals.FiveOnFive.CorsiPercentage, theirStats.ShotTotals.FiveOnFive.CorsiPercentage, false, OneDecimalPercent);
        Row(tape, "Team save %", SavePercentage(season, managed), SavePercentage(season, opponent), false, Fixed3);

        // The last ten games are compared by the standings points they earned.
        var (ourForm, theirForm) = (LastTen(season, managed), LastTen(season, opponent));
        var lastTen = tape.Rows.Single(row => row.Label == "Last 10");
        Assert.Equal(ourForm.Text, lastTen.OursValue);
        Assert.Equal(theirForm.Text, lastTen.TheirsValue);
        Assert.Equal(ourForm.Points > theirForm.Points, lastTen.OursBetter);
        Assert.Equal(ourForm.Points < theirForm.Points, lastTen.TheirsBetter);
        if (ourForm.Points != theirForm.Points)
        {
            Assert.Equal(ourForm.Points / (double)(ourForm.Points + theirForm.Points), lastTen.OursShare, 10);
        }

        var meetings = season.Results.Where(result => Involves(result, managed) && Involves(result, opponent)).ToList();
        var won = meetings.Count(result => result.WinnerId == managed);
        var lostInRegulation = meetings.Count(result => result.WinnerId != managed && result.Decision == MatchDecision.Regulation);
        Assert.Equal($"Season series {won}-{lostInRegulation}-{meetings.Count - won - lostInRegulation} ›", tape.ContextLinkText);

        // Each side shows its top scorer and the goalie that starts today.
        var scorer = season.SkaterStatistics.Where(skater => skater.TeamId == managed)
            .OrderByDescending(skater => skater.Points).ThenByDescending(skater => skater.Goals).First();
        Assert.Equal($"{scorer.Goals} G · {scorer.Assists} A · {scorer.Points} P", tape.OurPlayers[0].Line);
        Assert.Equal(ShortName(shell.Session.ManagedTeam, scorer.PlayerId), tape.OurPlayers[0].Name);
        var starter = shell.Session.ManagedTeam.Lineup.StartingGoalieId;
        Assert.Equal(ShortName(shell.Session.ManagedTeam, starter), tape.OurPlayers[1].Name);
        var goalie = season.GoalieStatistics.SingleOrDefault(entry => entry.PlayerId == starter);
        Assert.Equal(
            goalie is null
                ? "— · —"
                : $"{Fixed3(goalie.ShotsAgainst == 0 ? null : (goalie.ShotsAgainst - goalie.GoalsAgainst) / (double)goalie.ShotsAgainst)} · {goalie.GoalsAgainstAverage!.Value.ToString("0.00", CultureInfo.CurrentCulture)} GAA",
            tape.OurPlayers[1].Line);
    }

    [Fact]
    public void ACompletedSeasonHasNoMatchToCompare()
    {
        var home = playedSeason.Shell.Home;

        Assert.False(home.TaleOfTheTape.HasMatch);
        Assert.Equal("Season complete", home.TaleOfTheTape.EmptyText);
        Assert.Equal("Season complete", home.UpcomingGames.EmptyText);
        Assert.False(home.UpcomingGames.HasRows);
        Assert.Equal(6, home.RecentResults.Rows.Count);
    }

    [Fact]
    public void InThePlayoffsTheTapeSwitchesToPlayoffFigures()
    {
        var session = playedSeason.Load(playedSeason.QualifierInPlayoffs);
        var shell = new GameShellViewModel(session);
        var snapshot = session.Snapshot;
        var playoffs = snapshot.Season.Playoffs!;
        var managed = snapshot.ManagedTeamId;
        var series = playoffs.Series.Last(candidate => candidate.HigherRanked.TeamId == managed || candidate.LowerRanked.TeamId == managed);
        var tape = shell.Home.TaleOfTheTape;

        Assert.Equal(
            ["Goals for / game", "Goals against / game", "Power play", "Penalty kill", "5v5 xGF%", "5v5 CF%", "Team save %"],
            tape.Rows.Select(row => row.Label));
        var ourSeed = series.HigherRanked.TeamId == managed ? series.HigherRanked : series.LowerRanked;
        var theirSeed = series.HigherRanked.TeamId == managed ? series.LowerRanked : series.HigherRanked;
        Assert.Equal(Seed(ourSeed), tape.Ours!.Standing);
        Assert.Equal(Seed(theirSeed), tape.Theirs!.Standing);
        var (ourPlayoffs, theirPlayoffs) = (playoffs.TeamRecords.Single(record => record.TeamId == managed), playoffs.TeamRecords.Single(record => record.TeamId == theirSeed.TeamId));
        Assert.Equal($"{ourPlayoffs.Wins}-{ourPlayoffs.Losses}", tape.Ours.Record);
        Assert.Equal($"{theirPlayoffs.Wins}-{theirPlayoffs.Losses}", tape.Theirs.Record);
        Assert.Contains($"game {series.Games.Count + 1}", tape.Caption, StringComparison.Ordinal);
        Assert.EndsWith(" ›", tape.ContextLinkText, StringComparison.Ordinal);
        Assert.DoesNotContain("Season series", tape.ContextLinkText, StringComparison.Ordinal);

        var goalsFor = tape.Rows.Single(row => row.Label == "Goals for / game");
        Assert.Equal(Fixed2(ourPlayoffs.GoalsFor / (double)ourPlayoffs.GamesPlayed), goalsFor.OursValue);

        tape.OpenContextCommand.Execute(null);
        Assert.Equal(ShellPage.PlayoffPicture, shell.CurrentPageKind);

        // The scorer is the team's top playoff scorer, not its regular-season one.
        var scorer = playoffs.SkaterStatistics.Where(skater => skater.TeamId == managed)
            .OrderByDescending(skater => skater.Points).ThenByDescending(skater => skater.Goals).First();
        Assert.Equal($"{scorer.Goals} G · {scorer.Assists} A · {scorer.Points} P", tape.OurPlayers[0].Line);
        var points = shell.Home.TeamLeaders.Tiles.Single(tile => tile.Label == "Points");
        Assert.Equal(scorer.Points.ToString(CultureInfo.CurrentCulture), points.Value);
    }

    [Fact]
    public async Task RateLeadersNeedAShareOfTheTeamsGamesAndTheChoiceHolds()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        var leaders = shell.Home.TeamLeaders;
        Assert.Equal("50%", leaders.MinimumGames.Label);
        Assert.Equal(["Any", "25%", "50%", "75%"], leaders.MinimumGamesOptions.Select(option => option.Label));

        // Play until a skater with too few games has a better expected-goal share than any who qualify.
        (SkaterSeasonStatisticsSnapshot Any, SkaterSeasonStatisticsSnapshot Qualified)? found = null;
        for (var day = 0; day < 80 && found is null; day++)
        {
            await PlayDays(shell, 1);
            found = FindLowGamesLeader(shell.Session);
        }

        Assert.NotNull(found);
        var team = shell.Session.ManagedTeam;
        static string Xgf(SkaterSeasonStatisticsSnapshot skater) => OneDecimalPercent(skater.OnIce.FiveOnFive.ExpectedGoalsPercentage);
        var tile = () => leaders.Tiles.Single(candidate => candidate.Label == "5v5 xGF%");

        Assert.Equal(ShortName(team, found.Value.Qualified.PlayerId), tile().PlayerName);
        Assert.Equal(Xgf(found.Value.Qualified), tile().Value);

        leaders.MinimumGames = leaders.MinimumGamesOptions[0];
        Assert.Equal(ShortName(team, found.Value.Any.PlayerId), tile().PlayerName);
        Assert.Equal(Xgf(found.Value.Any), tile().Value);

        // The choice holds across a refresh and another day.
        shell.Home.Refresh();
        Assert.Equal("Any", leaders.MinimumGames.Label);
        await PlayDays(shell, 1);
        Assert.Equal("Any", leaders.MinimumGames.Label);

        // A counting statistic has no minimum.
        var season = shell.Session.Snapshot.Season;
        var topPoints = season.SkaterStatistics.Where(skater => skater.TeamId == team.Id).Max(skater => skater.Points);
        Assert.Equal(topPoints.ToString(CultureInfo.CurrentCulture), leaders.Tiles.Single(candidate => candidate.Label == "Points").Value);
        Assert.Equal(["Points", "Goals", "Assists", "5v5 xGF%", "Save %", "GSAx"], leaders.Tiles.Select(candidate => candidate.Label));
    }

    [Fact]
    public void BeforeAnyoneHasPlayedEveryLeaderIsADash()
    {
        var leaders = new GameShellViewModel(GameTestData.StartSession()).Home.TeamLeaders;

        Assert.All(leaders.Tiles, tile =>
        {
            Assert.Equal("—", tile.Value);
            Assert.Equal(string.Empty, tile.PlayerName);
            Assert.Null(tile.OpenPlayerCommand);
        });
    }

    [Fact]
    public async Task TheStandingsPanelShowsEachScopeAndKeepsItAcrossRefreshes()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        var panel = shell.Home.Standings;
        var standings = shell.Session.Snapshot.Season.Standings;
        var (conference, division) = shell.Session.FindDivision(shell.Session.ManagedTeam);

        Assert.True(panel.IsDivisionScope);
        Assert.Equal("NORTHERN DIVISION", panel.Title);
        Assert.Equal(
            standings.Conferences.Single(entry => entry.Name == conference.Name).Divisions.Single(entry => entry.Name == division.Name).Teams.Select(entry => entry.Record.TeamId),
            Assert.Single(panel.Tables).Rows.Select(row => row.TeamId));

        panel.SelectScopeCommand.Execute(StandingsScope.Conference);
        Assert.Equal(conference.Name.ToUpperInvariant(), panel.Title);
        var conferenceRows = Assert.Single(panel.Tables).Rows;
        Assert.Equal(16, conferenceRows.Count);
        Assert.Equal(
            standings.Conferences.Single(entry => entry.Name == conference.Name).Teams.Select(entry => entry.Record.TeamId),
            conferenceRows.Select(row => row.TeamId));

        panel.SelectScopeCommand.Execute(StandingsScope.League);
        Assert.Equal("LEAGUE", panel.Title);
        var leagueRows = Assert.Single(panel.Tables).Rows;
        Assert.Equal(standings.League.Select(entry => entry.Record.TeamId), leagueRows.Select(row => row.TeamId));
        Assert.Single(leagueRows, row => row.IsManaged);

        panel.SelectScopeCommand.Execute(StandingsScope.WildCard);
        var wildCard = standings.WildCard.Single(entry => entry.Name == conference.Name);
        Assert.Equal($"{conference.Name.ToUpperInvariant()} WILD CARD", panel.Title);
        Assert.Equal(wildCard.DivisionLeaders.Count + 1, panel.Tables.Count);
        var race = panel.Tables[^1];
        Assert.False(race.ShowTitle);
        Assert.All(panel.Tables.Take(panel.Tables.Count - 1), table => Assert.True(table.ShowTitle));
        Assert.Equal(wildCard.WildCardRace.Select(entry => entry.Record.TeamId), race.Rows.Select(row => row.TeamId));
        Assert.Equal([wildCard.WildCardCount - 1], race.Rows.Select((row, index) => (row.IsLastQualifier, index)).Where(pair => pair.IsLastQualifier).Select(pair => pair.index));

        shell.Home.Refresh();
        Assert.True(panel.IsWildCardScope);
        await PlayDays(shell, 2);
        Assert.True(panel.IsWildCardScope);
        Assert.Equal($"{conference.Name.ToUpperInvariant()} WILD CARD", panel.Title);
    }

    [Fact]
    public void UpcomingGamesListTheNextSevenMatchesAndOpenTheSchedule()
    {
        var session = GameTestData.StartSession();
        var opened = new List<(DateOnly Date, TeamId Team)>();
        var pages = new List<ShellPage>();
        var panel = new UpcomingGamesPanelViewModel(session, pages.Add, (date, team) => opened.Add((date, team)));
        var managed = session.Snapshot.ManagedTeamId;
        var expected = session.Snapshot.Schedule.Matches.Where(match => Involves(match, managed)).Take(7).ToList();

        Assert.Equal(7, panel.Rows.Count);
        Assert.Equal(["Thu, Oct 1"], panel.Rows.Take(1).Select(row => row.Date));
        Assert.All(panel.Rows.Zip(expected), pair =>
        {
            var (row, match) = pair;
            var isHome = match.HomeTeamId == managed;
            Assert.Equal(isHome ? "vs" : "@", row.Venue);
            Assert.Equal(session.GetTeam(isHome ? match.AwayTeamId : match.HomeTeamId).Name, row.Opponent);
            Assert.Equal("0-0-0", row.Detail);
            Assert.Equal(string.Empty, row.Phase);
        });

        panel.Rows[2].OpenCommand.Execute(null);
        panel.OpenScheduleCommand.Execute(null);

        Assert.Equal([(expected[2].Date, managed)], opened);
        Assert.Equal([ShellPage.TeamSchedule], pages);
    }

    [Fact]
    public void PreseasonMatchesAreTaggedAndShowTheOpponentsRegularSeasonRecord()
    {
        var panel = new UpcomingGamesPanelViewModel(GameTestData.StartPreseasonSession(), _ => { }, (_, _) => { });

        Assert.Equal(7, panel.Rows.Count);
        Assert.All(panel.Rows, row =>
        {
            Assert.Equal("PRE", row.Phase);
            Assert.Equal("0-0-0", row.Detail);
        });
    }

    [Fact]
    public void InThePlayoffsUpcomingGamesShowTheRoundAndGame()
    {
        var session = playedSeason.Load(playedSeason.QualifierInPlayoffs);
        var panel = new UpcomingGamesPanelViewModel(session, _ => { }, (_, _) => { });

        var row = Assert.Single(panel.Rows);
        Assert.Matches(@"^(R1|R2|CF|F) G[1-7]$", row.Detail);
        Assert.Equal(string.Empty, row.Phase);
    }

    [Fact]
    public async Task RecentResultsListTheLastSixNewestFirstAndOpenTheirBoxScores()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        var opened = new List<(DateOnly Date, TeamId Team)>();
        var panel = new RecentResultsPanelViewModel(shell.Session, (date, team) => opened.Add((date, team)));
        await PlayDays(shell, 6);
        panel.Refresh();
        var managed = shell.Session.Snapshot.ManagedTeamId;
        var season = shell.Session.Snapshot.Season;
        var expected = season.PreseasonResults.Concat(season.Results).Where(result => Involves(result, managed)).TakeLast(6).Reverse().ToList();

        Assert.Equal(6, panel.Rows.Count);
        Assert.All(panel.Rows.Zip(expected), pair =>
        {
            var (row, result) = pair;
            var (ours, theirs) = result.Home.TeamId == managed ? (result.Home, result.Away) : (result.Away, result.Home);
            var suffix = result.Decision switch { MatchDecision.Overtime => " OT", MatchDecision.Shootout => " SO", _ => string.Empty };
            Assert.Equal($"{(ours.Score > theirs.Score ? "W" : "L")} {ours.Score}–{theirs.Score}{suffix}", row.Result);
            Assert.Equal(ours.Score > theirs.Score, row.IsWin);
            Assert.Equal(result.Home.TeamId == managed ? "vs" : "@", row.Venue);
            Assert.Equal(shell.Session.GetTeam(theirs.TeamId).Name, row.Opponent);
        });
        Assert.Equal(season.Results.Last(result => Involves(result, managed)).Date, expected[0].Date);

        panel.Rows[1].OpenCommand.Execute(null);
        Assert.Equal([(expected[1].Date, managed)], opened);
    }

    [Fact]
    public void WithoutResultsThePanelSaysSo()
    {
        var panel = new RecentResultsPanelViewModel(GameTestData.StartPreseasonSession(), (_, _) => { });

        Assert.Empty(panel.Rows);
        Assert.False(panel.HasRows);
        Assert.Equal("No results", panel.EmptyText);
    }

    private static async Task PlayDays(GameShellViewModel shell, int days)
    {
        for (var day = 0; day < days && !shell.Session.Snapshot.Season.IsComplete; day++)
        {
            if (shell.HasPlayersToReplace)
            {
                shell.Session.SetLineup(InjuredPlayerReplacement.ReplacingInjured(shell.Session.Snapshot));
            }

            await shell.AdvanceDayCommand.ExecuteAsync(null);
            Assert.Null(shell.AdvanceError);
        }
    }

    /// <summary>
    /// The managed team's best expected-goal share overall when it belongs to a skater below the
    /// half-of-games requirement, with the best of those who meet it.
    /// </summary>
    private static (SkaterSeasonStatisticsSnapshot Any, SkaterSeasonStatisticsSnapshot Qualified)? FindLowGamesLeader(GameSession session)
    {
        var managed = session.Snapshot.ManagedTeamId;
        var teamGames = session.Snapshot.Season.TeamRecords.Single(record => record.TeamId == managed).GamesPlayed;
        var required = Math.Max(1, (teamGames + 1) / 2);
        var roster = session.ManagedTeam.Roster.ToDictionary(player => player.Id);
        var ranked = session.Snapshot.Season.SkaterStatistics
            .Where(skater => skater.TeamId == managed && skater.OnIce.FiveOnFive.ExpectedGoalsPercentage is not null)
            .OrderByDescending(skater => skater.OnIce.FiveOnFive.ExpectedGoalsPercentage)
            .ThenByDescending(skater => skater.GamesPlayed)
            .ThenBy(skater => roster[skater.PlayerId].LastName, StringComparer.CurrentCulture)
            .ToList();
        var qualified = ranked.FirstOrDefault(skater => skater.GamesPlayed >= required);
        return ranked.Count > 0 && qualified is not null && ranked[0].GamesPlayed < required ? (ranked[0], qualified) : null;
    }

    private static void Row(TaleOfTheTapePanelViewModel tape, string label, double? ours, double? theirs, bool lowerIsBetter, Func<double?, string> format)
    {
        var row = tape.Rows.Single(candidate => candidate.Label == label);
        Assert.Equal(format(ours), row.OursValue);
        Assert.Equal(format(theirs), row.TheirsValue);
        if (ours is not { } mine || theirs is not { } other || mine == other)
        {
            Assert.Equal(0.5, row.OursShare);
            Assert.False(row.OursBetter);
            Assert.False(row.TheirsBetter);
            return;
        }

        var oursBetter = lowerIsBetter ? mine < other : mine > other;
        Assert.Equal(oursBetter, row.OursBetter);
        Assert.Equal(!oursBetter, row.TheirsBetter);
        Assert.Equal((lowerIsBetter ? other : mine) / (mine + other), row.OursShare, 10);
    }

    private static string Fixed3(double? value) => value is { } figure ? figure.ToString("0.000", CultureInfo.CurrentCulture).TrimStart('0') : "—";

    private static string Fixed2(double? value) => value is { } figure ? figure.ToString("0.00", CultureInfo.CurrentCulture) : "—";

    private static string OneDecimalPercent(double? share) => share is { } figure ? (figure * 100).ToString("0.0", CultureInfo.CurrentCulture) : "—";

    private static string Seed(PlayoffSeedSnapshot seed) => seed.IsWildCard ? $"WC{seed.Rank}" : $"{seed.DivisionName[0]}{seed.Rank}";

    private static string ShortName(TeamSnapshot team, PlayerId playerId)
    {
        var player = team.Roster.Single(candidate => candidate.Id == playerId);
        return $"{player.FirstName[0]}. {player.LastName}";
    }

    private static bool Involves(ScheduledMatchSnapshot match, TeamId teamId) => match.HomeTeamId == teamId || match.AwayTeamId == teamId;

    private static bool Involves(CompletedMatchSnapshot result, TeamId teamId) => result.Home.TeamId == teamId || result.Away.TeamId == teamId;

    private static TeamRecordSnapshot RecordOf(SeasonSnapshot season, TeamId teamId) => season.TeamRecords.Single(record => record.TeamId == teamId);

    private static TeamSeasonStatisticsSnapshot StatsOf(SeasonSnapshot season, TeamId teamId) => season.TeamStatistics.Single(entry => entry.TeamId == teamId);

    private static string Record(SeasonSnapshot season, TeamId teamId)
    {
        var record = RecordOf(season, teamId);
        return $"{record.Wins}-{record.RegulationLosses}-{record.OvertimeLosses + record.ShootoutLosses}";
    }

    private static double? SavePercentage(SeasonSnapshot season, TeamId teamId)
    {
        var goalies = season.GoalieStatistics.Where(goalie => goalie.TeamId == teamId).ToList();
        var shots = goalies.Sum(goalie => goalie.ShotsAgainst);
        return shots == 0 ? null : (shots - goalies.Sum(goalie => goalie.GoalsAgainst)) / (double)shots;
    }

    private static (string Text, int Points) LastTen(SeasonSnapshot season, TeamId teamId)
    {
        var games = season.Results.Where(result => Involves(result, teamId)).TakeLast(10).ToList();
        var wins = games.Count(result => result.WinnerId == teamId);
        var regulationLosses = games.Count(result => result.WinnerId != teamId && result.Decision == MatchDecision.Regulation);
        var overtimeLosses = games.Count - wins - regulationLosses;
        return ($"{wins}-{regulationLosses}-{overtimeLosses}", (2 * wins) + overtimeLosses);
    }
}