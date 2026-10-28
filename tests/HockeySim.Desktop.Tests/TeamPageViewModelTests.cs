using HockeySim.Desktop.Game;
using HockeySim.Desktop.Lines;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.Teams;
using HockeySim.Domain;

using Xunit;

namespace HockeySim.Desktop.Tests;

/// <summary>A team's page against real games: its banner, tabs, roster, lines, schedule, and statistics.</summary>
[Collection(PlayedSeasonCollection.Name)]
public sealed class TeamPageViewModelTests(PlayoffDisplayTests.PlayedSeason season)
{
    [Fact]
    public async Task TheBannerShowsAnotherTeamsIdentityRecordAndStandingInItsOwnColours()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        await shell.AdvanceDayCommand.ExecuteAsync(null);
        var result = session.Snapshot.Season.PreseasonResults.Concat(session.Snapshot.Season.Results).First(
            candidate => candidate.Home.TeamId != session.Snapshot.ManagedTeamId && candidate.Away.TeamId != session.Snapshot.ManagedTeamId);
        var team = session.GetTeam(result.Home.TeamId);

        shell.OpenTeam(team.Id);

        var page = shell.TeamPage!;
        var banner = Assert.IsType<TeamPageBannerViewModel>(shell.Banner);
        Assert.Equal(team.Name, banner.Team.TeamName);
        Assert.Equal(team.Colours.Primary, banner.Colours.Primary);
        Assert.NotEqual(session.ManagedTeam.Colours.Primary, banner.Colours.Primary);
        var (conference, division) = session.FindDivision(team);
        Assert.Equal($"{division.Name} · {conference.Name}", page.Subtitle);
        Assert.Contains(division.Name, banner.Team.Summary, StringComparison.Ordinal);
        Assert.Contains(conference.Name, banner.Team.Summary, StringComparison.Ordinal);
        var record = session.Snapshot.Season.Standings.Conferences.SelectMany(c => c.Divisions).SelectMany(d => d.Teams).Single(entry => entry.Record.TeamId == team.Id).Record;
        Assert.StartsWith($"{record.Wins}-{record.RegulationLosses}-{record.OvertimeLosses + record.ShootoutLosses} · {record.Points} pts", banner.Team.Summary, StringComparison.Ordinal);
        Assert.Equal(["NEXT", "LAST", "STREAK"], banner.Team.Figures.Select(figure => figure.Label));
        var last = ScheduleRows.AllResults(session.Snapshot).Last(candidate => TeamResults.Involves(candidate, team.Id));
        Assert.StartsWith(MatchDisplay.ResultFor(last, team.Id), banner.Team.Figures.Single(figure => figure.Label == "LAST").Value, StringComparison.Ordinal);
    }

    [Fact]
    public void TheManagedTeamsPageShowsTheSameFiguresAsTheShellBanner()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);

        shell.OpenTeam(session.Snapshot.ManagedTeamId);

        var banner = Assert.IsType<TeamPageBannerViewModel>(shell.Banner);
        Assert.Equal(shell.TeamBanner.Summary, banner.Team.Summary);
        Assert.Equal(shell.TeamBanner.Figures, banner.Team.Figures);
        Assert.Equal(ShellPage.TeamPage, shell.CurrentPageKind);
    }

    [Fact]
    public void TheTabsAreRosterLinesScheduleAndStatisticsStartingOnTheRoster()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        shell.OpenTeam(shell.Session.Snapshot.ManagedTeamId);
        var page = shell.TeamPage!;

        Assert.Same(page.Tabs, shell.PageTabs);
        Assert.Equal(["Roster", "Lines", "Schedule", "Statistics"], page.Tabs!.Select(tab => tab.Label));
        Assert.Equal([true, false, false, false], page.Tabs!.Select(tab => tab.IsActive));
        Assert.True(page.ShowsRoster);

        page.Tabs![2].Command.Execute(null);

        Assert.True(page.ShowsSchedule);
        Assert.Equal([false, false, true, false], page.Tabs!.Select(tab => tab.IsActive));
    }

    [Fact]
    public void TheRosterListsTheTeamsPlayersAndItsLinesLinkShowsTheLinesTab()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        var other = session.Snapshot.League.Teams.First(team => team.Id != session.Snapshot.ManagedTeamId);
        shell.OpenTeam(other.Id);
        var page = shell.TeamPage!;

        Assert.Equal(
            other.Roster.Select(player => player.Id.Value).Order(),
            page.Roster.Skaters.Concat(page.Roster.Goalies).Select(row => row.Player.Id.Value).Order());
        page.Roster.OpenLinesCommand.Execute(null);

        Assert.Equal(TeamPageTab.Lines, page.Tab);
        Assert.Equal(ShellPage.TeamPage, shell.CurrentPageKind);
    }

    [Fact]
    public void RosterPlayersOpenTheirProfiles()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        var other = session.Snapshot.League.Teams.First(team => team.Id != session.Snapshot.ManagedTeamId);
        shell.OpenTeam(other.Id);
        var row = shell.TeamPage!.Roster.Skaters[0];
        shell.TeamPage.Roster.SelectPlayer(row.Player.Id);

        shell.TeamPage.Roster.OpenProfileCommand.Execute(null);

        Assert.Equal(row.Player.Id, shell.Profile!.Id);
    }

    [Fact]
    public void AnotherTeamsLinesAreReadOnlyAndItsPlayersOpenProfiles()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        var other = session.Snapshot.League.Teams.First(team => team.Id != session.Snapshot.ManagedTeamId);
        shell.OpenTeam(other.Id);
        var lines = shell.TeamPage!.Lines;

        Assert.False(lines.IsEditable);
        Assert.False(lines.Lineup.IsEditable);
        Assert.False(lines.ShowsTeamChoice);
        Assert.Equal(other.Id, lines.Lineup.TeamId);
        Assert.False(lines.HasChanges);

        var player = lines.Lineup.ForwardLines[0].Centre.SelectedPlayer!;
        lines.OpenPlayerCommand.Execute(player);
        Assert.Equal(player.Id, shell.Profile!.Id);
    }

    [Fact]
    public void TheManagedTeamsLinesAreEditableAndShareUnsavedEditsWithTheLinesPage()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        shell.OpenTeam(session.Snapshot.ManagedTeamId);
        var teamLines = shell.TeamPage!.Lines;
        Assert.True(teamLines.IsEditable);
        Assert.False(shell.Lines.HasChanges);

        // An edit on the team page shows on the Lines page, which can save it.
        var first = teamLines.Lineup.ForwardLines[0].LeftWing.SelectedPlayer;
        var second = teamLines.Lineup.ForwardLines[3].RightWing.SelectedPlayer;
        teamLines.Lineup.ForwardLines[0].LeftWing.SelectedPlayer = second;

        Assert.True(teamLines.HasChanges);
        Assert.True(shell.Lines.HasChanges);
        Assert.Equal(second?.Id, shell.Lines.Lineup.ForwardLines[0].LeftWing.SelectedPlayer?.Id);
        shell.Lines.SaveCommand.Execute(null);
        Assert.Equal(second?.Id, session.ManagedTeam.Lineup.ForwardLines[0].LeftWingId);
        Assert.False(teamLines.HasChanges);
        Assert.Equal("Lineup saved.", teamLines.StatusMessage);

        // An edit on the Lines page shows on the team page, which can revert it.
        shell.Lines.Lineup.ForwardLines[0].LeftWing.SelectedPlayer = first;
        Assert.True(teamLines.HasChanges);
        Assert.Equal(first?.Id, teamLines.Lineup.ForwardLines[0].LeftWing.SelectedPlayer?.Id);
        teamLines.RevertCommand.Execute(null);
        Assert.False(shell.Lines.HasChanges);
        Assert.Equal(second?.Id, shell.Lines.Lineup.ForwardLines[0].LeftWing.SelectedPlayer?.Id);
    }

    [Fact]
    public async Task UnsavedEditsSurviveADayAndANewTeamPage()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        shell.Lines.Lineup.ForwardLines[0].LeftWing.SelectedPlayer = shell.Lines.Lineup.ForwardLines[3].RightWing.SelectedPlayer;
        var edited = shell.Lines.Lineup.ForwardLines[0].LeftWing.SelectedPlayer!.Id;

        shell.OpenTeam(session.Snapshot.ManagedTeamId);
        Assert.True(shell.TeamPage!.Lines.HasChanges);
        Assert.Equal(edited, shell.TeamPage.Lines.Lineup.ForwardLines[0].LeftWing.SelectedPlayer!.Id);

        shell.OpenTeam(session.Snapshot.League.Teams.First(team => team.Id != session.Snapshot.ManagedTeamId).Id);
        shell.OpenTeam(session.Snapshot.ManagedTeamId);
        Assert.Equal(edited, shell.TeamPage!.Lines.Lineup.ForwardLines[0].LeftWing.SelectedPlayer!.Id);

        if (!shell.HasPlayersToReplace)
        {
            await shell.AdvanceDayCommand.ExecuteAsync(null);
            Assert.True(shell.TeamPage!.Lines.HasChanges);
            Assert.Equal(edited, shell.TeamPage.Lines.Lineup.ForwardLines[0].LeftWing.SelectedPlayer!.Id);
        }
    }

    // Each page makes its own link commands, which would differ between otherwise equal rows.
    private static List<ScheduleMatchRowViewModel> WithoutLinks(IEnumerable<ScheduleMatchRowViewModel> rows) =>
        rows.Select(row => row with { OpenOpponentCommand = null }).ToList();

    [Fact]
    public async Task TheScheduleListsTheTeamsMatchesAsTheSchedulePageDoes()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        await shell.AdvanceDayCommand.ExecuteAsync(null);
        var other = session.Snapshot.League.Teams.First(team => team.Id != session.Snapshot.ManagedTeamId);
        shell.OpenTeam(other.Id);

        var rows = shell.TeamPage!.Schedule.Matches;

        shell.Schedule.ShowTeam(other.Id);
        Assert.Equal(WithoutLinks(shell.Schedule.Matches), WithoutLinks(rows));
        Assert.Contains(rows, row => row.PhaseLabel == "PRE" || row.PhaseLabel == "REG");
        Assert.Contains(rows, row => row.IsNext);
    }

    [Fact]
    public async Task ChoosingAResultOpensItsBoxScoreAndChoosingAFutureMatchOpensNothing()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        await shell.AdvanceDayCommand.ExecuteAsync(null);
        var snapshot = session.Snapshot.Season;
        var result = snapshot.PreseasonResults.Concat(snapshot.Results).First(
            candidate => candidate.Home.TeamId != session.Snapshot.ManagedTeamId && candidate.Away.TeamId != session.Snapshot.ManagedTeamId);
        shell.OpenTeam(result.Away.TeamId);
        var schedule = shell.TeamPage!.Schedule;

        schedule.SelectedMatch = schedule.Matches.First(row => !row.IsCompleted);
        Assert.Equal(ShellPage.TeamPage, shell.CurrentPageKind);
        Assert.Null(schedule.SelectedMatch);

        schedule.SelectedMatch = schedule.Matches.Single(row => row.Date == result.Date);

        Assert.Equal(ShellPage.LeagueSchedule, shell.CurrentPageKind);
        Assert.Equal(result.Away.TeamId, shell.Schedule.SelectedTeam.Id);
        Assert.Equal(result.Date, shell.Schedule.SelectedMatch?.Date);
        Assert.NotNull(shell.Schedule.SelectedResult);
        Assert.Null(schedule.SelectedMatch);
    }

    [Fact]
    public void TheScheduleSortsByAColumn()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        shell.OpenTeam(shell.Session.Snapshot.ManagedTeamId);
        var schedule = shell.TeamPage!.Schedule;

        schedule.SortMatchesCommand.Execute("opponent");

        Assert.Equal(schedule.Matches.Select(row => row.Opponent).Order(StringComparer.CurrentCulture), schedule.Matches.Select(row => row.Opponent));
    }

    [Fact]
    public void ThePlayoffScheduleAndStatisticsSwitchAndAMissedTeamHasDashes()
    {
        var session = season.Load(season.SecondRound);
        var shell = new GameShellViewModel(session);
        var playoffs = session.Snapshot.Season.Playoffs!;
        var qualifier = playoffs.Series[0].HigherRanked.TeamId;
        var missed = session.Snapshot.League.Teams.First(team => !playoffs.Series.Any(series =>
            series.HigherRanked.TeamId == team.Id || series.LowerRanked.TeamId == team.Id)).Id;

        shell.OpenTeam(qualifier);
        var statistics = shell.TeamPage!.Statistics;
        Assert.True(statistics.HasPlayoffStatistics);
        Assert.True(statistics.ShowsRegularSeason);
        Assert.Contains(shell.TeamPage.Schedule.Matches, row => row.IsPlayoff && row.PhaseLabel.StartsWith('R'));
        var regular = statistics.Statistics;
        var expected = playoffs.TeamStatistics.Single(candidate => candidate.TeamId == qualifier);

        statistics.ShowStatisticsSetCommand.Execute(StatisticsSet.Playoffs);

        Assert.True(statistics.ShowsPlayoffs);
        Assert.NotEqual(regular, statistics.Statistics);
        Assert.Equal(TeamStatistics.TeamStatisticsDisplay.Rows(expected), statistics.Statistics);

        shell.OpenTeam(missed);
        shell.TeamPage!.Statistics.ShowStatisticsSetCommand.Execute(StatisticsSet.Playoffs);
        Assert.Equal(TeamStatistics.TeamStatisticsDisplay.Rows(TeamStatistics.TeamStatisticsDisplay.None(missed)), shell.TeamPage.Statistics.Statistics);
        Assert.All(shell.TeamPage.Statistics.Statistics.Take(4), row => Assert.Equal("—", row.Value));
    }

    [Fact]
    public async Task ADayRefreshesTheBannerAndRosterKeepingTheTab()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        var other = session.Snapshot.League.Teams.First(team => team.Id != session.Snapshot.ManagedTeamId);
        shell.OpenTeam(other.Id);
        var page = shell.TeamPage!;
        page.Tabs![3].Command.Execute(null);

        await shell.AdvanceDayCommand.ExecuteAsync(null);

        Assert.Same(page, shell.TeamPage);
        Assert.Equal(TeamPageTab.Statistics, page.Tab);
        Assert.Equal(new TeamBannerViewModel(session, other.Id).Summary, ((TeamPageBannerViewModel)shell.Banner).Team.Summary);
        Assert.Equal(session.GetTeam(other.Id).Roster.Count, page.Roster.Skaters.Count + page.Roster.Goalies.Count);
    }
}