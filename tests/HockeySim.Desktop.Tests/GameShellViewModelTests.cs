using HockeySim.Desktop.Game;
using HockeySim.Desktop.Home;
using HockeySim.Desktop.Inbox;
using HockeySim.Desktop.Roster;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class GameShellViewModelTests
{
    [Fact]
    public void ShellOpensOnHomeWithTeamIdentityAndUnreadBadge()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());

        Assert.IsType<HomePageViewModel>(shell.CurrentPage);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
        Assert.Equal("Ottawa Owls", shell.TeamName);
        Assert.Equal("OO", shell.TeamInitials);
        Assert.Equal("Northern Division · Eastern Conference", shell.TeamDivision);
        Assert.Equal("2026–27 Season", shell.SeasonLabel);
        Assert.Equal(4, GetItem(shell, ShellPage.Inbox).BadgeCount);
        Assert.True(GetItem(shell, ShellPage.Home).IsActive);
    }

    [Theory]
    [InlineData(ShellPage.Standings)]
    [InlineData(ShellPage.FreeAgents)]
    [InlineData(ShellPage.Trades)]
    public void PlannedPagesAreListedButCannotBeOpened(ShellPage page)
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        var item = GetItem(shell, page);

        Assert.False(item.IsAvailable);
        Assert.False(item.NavigateCommand.CanExecute(null));
        Assert.NotNull(item.UnavailableReason);
        Assert.Throws<ArgumentException>(() => shell.Navigate(page));
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
    }

    [Fact]
    public void NavigatingMarksOnlyTheCurrentItemActive()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());

        GetItem(shell, ShellPage.Roster).NavigateCommand.Execute(null);

        Assert.IsType<RosterPageViewModel>(shell.CurrentPage);
        Assert.Single(shell.NavigationSections.SelectMany(section => section.Items), item => item.IsActive);
        Assert.True(GetItem(shell, ShellPage.Roster).IsActive);
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
        Assert.Equal(3, GetItem(shell, ShellPage.Inbox).BadgeCount);
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

    private static NavigationItemViewModel GetItem(GameShellViewModel shell, ShellPage page) =>
        shell.NavigationSections.SelectMany(section => section.Items).Single(item => item.Page == page);
}