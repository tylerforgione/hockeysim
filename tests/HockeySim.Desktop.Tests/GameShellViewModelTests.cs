using HockeySim.Desktop.Game;
using HockeySim.Desktop.Home;
using HockeySim.Desktop.Inbox;
using HockeySim.Desktop.Injuries;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Schedule;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class GameShellViewModelTests
{
    [Fact]
    public void ShellOpensOnHomeWithTheTeamBannerAndStatusBar()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);

        Assert.IsType<HomePageViewModel>(shell.CurrentPage);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
        Assert.True(shell.IsHomeSection);
        Assert.Equal(["Overview"], shell.CurrentSection.Items.Select(item => item.Label));
        Assert.True(shell.CurrentSection.Items[0].IsActive);

        var banner = Assert.IsType<TeamBannerViewModel>(shell.Banner);
        Assert.Equal("Ottawa Owls", banner.TeamName);
        Assert.Equal("OO", banner.TeamInitials);
        Assert.Equal("0-0-0 · 0 pts · 1st in Northern Division · 1st in Eastern Conference", banner.Summary);
        Assert.Equal(["NEXT", "LAST", "STREAK"], banner.Figures.Select(figure => figure.Label));
        Assert.EndsWith(" · Today", banner.Figures[0].Value, StringComparison.Ordinal);
        Assert.Equal("—", banner.Figures[1].Value);
        Assert.Equal("—", banner.Figures[2].Value);

        Assert.Equal(4, shell.UnreadCount);
        Assert.Equal("Inbox (4)", shell.InboxMenuLabel);
        Assert.Equal($"Inbox: {session.Snapshot.Inbox[0].Subject} (+3)", shell.LatestInboxItem);
        Assert.True(shell.IsLineupValid);
        Assert.Equal("Lineup valid", shell.LineupStatus);
        Assert.Equal($"HockeySim {AppVersion.Current}", GameShellViewModel.Version);
    }

    [Theory]
    [InlineData(ShellPage.LeagueLeaders)]
    [InlineData(ShellPage.PlayoffPicture)]
    [InlineData(ShellPage.PlayerStatistics)]
    [InlineData(ShellPage.Staff)]
    [InlineData(ShellPage.Transactions)]
    public void PlannedPagesAreListedButCannotBeOpened(ShellPage page)
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        var tab = GetTab(shell, page);

        Assert.False(tab.IsAvailable);
        Assert.False(tab.NavigateCommand.CanExecute(null));
        Assert.False(shell.OpenPageCommand.CanExecute(page));
        Assert.Throws<ArgumentException>(() => shell.Navigate(page));
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
    }

    [Fact]
    public void OpeningAPageShowsItsSectionsTabsWithOnlyItActive()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());

        shell.OpenPageCommand.Execute(ShellPage.Roster);

        Assert.IsType<RosterPageViewModel>(shell.CurrentPage);
        Assert.Equal(ShellSection.Team, shell.CurrentSection.Section);
        Assert.False(shell.IsHomeSection);
        Assert.Equal(
            ["Roster", "Lines", "Schedule", "Team statistics", "Injuries"],
            shell.CurrentSection.Items.Select(item => item.Label));
        Assert.Single(shell.Sections.SelectMany(section => section.Items), item => item.IsActive);
        Assert.True(GetTab(shell, ShellPage.Roster).IsActive);

        GetTab(shell, ShellPage.Injuries).NavigateCommand.Execute(null);
        Assert.IsType<InjuriesPageViewModel>(shell.CurrentPage);
        Assert.Equal(ShellPage.Injuries, shell.CurrentPageKind);

        shell.OpenPageCommand.Execute(ShellPage.Standings);
        Assert.Equal(ShellSection.League, shell.CurrentSection.Section);
        Assert.Equal(
            ["Standings", "Teams", "League leaders", "Playoff picture", "Schedule"],
            shell.CurrentSection.Items.Select(item => item.Label));

        shell.OpenPageCommand.Execute(ShellPage.Home);
        Assert.True(shell.IsHomeSection);
    }

    [Fact]
    public void TheTeamScheduleShowsTheManagedTeamAndTheLeagueScheduleKeepsItsChoice()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        var other = shell.Schedule.Teams.First(team => !team.IsManaged);

        shell.Navigate(ShellPage.LeagueSchedule);
        shell.Schedule.SelectedTeam = other;
        shell.Navigate(ShellPage.Standings);
        shell.Navigate(ShellPage.LeagueSchedule);

        Assert.IsType<SchedulePageViewModel>(shell.CurrentPage);
        Assert.Equal(ShellSection.League, shell.CurrentSection.Section);
        Assert.Equal(other, shell.Schedule.SelectedTeam);

        shell.Navigate(ShellPage.TeamSchedule);

        Assert.Equal(ShellSection.Team, shell.CurrentSection.Section);
        Assert.True(shell.Schedule.SelectedTeam.IsManaged);
    }

    [Fact]
    public void LoadingIsOfferedOnlyWhenTheShellCanOpenTheSavedGames()
    {
        Assert.False(new GameShellViewModel(GameTestData.StartSession()).LoadGameCommand.CanExecute(null));

        var opened = false;
        var shell = new GameShellViewModel(GameTestData.StartSession(), showLoadGame: () => opened = true);
        shell.LoadGameCommand.Execute(null);

        Assert.True(opened);
    }

    [Fact]
    public async Task AfterADayTheBannerShowsTheRecordLastResultAndStreak()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);

        await shell.AdvanceDayCommand.ExecuteAsync(null);

        var result = session.Snapshot.Season.Results.Single(result =>
            result.Home.TeamId == session.ManagedTeam.Id || result.Away.TeamId == session.ManagedTeam.Id);
        var won = result.WinnerId == session.ManagedTeam.Id;
        var record = session.Snapshot.Season.Standings.League.Single(entry => entry.Record.TeamId == session.ManagedTeam.Id).Record;
        var figures = shell.TeamBanner.Figures.ToDictionary(figure => figure.Label, figure => figure.Value);

        Assert.StartsWith(
            $"{record.Wins}-{record.RegulationLosses}-{record.OvertimeLosses + record.ShootoutLosses} · {record.Points} pts · ",
            shell.TeamBanner.Summary,
            StringComparison.Ordinal);
        Assert.StartsWith(MatchDisplay.ResultFor(result, session.ManagedTeam.Id), figures["LAST"], StringComparison.Ordinal);
        Assert.Equal(won ? "W1" : record.RegulationLosses == 1 ? "L1" : "OT1", figures["STREAK"]);
    }

    [Fact]
    public void TheStatusBarOpensTheNewestUnreadMessage()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        var newest = session.Snapshot.Inbox[0];

        shell.OpenLatestInboxItemCommand.Execute(null);

        var inbox = Assert.IsType<InboxPageViewModel>(shell.CurrentPage);
        Assert.Equal(ShellSection.Inbox, shell.CurrentSection.Section);
        Assert.Equal(newest.Id, inbox.SelectedMessage?.Id);
        Assert.Equal($"Inbox: {session.Snapshot.Inbox[1].Subject} (+2)", shell.LatestInboxItem);
        Assert.Equal("Inbox (3)", shell.InboxMenuLabel);
    }

    [Fact]
    public void OpeningAMessageFromHomeShowsItInTheInboxAndMarksItRead()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        var preview = shell.Home.InboxPreview[1];

        preview.OpenCommand.Execute(null);

        var inbox = Assert.IsType<InboxPageViewModel>(shell.CurrentPage);
        Assert.Equal(preview.Message.Id, inbox.SelectedMessage?.Id);
        Assert.False(inbox.SelectedMessage!.IsUnread);
        Assert.True(session.Snapshot.Inbox.Single(message => message.Id == preview.Message.Id).IsRead);
        Assert.Equal(3, shell.UnreadCount);
        Assert.Equal("3", shell.Home.Tiles.Single(tile => tile.Label == "INBOX").Value);
        Assert.Equal("4 messages · 3 unread", inbox.Subtitle);
    }

    [Fact]
    public void OpeningARatingLeaderShowsThatPlayerOnTheRoster()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        var goalieLeader = shell.Home.RatingLeaders.Single(leader => leader.RatingName == "Reflexes");

        goalieLeader.OpenCommand.Execute(null);

        var roster = Assert.IsType<RosterPageViewModel>(shell.CurrentPage);
        Assert.Equal(goalieLeader.PlayerName, roster.Roster.SelectedPlayer?.FullName);
        Assert.Equal(goalieLeader.PlayerName, roster.Roster.SelectedGoalie?.Name);
        Assert.Null(roster.Roster.SelectedSkater);
    }

    [Fact]
    public void HomeSummarisesTheManagedTeamAndItsDivision()
    {
        var session = GameTestData.StartSession();
        var home = new GameShellViewModel(session).Home;
        var team = session.ManagedTeam;

        Assert.Equal("23", home.Tiles.Single(tile => tile.Label == "ROSTER").Value);
        Assert.Equal("20", home.Tiles.Single(tile => tile.Label == "DRESSED").Value);
        Assert.Equal("NORTHERN DIVISION", home.DivisionTitle);
        Assert.Equal(8, home.DivisionStandings.Count);
        Assert.Single(home.DivisionStandings, row => row.IsManaged && row.TeamName == team.Name);
        Assert.Equal(8, home.LineupSummary.Count);
        Assert.Equal(4, home.InboxPreview.Count);
        Assert.All(home.RatingLeaders, leader => Assert.Contains(
            team.Roster,
            player => $"{player.FirstName} {player.LastName}" == leader.PlayerName));
    }

    private static NavigationItemViewModel GetTab(GameShellViewModel shell, ShellPage page) =>
        shell.Sections.SelectMany(section => section.Items).Single(item => item.Page == page);
}