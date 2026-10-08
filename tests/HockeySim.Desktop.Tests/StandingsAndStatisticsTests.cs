using System.Globalization;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.Standings;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class StandingsAndStatisticsTests
{
    [Fact]
    public void StandingsOpenOnTheDivisionsWithEveryTeamLevelBeforeAnyMatch()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());

        shell.Navigate(ShellPage.Standings);

        var page = Assert.IsType<StandingsPageViewModel>(shell.CurrentPage);
        Assert.True(page.IsDivisionScope);
        Assert.Equal("2026–27 regular season · no matches played yet", page.Subtitle);
        Assert.Equal(4, page.Tables.Count);
        Assert.All(page.Tables, table => Assert.Equal(8, table.Rows.Count));
        Assert.Contains(page.Tables, table => table.Title == "NORTHERN DIVISION" && table.Caption == "Eastern Conference");

        var rows = page.Tables.SelectMany(table => table.Rows).ToList();
        Assert.Equal(32, rows.Select(row => row.TeamName).Distinct().Count());
        Assert.Single(rows, row => row.IsManaged && row.TeamName == GameTestData.ManagedTeamName);
        Assert.All(rows, row =>
        {
            // Teams level on every criterion share a rank, and no percentage exists before a game.
            Assert.Equal(1, row.Rank);
            Assert.Equal(0, row.GamesPlayed);
            Assert.Equal(0, row.Points);
            Assert.Equal("—", row.PointsPercentage);
            Assert.Equal("0", row.GoalDifferential);
        });
    }

    [Fact]
    public async Task EachScopePresentsManagementsRankedTables()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        await shell.AdvanceDayCommand.ExecuteAsync(null);
        var page = shell.Standings;
        var standings = session.Snapshot.Season.Standings;

        AssertTables(
            session,
            standings.Conferences.SelectMany(conference => conference.Divisions.Select(division => (division.Name, division.Teams))),
            page.Tables);

        page.SelectScopeCommand.Execute(StandingsScope.Conference);

        Assert.True(page.IsConferenceScope);
        Assert.False(page.IsDivisionScope);
        AssertTables(session, standings.Conferences.Select(conference => (conference.Name, conference.Teams)), page.Tables);

        page.SelectScopeCommand.Execute(StandingsScope.League);

        Assert.True(page.IsLeagueScope);
        AssertTables(session, [("League", standings.League)], page.Tables);
    }

    [Fact]
    public async Task TheWildCardScopeShowsEachDivisionsTopThreeThenEachConferencesRace()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        await shell.AdvanceDayCommand.ExecuteAsync(null);
        var page = shell.Standings;

        page.SelectScopeCommand.Execute(StandingsScope.WildCard);

        Assert.True(page.IsWildCardScope);
        Assert.False(page.IsDivisionScope);
        var expected = session.Snapshot.Season.Standings.WildCard
            .SelectMany(conference => conference.DivisionLeaders
                .Select(division => (division.Name, division.Teams))
                .Append(($"{conference.Name} wild card", conference.WildCardRace)));
        AssertTables(session, expected, page.Tables);
        Assert.Equal(
            ["Eastern Conference", "Eastern Conference", "Top 2 qualify", "Western Conference", "Western Conference", "Top 2 qualify"],
            page.Tables.Select(table => table.Caption));

        // Only the second wild card is drawn with the playoff line beneath it.
        Assert.All(page.Tables, table => Assert.Equal(
            table.Title.EndsWith("WILD CARD", StringComparison.Ordinal) ? [1] : [],
            table.Rows.Select((row, index) => (row, index)).Where(pair => pair.row.IsLastQualifier).Select(pair => pair.index)));
        Assert.All(page.Tables.SelectMany(table => table.Rows), row =>
        {
            Assert.Equal("", row.PlayoffMarker);
            Assert.Null(row.PlayoffStatus);
        });
    }

    [Theory]
    [InlineData(PlayoffStatus.Undecided, "", null)]
    [InlineData(PlayoffStatus.Eliminated, "e", "Eliminated from playoff contention")]
    [InlineData(PlayoffStatus.ClinchedPlayoffSpot, "x", "Clinched a playoff spot")]
    [InlineData(PlayoffStatus.ClinchedDivision, "y", "Clinched the division")]
    [InlineData(PlayoffStatus.ClinchedConference, "z", "Clinched the conference")]
    [InlineData(PlayoffStatus.ClinchedBestRecord, "p", "Clinched the best record in the league")]
    public void EachPlayoffStatusShowsTheNhlLetterWithItsMeaning(PlayoffStatus status, string marker, string? meaning)
    {
        var entry = new StandingsEntrySnapshot(1, new TeamRecordSnapshot(new TeamId(Guid.NewGuid()), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), status);

        var row = StandingsRowViewModel.Create(entry, "Team", isManaged: false);

        Assert.Equal((marker, meaning), (row.PlayoffMarker, row.PlayoffStatus));
        if (meaning is not null)
        {
            Assert.Contains($"{marker} {meaning}", PlayoffStatusDisplay.Legend, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task StandingsRefreshAfterADayAndKeepTheChosenScope()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        shell.Standings.SelectScopeCommand.Execute(StandingsScope.League);

        await shell.AdvanceDayCommand.ExecuteAsync(null);

        var page = shell.Standings;
        Assert.True(page.IsLeagueScope);
        Assert.Equal(
            string.Create(CultureInfo.CurrentCulture, $"2026–27 regular season · {16:N0} of {1344:N0} matches played"),
            page.Subtitle);

        var rows = Assert.Single(page.Tables).Rows;
        Assert.All(rows.Zip(session.Snapshot.Season.Standings.League), pair =>
        {
            var (row, entry) = pair;
            var record = entry.Record;
            Assert.Equal(1, row.GamesPlayed);
            Assert.Equal(record.RegulationLosses, row.Losses);
            Assert.Equal(record.OvertimeLosses + record.ShootoutLosses, row.OvertimeLosses);
            Assert.Equal(record.RegulationAndOvertimeWins, row.RegulationAndOvertimeWins);
            Assert.Equal((record.Points / 2.0).ToString(".000", CultureInfo.CurrentCulture), row.PointsPercentage);
            Assert.Equal(
                record.GoalDifferential > 0 ? $"+{record.GoalDifferential}" : record.GoalDifferential.ToString(CultureInfo.CurrentCulture),
                row.GoalDifferential);
        });

        // Every match has a winner and a loser, so both signs appear after a full day.
        Assert.Contains(rows, row => row.GoalDifferential.StartsWith('+'));
        Assert.Contains(rows, row => row.GoalDifferential.StartsWith('-'));
    }

    [Fact]
    public void PlayersWithoutAppearancesShowZeroTotalsAndNoSavePercentage()
    {
        var session = GameTestData.StartSession();
        var roster = new TeamRosterViewModel(session, session.ManagedTeam.Id);

        Assert.True(roster.ShowsRatings);
        Assert.All(roster.Skaters.Concat(roster.Goalies), row => Assert.Equal(PlayerSeasonTotals.None, row.Season));
        Assert.All(roster.Goalies, row => Assert.Equal("—", row.Season.SavePercentage));

        roster.SelectPlayer(roster.Skaters[0].Player.Id);
        var skater = roster.SelectedPlayer!;
        Assert.Equal("2026–27 REGULAR SEASON", skater.SeasonTitle);
        Assert.Equal(
            ["SCORING", "SPECIAL TEAMS", "SHOOTING AND PLAY", "FACEOFFS AND ICE TIME", "5-ON-5 ON ICE"],
            skater.SeasonStatisticGroups.Select(group => group.Name));
        Assert.Equal(["GP", "G", "A", "P", "+/-", "PIM", "ENG"], skater.SeasonStatisticGroups[0].Statistics.Select(stat => stat.Label));
        var skaterLine = skater.SeasonStatisticGroups.SelectMany(group => group.Statistics).ToDictionary(stat => stat.Label, stat => stat.Value);
        Assert.All(["FO%", "TOI/GP", "CF%", "FF%", "SF%", "GF%", "xGF%"], label => Assert.Equal("—", skaterLine[label]));
        Assert.All(["GP", "G", "A", "P", "+/-", "PPP", "CF", "CA"], label => Assert.Equal("0", skaterLine[label]));
        Assert.Equal("No appearances yet this season.", skater.SeasonCaption);

        roster.SelectPlayer(roster.Goalies[0].Player.Id);
        var goalie = roster.SelectedPlayer!;
        Assert.Equal(["GP", "SA", "SV", "GA", "SV%", "GAA", "SO"], goalie.SeasonStatisticGroups[0].Statistics.Select(stat => stat.Label));
        Assert.Equal(["0", "0", "0", "0", "—", "—", "0"], goalie.SeasonStatisticGroups[0].Statistics.Select(stat => stat.Value));
        Assert.Equal(["0.00", "0.00", "0:00", "—"], goalie.SeasonStatisticGroups[1].Statistics.Select(stat => stat.Value));
        Assert.Equal("No starts yet this season.", goalie.SeasonCaption);
    }

    [Fact]
    public async Task RostersShowEveryTeamsCurrentSeasonTotals()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        await shell.AdvanceDayCommand.ExecuteAsync(null);
        var other = session.Snapshot.League.Teams.First(team => team.Id != session.Snapshot.ManagedTeamId);
        var season = session.Snapshot.Season;

        shell.Teams.SelectTeam(other.Id);
        var roster = shell.Teams.Roster;

        Assert.All(roster.Skaters, row =>
        {
            var statistics = season.SkaterStatistics.SingleOrDefault(skater => skater.PlayerId == row.Player.Id);
            Assert.Equal(statistics?.GamesPlayed ?? 0, row.Season.GamesPlayed);
            Assert.Equal(statistics?.Goals ?? 0, row.Season.Goals);
            Assert.Equal(statistics?.Assists ?? 0, row.Season.Assists);
            Assert.Equal(row.Season.Goals + row.Season.Assists, row.Season.Points);
            Assert.Equal(row.IsScratched ? 0 : 1, row.Season.GamesPlayed);
        });

        var starter = roster.Goalies.Single(row => row.Player.Id == other.Lineup.StartingGoalieId);
        var starterStatistics = season.GoalieStatistics.Single(goalie => goalie.PlayerId == starter.Player.Id);
        Assert.Equal(1, starter.Season.GamesPlayed);
        Assert.Equal(starterStatistics.ShotsAgainst, starter.Season.ShotsAgainst);
        Assert.Equal(starterStatistics.Saves, starter.Season.Saves);
        Assert.Equal(starterStatistics.GoalsAgainst, starter.Season.GoalsAgainst);
        Assert.Equal(MatchDisplay.SavePercentage(starterStatistics.Saves, starterStatistics.ShotsAgainst), starter.Season.SavePercentage);
        Assert.All(
            roster.Goalies.Where(row => row != starter),
            row => Assert.Equal("—", row.Season.SavePercentage));

        roster.SelectPlayer(starter.Player.Id);
        var detail = roster.SelectedPlayer!;
        Assert.Equal(other.Name, detail.TeamName);
        Assert.Equal(starter.Season.SavePercentage, detail.SeasonStatisticGroups.SelectMany(group => group.Statistics).Single(stat => stat.Label == "SV%").Value);
        Assert.False(detail.HasSeasonCaption);
    }

    [Fact]
    public async Task StatisticColumnsStayChosenAcrossTeamsAndDays()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        shell.Teams.Roster.ShowColumnsCommand.Execute(RosterColumns.Advanced);
        shell.Roster.Roster.ShowColumnsCommand.Execute(RosterColumns.Basic);

        Assert.True(shell.Teams.Roster.ShowsAdvanced);
        Assert.False(shell.Teams.Roster.ShowsBasic);
        Assert.False(shell.Teams.Roster.ShowsRatings);

        shell.Teams.ShowNextTeamCommand.Execute(null);
        Assert.True(shell.Teams.Roster.ShowsAdvanced);

        await shell.AdvanceDayCommand.ExecuteAsync(null);
        Assert.True(shell.Teams.Roster.ShowsAdvanced);
        Assert.True(shell.Roster.Roster.ShowsBasic);
        Assert.Contains(shell.Roster.Roster.Skaters, row => row.Season.GamesPlayed == 1);
    }

    [Fact]
    public async Task ReplacingTheSnapshotRedrawsStandingsAndTotals()
    {
        // A loaded save will publish a replacement snapshot through the session. Restoring the
        // opening-day snapshot stands in for loading an earlier save of the same game.
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        var openingDay = session.Snapshot;
        await shell.AdvanceDayCommand.ExecuteAsync(null);
        Assert.All(shell.Standings.Tables.SelectMany(table => table.Rows), row => Assert.Equal(1, row.GamesPlayed));

        session.Snapshot = openingDay;

        Assert.Equal("2026–27 regular season · no matches played yet", shell.Standings.Subtitle);
        Assert.All(shell.Standings.Tables.SelectMany(table => table.Rows), row => Assert.Equal(0, row.GamesPlayed));
        Assert.All(shell.Roster.Roster.Skaters, row => Assert.Equal(PlayerSeasonTotals.None, row.Season));
        Assert.All(shell.Teams.Roster.Goalies, row => Assert.Equal("—", row.Season.SavePercentage));
        Assert.Equal("No appearances yet this season.", shell.Roster.Roster.SelectedPlayer?.SeasonCaption);
    }

    private static void AssertTables(
        GameSession session,
        IEnumerable<(string Name, IReadOnlyList<StandingsEntrySnapshot> Entries)> expected,
        IReadOnlyList<StandingsTableViewModel> tables)
    {
        var expectedTables = expected.ToList();
        Assert.Equal(expectedTables.Select(table => table.Name.ToUpperInvariant()), tables.Select(table => table.Title));
        Assert.All(expectedTables.Zip(tables), pair =>
        {
            var (entries, table) = (pair.First.Entries, pair.Second);
            Assert.Equal(entries.Select(entry => entry.Rank), table.Rows.Select(row => row.Rank));
            Assert.Equal(entries.Select(entry => session.GetTeam(entry.Record.TeamId).Name), table.Rows.Select(row => row.TeamName));
            Assert.Equal(entries.Select(entry => entry.Record.Points), table.Rows.Select(row => row.Points));
            Assert.Equal(
                entries.Select(entry => entry.Record.TeamId == session.Snapshot.ManagedTeamId),
                table.Rows.Select(row => row.IsManaged));
        });
    }
}