using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Teams;
using HockeySim.Domain;

using Xunit;

namespace HockeySim.Desktop.Tests;

/// <summary>
/// Back and forward through the shell: what makes a history entry (pages and entity selections,
/// not tabs), that each way of opening something makes one, and that a game's history is its own.
/// </summary>
[Collection(PlayedSeasonCollection.Name)]
public sealed class NavigationHistoryShellTests(PlayoffDisplayTests.PlayedSeason season)
{
    [Fact]
    public void AFreshShellHasNothingToGoBackOrForwardTo()
    {
        var shell = NewShell();

        AssertHistory(shell, canGoBack: false, canGoForward: false);
        Assert.False(shell.GoBackCommand.CanExecute(null));
        Assert.False(shell.GoForwardCommand.CanExecute(null));
    }

    [Fact]
    public void ANewShellForANewOrLoadedGameCannotGoBackToTheLastOne()
    {
        var session = GameTestData.StartSession();
        var first = new GameShellViewModel(session);
        first.Navigate(ShellPage.Roster);
        first.OpenTeam(OtherTeam(first));
        AssertHistory(first, canGoBack: true, canGoForward: false);

        // The main window builds a shell per game, as it does when a save is loaded.
        var second = new GameShellViewModel(session);

        AssertHistory(second, canGoBack: false, canGoForward: false);
        Assert.Equal(ShellPage.Home, second.CurrentPageKind);
    }

    [Fact]
    public void ChangingPageIsAnEntryAndBackAndForwardReturnThroughThePages()
    {
        var shell = NewShell();

        shell.Navigate(ShellPage.Roster);
        shell.Navigate(ShellPage.Standings);
        AssertHistory(shell, canGoBack: true, canGoForward: false);

        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Roster, shell.CurrentPageKind);
        Assert.Equal(ShellSection.Team, shell.CurrentSection.Section);
        Assert.IsType<HockeySim.Desktop.Roster.RosterPageViewModel>(shell.CurrentPage);
        AssertHistory(shell, canGoBack: true, canGoForward: true);

        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
        Assert.True(shell.IsHomeSection);
        AssertHistory(shell, canGoBack: false, canGoForward: true);

        shell.GoForwardCommand.Execute(null);
        shell.GoForwardCommand.Execute(null);
        Assert.Equal(ShellPage.Standings, shell.CurrentPageKind);
        Assert.Single(shell.Sections.SelectMany(section => section.Items), item => item.IsActive);
        AssertHistory(shell, canGoBack: true, canGoForward: false);
    }

    [Fact]
    public void ShowingThePageAlreadyShownAddsNoEntry()
    {
        var shell = NewShell();

        shell.Navigate(ShellPage.Home);

        AssertHistory(shell, canGoBack: false, canGoForward: false);
    }

    [Fact]
    public void NavigatingAfterGoingBackDropsTheForwardHistory()
    {
        var shell = NewShell();
        shell.Navigate(ShellPage.Roster);
        shell.Navigate(ShellPage.Standings);
        shell.GoBackCommand.Execute(null);

        shell.Navigate(ShellPage.Injuries);

        AssertHistory(shell, canGoBack: true, canGoForward: false);
        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Roster, shell.CurrentPageKind);
    }

    [Fact]
    public void OpeningAPlayerIsOneEntryAndBackReturnsToThePageItWasOpenedFrom()
    {
        var shell = NewShell();
        shell.Navigate(ShellPage.Roster);
        var playerId = shell.Session.ManagedTeam.Roster[0].Id;

        shell.OpenPlayer(playerId);

        Assert.Equal(ShellPage.PlayerProfile, shell.CurrentPageKind);
        Assert.Equal(ShellSection.Team, shell.CurrentSection.Section);
        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Roster, shell.CurrentPageKind);
        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
        AssertHistory(shell, canGoBack: false, canGoForward: true);
    }

    [Fact]
    public void BackAndForwardRestoreAProfileOnTheTabItWasLeftOn()
    {
        var shell = NewShell();
        var playerId = shell.Session.ManagedTeam.Roster[0].Id;
        shell.OpenPlayer(playerId);
        var profile = shell.Profile!;

        // Switching tabs is not an entry, but leaving remembers where the profile was.
        profile.Tab = PlayerProfileTab.Ratings;
        AssertHistory(shell, canGoBack: true, canGoForward: false);
        shell.Navigate(ShellPage.Standings);
        shell.GoBackCommand.Execute(null);

        Assert.Equal(ShellPage.PlayerProfile, shell.CurrentPageKind);
        Assert.Equal(playerId, shell.Profile!.Id);
        Assert.Equal(PlayerProfileTab.Ratings, shell.Profile.Tab);
        Assert.Same(shell.Profile, shell.CurrentPage);
        Assert.Equal(["Overview", "Ratings", "Statistics", "Game log", "Health", "Career"], shell.PageTabs.Select(tab => tab.Label));
    }

    [Fact]
    public void SwitchingProfileTabsOrStatisticsAddsNoEntry()
    {
        var shell = NewShell();
        shell.OpenPlayer(shell.Session.ManagedTeam.Roster[0].Id);

        shell.Profile!.Tab = PlayerProfileTab.Health;
        shell.Profile.Tab = PlayerProfileTab.GameLog;
        shell.Profile.Statistics = StatisticsSet.Playoffs;

        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
        AssertHistory(shell, canGoBack: false, canGoForward: true);
    }

    [Fact]
    public void OpeningAnotherPlayerFromAProfileIsAnotherEntry()
    {
        var shell = NewShell();
        var roster = shell.Session.ManagedTeam.Roster;
        shell.OpenPlayer(roster[0].Id);

        shell.OpenPlayer(roster[1].Id);
        Assert.Equal(roster[1].Id, shell.Profile!.Id);

        shell.GoBackCommand.Execute(null);
        Assert.Equal(roster[0].Id, shell.Profile!.Id);
        shell.GoForwardCommand.Execute(null);
        Assert.Equal(roster[1].Id, shell.Profile!.Id);
    }

    [Fact]
    public void OpeningTeamsFromTheDirectoryAndTheBannerEachMakeOneEntryAndBackRestoresTheTeam()
    {
        var shell = NewShell();
        var managed = shell.Session.Snapshot.ManagedTeamId;
        var other = OtherTeam(shell);
        shell.Navigate(ShellPage.Teams);

        shell.Teams.Conferences[0].Divisions[0].Teams.Single(row => row.Id == other).OpenTeamCommand.Execute(null);
        Assert.Equal(ShellPage.TeamPage, shell.CurrentPageKind);
        Assert.Equal(ShellSection.League, shell.CurrentSection.Section);
        shell.TeamBanner.OpenTeamCommand.Execute(null);
        Assert.Equal(managed, shell.TeamPage!.Id);

        shell.GoBackCommand.Execute(null);
        Assert.Equal(other, shell.TeamPage!.Id);
        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Teams, shell.CurrentPageKind);
        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
        shell.GoForwardCommand.Execute(null);
        shell.GoForwardCommand.Execute(null);
        shell.GoForwardCommand.Execute(null);
        Assert.Equal(managed, shell.TeamPage!.Id);
        AssertHistory(shell, canGoBack: true, canGoForward: false);
    }

    [Fact]
    public void ATeamPagesTabIsNotAnEntryButBackAndForwardRestoreIt()
    {
        var shell = NewShell();
        var first = OtherTeam(shell);
        var second = shell.Session.Snapshot.League.Teams.Where(team => team.Id != first && team.Id != shell.Session.Snapshot.ManagedTeamId).First().Id;
        shell.OpenTeam(first);
        Assert.Equal(TeamPageTab.Roster, shell.TeamPage!.Tab);

        shell.TeamPage.Tabs![2].Command.Execute(null);
        Assert.Equal(TeamPageTab.Schedule, shell.TeamPage.Tab);
        shell.OpenTeam(second);
        shell.TeamPage!.Tabs![3].Command.Execute(null);

        shell.GoBackCommand.Execute(null);
        Assert.Equal(first, shell.TeamPage!.Id);
        Assert.Equal(TeamPageTab.Schedule, shell.TeamPage.Tab);
        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
        AssertHistory(shell, canGoBack: false, canGoForward: true);
        shell.GoForwardCommand.Execute(null);
        shell.GoForwardCommand.Execute(null);
        Assert.Equal(second, shell.TeamPage!.Id);
        Assert.Equal(TeamPageTab.Statistics, shell.TeamPage.Tab);
        AssertHistory(shell, canGoBack: true, canGoForward: false);
    }

    [Fact]
    public async Task ARefreshAfterADayKeepsTheTeamPageAndItsTabAndAddsNoEntry()
    {
        var shell = NewShell();
        var other = OtherTeam(shell);
        shell.OpenTeam(other);
        shell.TeamPage!.Tabs![1].Command.Execute(null);

        await shell.AdvanceDayCommand.ExecuteAsync(null);

        Assert.Equal(other, shell.TeamPage!.Id);
        Assert.Equal(TeamPageTab.Lines, shell.TeamPage.Tab);
        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
        AssertHistory(shell, canGoBack: false, canGoForward: true);
    }

    [Fact]
    public async Task ChoosingAResultOnATeamPageOpensItsBoxScoreAsOneEntryAndBackReturnsToTheTab()
    {
        var shell = NewShell();
        await shell.AdvanceDayCommand.ExecuteAsync(null);
        var snapshot = shell.Session.Snapshot.Season;
        var result = snapshot.PreseasonResults.Concat(snapshot.Results).First();
        var teamId = result.Home.TeamId;
        shell.OpenTeam(teamId);
        shell.TeamPage!.Tabs![2].Command.Execute(null);

        shell.TeamPage.Schedule.SelectedMatch = shell.TeamPage.Schedule.Matches.Single(row => row.Date == result.Date);

        Assert.Equal(shell.Session.Snapshot.ManagedTeamId == teamId ? ShellPage.TeamSchedule : ShellPage.LeagueSchedule, shell.CurrentPageKind);
        Assert.Equal(result.Date, shell.Schedule.SelectedMatch?.Date);
        Assert.NotNull(shell.Schedule.SelectedResult);
        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.TeamPage, shell.CurrentPageKind);
        Assert.Equal(teamId, shell.TeamPage!.Id);
        Assert.Equal(TeamPageTab.Schedule, shell.TeamPage.Tab);
    }

    [Fact]
    public void OpeningATeamIsOneEntry()
    {
        var shell = NewShell();
        var other = OtherTeam(shell);

        shell.OpenTeam(other);

        Assert.Equal(ShellPage.TeamPage, shell.CurrentPageKind);
        Assert.Equal(other, shell.TeamPage!.Id);
        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
        AssertHistory(shell, canGoBack: false, canGoForward: true);
    }

    [Fact]
    public void ChoosingATeamOnTheLinesPageIsAnEntryAndOpeningItsLinesIsOne()
    {
        var shell = NewShell();
        var other = OtherTeam(shell);
        shell.Navigate(ShellPage.Lines);
        var managed = shell.Lines.SelectedTeam.Id;

        shell.Lines.SelectedTeam = shell.Lines.Teams.Single(team => team.Id == other);
        shell.GoBackCommand.Execute(null);
        Assert.Equal(managed, shell.Lines.SelectedTeam.Id);
        shell.GoForwardCommand.Execute(null);
        Assert.Equal(other, shell.Lines.SelectedTeam.Id);

        shell.Navigate(ShellPage.Roster);
        shell.OpenTeamLines(managed);
        Assert.Equal(managed, shell.Lines.SelectedTeam.Id);
        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Roster, shell.CurrentPageKind);
    }

    [Fact]
    public void LinesTabsAreNotEntries()
    {
        var shell = NewShell();
        shell.Navigate(ShellPage.Lines);

        shell.Lines.SelectTabCommand.Execute(Lines.LinesTab.PowerPlay);

        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
    }

    [Fact]
    public void ChoosingATeamAndAMatchOnTheScheduleAreEntries()
    {
        var shell = NewShell();
        var other = shell.Schedule.Teams.First(team => !team.IsManaged);
        shell.Navigate(ShellPage.LeagueSchedule);

        shell.Schedule.SelectedTeam = other;
        var match = shell.Schedule.Matches[2];
        shell.Schedule.SelectedMatch = match;

        shell.GoBackCommand.Execute(null);
        Assert.Equal(other, shell.Schedule.SelectedTeam);
        Assert.Null(shell.Schedule.SelectedMatch);
        shell.GoBackCommand.Execute(null);
        Assert.True(shell.Schedule.SelectedTeam.IsManaged);
        Assert.Equal(ShellPage.LeagueSchedule, shell.CurrentPageKind);
        shell.GoForwardCommand.Execute(null);
        shell.GoForwardCommand.Execute(null);
        Assert.Equal(other, shell.Schedule.SelectedTeam);
        Assert.Equal(match.Date, shell.Schedule.SelectedMatch?.Date);
    }

    [Fact]
    public void OpeningAMatchIsOneEntryInTheScheduleOfTheTeamThatPlaysIt()
    {
        var shell = NewShell();
        var other = shell.Schedule.Teams.First(team => !team.IsManaged);
        var date = shell.Session.Snapshot.Schedule.Matches.First(match => match.HomeTeamId == other.Id || match.AwayTeamId == other.Id).Date;

        shell.OpenMatch(date, other.Id);

        Assert.Equal(ShellPage.LeagueSchedule, shell.CurrentPageKind);
        Assert.Equal(date, shell.Schedule.SelectedMatch?.Date);
        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
        AssertHistory(shell, canGoBack: false, canGoForward: true);
        shell.GoForwardCommand.Execute(null);
        Assert.Equal(other, shell.Schedule.SelectedTeam);
        Assert.Equal(date, shell.Schedule.SelectedMatch?.Date);

        shell.OpenMatch(date, shell.Session.Snapshot.ManagedTeamId);
        Assert.Equal(ShellPage.TeamSchedule, shell.CurrentPageKind);
        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.LeagueSchedule, shell.CurrentPageKind);
        Assert.Equal(other, shell.Schedule.SelectedTeam);
    }

    [Fact]
    public void TheTeamAndLeagueSchedulesAreSeparateEntriesOverOneViewModel()
    {
        var shell = NewShell();
        var other = shell.Schedule.Teams.First(team => !team.IsManaged);
        shell.Navigate(ShellPage.LeagueSchedule);
        shell.Schedule.SelectedTeam = other;

        shell.Navigate(ShellPage.TeamSchedule);
        Assert.True(shell.Schedule.SelectedTeam.IsManaged);

        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.LeagueSchedule, shell.CurrentPageKind);
        Assert.Equal(other, shell.Schedule.SelectedTeam);
        shell.GoForwardCommand.Execute(null);
        Assert.Equal(ShellPage.TeamSchedule, shell.CurrentPageKind);
        Assert.True(shell.Schedule.SelectedTeam.IsManaged);
    }

    [Fact]
    public void OpeningAnInboxMessageIsOneEntryAndSelectingOneIsAnother()
    {
        var shell = NewShell();
        var messages = shell.Session.Snapshot.Inbox;

        shell.OpenInboxMessage(messages[1].Id);
        Assert.Equal(messages[1].Id, shell.Inbox.SelectedMessage?.Id);
        shell.Inbox.SelectedMessage = shell.Inbox.Messages.Single(message => message.Id == messages[2].Id);

        shell.GoBackCommand.Execute(null);
        Assert.Equal(messages[1].Id, shell.Inbox.SelectedMessage?.Id);
        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
        shell.GoForwardCommand.Execute(null);
        shell.GoForwardCommand.Execute(null);
        Assert.Equal(messages[2].Id, shell.Inbox.SelectedMessage?.Id);
        AssertHistory(shell, canGoBack: true, canGoForward: false);
    }

    [Fact]
    public void AnUnreadMessageSelectedIsOneEntryDespiteBeingMarkedRead()
    {
        var shell = NewShell();
        shell.Navigate(ShellPage.Inbox);
        var unread = shell.Inbox.Messages.First(message => message.IsUnread);

        shell.Inbox.SelectedMessage = unread;

        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Inbox, shell.CurrentPageKind);
        Assert.Null(shell.Inbox.SelectedMessage);
        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
    }

    [Fact]
    public void ChoosingAPlayoffSeriesIsAnEntryAndBackRestoresTheSeries()
    {
        var shell = new GameShellViewModel(season.Load(season.SecondRound));
        shell.Navigate(ShellPage.PlayoffPicture);
        var first = shell.Playoffs.SelectedSeries!;
        var other = shell.Playoffs.Rounds.SelectMany(round => round.Series).First(series => series != first);

        other.SelectCommand.Execute(null);
        Assert.Same(other, shell.Playoffs.SelectedSeries);

        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.PlayoffPicture, shell.CurrentPageKind);
        Assert.Equal((first.Round, first.HigherRanked.TeamId), (shell.Playoffs.SelectedSeries!.Round, shell.Playoffs.SelectedSeries.HigherRanked.TeamId));
        Assert.True(shell.Playoffs.SelectedSeries.IsSelected);
        Assert.False(other.IsSelected);
        shell.GoForwardCommand.Execute(null);
        Assert.Equal((other.Round, other.HigherRanked.TeamId), (shell.Playoffs.SelectedSeries!.Round, shell.Playoffs.SelectedSeries.HigherRanked.TeamId));
    }

    [Fact]
    public async Task PlayingADayAddsNoEntryAndKeepsTheCurrentOneInStepWithThePage()
    {
        var shell = NewShell();
        shell.Navigate(ShellPage.Inbox);
        shell.Navigate(ShellPage.Standings);

        await shell.AdvanceDayCommand.ExecuteAsync(null);

        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Inbox, shell.CurrentPageKind);
        shell.GoBackCommand.Execute(null);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
        AssertHistory(shell, canGoBack: false, canGoForward: true);
    }

    private static GameShellViewModel NewShell() => new(GameTestData.StartSession());

    private static TeamId OtherTeam(GameShellViewModel shell) =>
        shell.Session.Snapshot.League.Teams.First(team => team.Id != shell.Session.Snapshot.ManagedTeamId).Id;

    private static void AssertHistory(GameShellViewModel shell, bool canGoBack, bool canGoForward)
    {
        Assert.Equal(canGoBack, shell.GoBackCommand.CanExecute(null));
        Assert.Equal(canGoForward, shell.GoForwardCommand.CanExecute(null));
        Assert.Equal(canGoBack, shell.CanGoBack);
        Assert.Equal(canGoForward, shell.CanGoForward);
    }
}