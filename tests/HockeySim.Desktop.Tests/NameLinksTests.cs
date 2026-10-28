using HockeySim.Desktop.Game;
using HockeySim.Desktop.Home;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Schedule;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;

using Xunit;

namespace HockeySim.Desktop.Tests;

/// <summary>
/// Every player and team name in the game pages opens what it names. Each click is one history
/// entry, so Back returns to the page and selection the name was clicked on.
/// </summary>
[Collection(PlayedSeasonCollection.Name)]
public sealed class NameLinksTests(PlayoffDisplayTests.PlayedSeason season)
{
    // Far more days than the managed team takes to dress an injured player with the test seed.
    private const int MaximumDays = 120;

    [Fact]
    public void ARosterNameOpensTheProfileWithTheRostersStatisticsAndBackReturnsToTheRoster()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        shell.Navigate(ShellPage.Roster);
        var roster = shell.Roster.Roster;
        var row = roster.Skaters[3];
        var goalie = roster.Goalies[0];

        row.OpenProfileCommand!.Execute(null);

        Assert.Equal(ShellPage.PlayerProfile, shell.CurrentPageKind);
        Assert.Equal(row.Player.Id, shell.Profile!.Id);
        Assert.Equal(PlayerProfileTab.Overview, shell.Profile.Tab);
        Assert.Equal(roster.Statistics, shell.Profile.Statistics);
        GoBack(shell);
        Assert.Equal(ShellPage.Roster, shell.CurrentPageKind);
        Assert.True(shell.CanGoForward);

        // The name was one entry: one more Back leaves the roster for the home page.
        GoBack(shell);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
        shell.GoForwardCommand.Execute(null);

        goalie.OpenProfileCommand!.Execute(null);
        Assert.Equal(goalie.Player.Id, shell.Profile!.Id);
    }

    [Fact]
    public void ARosterOnATeamPageLinksItsPlayersToo()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        var other = shell.Session.Snapshot.League.Teams.First(team => team.Id != shell.Session.Snapshot.ManagedTeamId);
        shell.OpenTeam(other.Id);
        var row = shell.TeamPage!.Roster.Skaters[0];

        row.OpenProfileCommand!.Execute(null);

        Assert.Equal(row.Player.Id, shell.Profile!.Id);
        GoBack(shell);
        Assert.Equal(other.Id, shell.TeamPage!.Id);
    }

    [Fact]
    public void ARosterWithNowhereToOpenAPlayerShowsPlainNames()
    {
        var session = GameTestData.StartSession();
        var roster = new Desktop.Roster.TeamRosterViewModel(session, session.ManagedTeam.Id);

        Assert.All(roster.Skaters.Concat(roster.Goalies), row => Assert.Null(row.OpenProfileCommand));
    }

    [Fact]
    public void AScheduleOpponentOpensItsTeamPageAndBackKeepsTheSelectedMatch()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        shell.Navigate(ShellPage.TeamSchedule);
        var selected = shell.Schedule.Matches.First(match => match.IsCompleted);
        shell.Schedule.SelectedMatch = selected;
        var row = shell.Schedule.Matches.Skip(5).First();
        var opponentId = OpponentOf(shell.Session, row);

        row.OpenOpponentCommand!.Execute(null);

        Assert.Equal(ShellPage.TeamPage, shell.CurrentPageKind);
        Assert.Equal(opponentId, shell.TeamPage!.Id);
        GoBack(shell);
        Assert.Equal(ShellPage.TeamSchedule, shell.CurrentPageKind);
        Assert.Equal(selected.Date, shell.Schedule.SelectedMatch!.Date);
    }

    [Fact]
    public void ATeamPagesScheduleOpponentOpensItsTeamPage()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        var other = shell.Session.Snapshot.League.Teams.First(team => team.Id != shell.Session.Snapshot.ManagedTeamId);
        shell.OpenTeam(other.Id);
        var row = shell.TeamPage!.Schedule.Matches[0];

        row.OpenOpponentCommand!.Execute(null);

        Assert.Equal(OpponentOf(shell.Session, row, other.Id), shell.TeamPage!.Id);
        GoBack(shell);
        Assert.Equal(other.Id, shell.TeamPage!.Id);
    }

    [Fact]
    public void ASchedulePageWithNowhereToOpenATeamShowsPlainOpponents()
    {
        var schedule = new SchedulePageViewModel(GameTestData.StartSession());

        Assert.All(schedule.Matches, row => Assert.Null(row.OpenOpponentCommand));
    }

    [Fact]
    public void EveryNameInABoxScoreOpensItsPlayerOrTeamAndBackReturnsToTheBoxScore()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        var session = shell.Session;
        var managedId = session.Snapshot.ManagedTeamId;
        var result = session.Snapshot.Season.PreseasonResults
            .Where(candidate => TeamResults.Involves(candidate, managedId))
            .First(candidate => candidate.Goals.Any(goal => goal.PrimaryAssistId is not null && goal.SecondaryAssistId is not null) && candidate.Penalties.Count > 0);
        shell.OpenMatch(result.Date, managedId);
        var detail = shell.Schedule.SelectedResult!;

        var playerLinks = new List<(System.Windows.Input.ICommand? Command, PlayerId Expected)>();
        for (var index = 0; index < result.Goals.Count; index++)
        {
            var (goal, row) = (result.Goals[index], detail.ScoringSummary[index]);
            playerLinks.Add((row.OpenScorerCommand, goal.ScorerId));
            var assists = new[] { goal.PrimaryAssistId, goal.SecondaryAssistId }.OfType<PlayerId>().ToList();
            Assert.Equal(assists.Count, row.AssistLinks.Count);
            playerLinks.AddRange(row.AssistLinks.Select((link, assist) => (link.OpenCommand, assists[assist])));
        }

        playerLinks.AddRange(result.Penalties.Select((penalty, index) => (detail.PenaltySummary[index].OpenPlayerCommand, penalty.PlayerId)));
        playerLinks.AddRange(result.ThreeStars.Select((star, index) => (detail.ThreeStars[index].OpenPlayerCommand, star.PlayerId)));
        playerLinks.Add((detail.Goalies[0].Goalie.OpenPlayerCommand, result.Away.Goalie.PlayerId));
        playerLinks.Add((detail.Goalies[1].Goalie.OpenPlayerCommand, result.Home.Goalie.PlayerId));
        playerLinks.AddRange(result.Away.Skaters.Select((line, index) => (detail.Away.Skaters[index].OpenPlayerCommand, line.PlayerId)));
        playerLinks.AddRange(result.Home.Skaters.Select((line, index) => (detail.Home.Skaters[index].OpenPlayerCommand, line.PlayerId)));
        playerLinks.Add((detail.Away.Goalie.OpenPlayerCommand, result.Away.Goalie.PlayerId));
        playerLinks.Add((detail.Home.Goalie.OpenPlayerCommand, result.Home.Goalie.PlayerId));

        Assert.True(playerLinks.Count > 40);
        foreach (var (command, expected) in playerLinks)
        {
            command!.Execute(null);
            Assert.Equal(ShellPage.PlayerProfile, shell.CurrentPageKind);
            Assert.Equal(expected, shell.Profile!.Id);
            GoBack(shell);
            AssertBoxScoreShown(shell, result);
        }

        foreach (var (command, expected) in new[] { (detail.Away.OpenTeamCommand, result.Away.TeamId), (detail.Home.OpenTeamCommand, result.Home.TeamId) })
        {
            command!.Execute(null);
            Assert.Equal(ShellPage.TeamPage, shell.CurrentPageKind);
            Assert.Equal(expected, shell.TeamPage!.Id);
            GoBack(shell);
            AssertBoxScoreShown(shell, result);
        }
    }

    [Fact]
    public void AnAssistsVisibleTextIsUnchangedByLinkingEachPlayer()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        var managedId = shell.Session.Snapshot.ManagedTeamId;
        var result = shell.Session.Snapshot.Season.PreseasonResults
            .First(candidate => TeamResults.Involves(candidate, managedId) && candidate.Goals.Any(goal => goal.PrimaryAssistId is not null && goal.SecondaryAssistId is not null));
        shell.OpenMatch(result.Date, managedId);

        foreach (var row in shell.Schedule.SelectedResult!.ScoringSummary)
        {
            var text = row.IsUnassisted ? "Unassisted" : string.Concat(row.AssistLinks.Select(link => link.Text + link.Separator));
            Assert.Equal(row.Assists, text);
        }
    }

    [Fact]
    public void ABoxScoreWithNowhereToOpenAnythingShowsPlainNames()
    {
        var session = GameTestData.StartSession();
        var result = session.Snapshot.Season.PreseasonResults[0];

        var detail = new MatchDetailViewModel(result, session.PlayersById, session.GetTeam);

        Assert.Null(detail.Home.OpenTeamCommand);
        Assert.Null(detail.Home.Goalie.OpenPlayerCommand);
        Assert.All(detail.Home.Skaters, row => Assert.Null(row.OpenPlayerCommand));
        Assert.All(detail.ScoringSummary, row =>
        {
            Assert.Null(row.OpenScorerCommand);
            Assert.All(row.AssistLinks, link => Assert.Null(link.OpenCommand));
        });
        Assert.All(detail.ThreeStars, star => Assert.Null(star.OpenPlayerCommand));
    }

    [Fact]
    public void ABoxScoresPlayerTablesStillSortWhileTheirNamesLink()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        var managedId = shell.Session.Snapshot.ManagedTeamId;
        var result = shell.Session.Snapshot.Season.PreseasonResults.First(candidate => TeamResults.Involves(candidate, managedId));
        shell.OpenMatch(result.Date, managedId);
        var detail = shell.Schedule.SelectedResult!;

        detail.Away.SortSkatersCommand.Execute("name");

        var first = detail.Away.Skaters[0];
        first.OpenPlayerCommand!.Execute(null);
        Assert.Equal(first.Name, PlayerDisplay.FullName(shell.Session.PlayersById[shell.Profile!.Id]));
    }

    [Fact]
    public void APlayoffTeamNameInTheBracketOrASeriesGameOpensItsTeamPage()
    {
        var shell = new GameShellViewModel(season.Load(season.PlayoffsStart));
        shell.Navigate(ShellPage.PlayoffPicture);
        var series = shell.Playoffs.Rounds[0].Series[2];
        series.SelectCommand.Execute(null);

        series.LowerRanked.OpenTeamCommand!.Execute(null);

        Assert.Equal(series.LowerRanked.TeamId, shell.TeamPage!.Id);
        GoBack(shell);
        Assert.Equal(ShellPage.PlayoffPicture, shell.CurrentPageKind);
        Assert.Equal(series.HigherRanked.TeamId, shell.Playoffs.SelectedSeries!.HigherRanked.TeamId);

        var game = shell.Playoffs.SelectedSeries.Games[0];
        game.Home.OpenCommand!.Execute(null);
        Assert.Equal(game.Home.Name, shell.Session.GetTeam(shell.TeamPage!.Id).Name);
        GoBack(shell);
        game.Away.OpenCommand!.Execute(null);
        Assert.Equal(game.Away.Name, shell.Session.GetTeam(shell.TeamPage!.Id).Name);
        GoBack(shell);
        Assert.Equal(series.HigherRanked.TeamId, shell.Playoffs.SelectedSeries!.HigherRanked.TeamId);
    }

    [Fact]
    public void HomePanelOpponentsAndTapeTeamsOpenTheirTeamPages()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        var home = shell.Home;
        var upcoming = home.UpcomingGames.Rows[0];
        var recent = home.RecentResults.Rows[0];

        upcoming.OpenOpponentCommand!.Execute(null);
        Assert.Equal(upcoming.Opponent, shell.Session.GetTeam(shell.TeamPage!.Id).Name);
        GoBack(shell);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);

        recent.OpenOpponentCommand!.Execute(null);
        Assert.Equal(recent.Opponent, shell.Session.GetTeam(shell.TeamPage!.Id).Name);
        GoBack(shell);

        home.TaleOfTheTape.Theirs!.OpenCommand!.Execute(null);
        Assert.Equal(home.TaleOfTheTape.Theirs.TeamId, shell.TeamPage!.Id);
        GoBack(shell);
        home.TaleOfTheTape.Ours!.OpenCommand!.Execute(null);
        Assert.Equal(shell.Session.Snapshot.ManagedTeamId, shell.TeamPage!.Id);
        GoBack(shell);
        Assert.Equal(ShellPage.Home, shell.CurrentPageKind);
        Assert.False(shell.CanGoBack);
    }

    [Fact]
    public void HomePanelsWithNowhereToOpenATeamShowPlainOpponents()
    {
        var session = GameTestData.StartSession();

        Assert.All(new UpcomingGamesPanelViewModel(session, _ => { }, (_, _) => { }).Rows, row => Assert.Null(row.OpenOpponentCommand));
        Assert.All(new RecentResultsPanelViewModel(session, (_, _) => { }).Rows, row => Assert.Null(row.OpenOpponentCommand));
    }

    [Fact]
    public void InjuryReportNamesAndTheInjuredPlayersBannerOpenTheirProfiles()
    {
        var shell = new GameShellViewModel(SessionWhereContinueIsBlocked());
        shell.Navigate(ShellPage.Injuries);
        var row = shell.Injuries.Injuries[0];

        row.OpenPlayerCommand!.Execute(null);

        Assert.Equal(PlayerDisplay.FullName(shell.Session.PlayersById[shell.Profile!.Id]), row.Name);
        GoBack(shell);
        Assert.Equal(ShellPage.Injuries, shell.CurrentPageKind);

        var links = shell.PlayersToReplaceLinks;
        Assert.Equal(shell.Session.Snapshot.PlayersToReplace.Count, links.Count);
        Assert.Equal(shell.PlayersToReplaceMessage, shell.PlayersToReplaceLead + string.Concat(links.Select(link => link.Text + link.Separator)));
        Assert.StartsWith("Injured players dressed: ", shell.PlayersToReplaceMessage, StringComparison.Ordinal);
        for (var index = 0; index < links.Count; index++)
        {
            links[index].OpenCommand!.Execute(null);
            Assert.Equal(shell.Session.Snapshot.PlayersToReplace[index], shell.Profile!.Id);
            GoBack(shell);
            Assert.Equal(ShellPage.Injuries, shell.CurrentPageKind);
        }
    }

    [Fact]
    public void AProfileGameLogOpponentOpensItsTeamPageWhileTheRowStillOpensTheBoxScore()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());
        var session = shell.Session;
        var playerId = session.ManagedTeam.Roster.First(player => player.Position != Position.Goalie).Id;
        shell.OpenPlayer(playerId, PlayerProfileTab.GameLog);
        var row = shell.Profile!.GameLog[0];

        row.OpenOpponentCommand!.Execute(null);

        Assert.Equal(row.OpponentName, session.GetTeam(shell.TeamPage!.Id).Name);
        GoBack(shell);
        Assert.Equal(ShellPage.PlayerProfile, shell.CurrentPageKind);
        Assert.Equal(PlayerProfileTab.GameLog, shell.Profile!.Tab);
        Assert.Equal($"{row.Venue} {row.OpponentName}", row.Opponent);

        shell.Profile.LastFiveGames[0].OpenOpponentCommand!.Execute(null);
        Assert.Equal(ShellPage.TeamPage, shell.CurrentPageKind);
        GoBack(shell);

        shell.Profile.SelectedGame = shell.Profile.GameLog[0];
        Assert.Equal(ShellPage.TeamSchedule, shell.CurrentPageKind);
        Assert.Equal(shell.Profile.GameLog[0].Date, shell.Schedule.SelectedMatch!.Date);
    }

    private static void GoBack(GameShellViewModel shell)
    {
        Assert.True(shell.CanGoBack);
        shell.GoBackCommand.Execute(null);
    }

    /// <summary>The box score shown on the schedule page, as it was before the name was clicked.</summary>
    private static void AssertBoxScoreShown(GameShellViewModel shell, CompletedMatchSnapshot result)
    {
        Assert.Contains(shell.CurrentPageKind, new[] { ShellPage.TeamSchedule, ShellPage.LeagueSchedule });
        Assert.Equal(result.Date, shell.Schedule.SelectedMatch!.Date);
        Assert.NotNull(shell.Schedule.SelectedResult);
    }

    private static TeamId OpponentOf(GameSession session, ScheduleMatchRowViewModel row, TeamId? team = null)
    {
        var teamId = team ?? session.Snapshot.ManagedTeamId;
        var match = session.Snapshot.Schedule.PreseasonMatches
            .Concat(session.Snapshot.Schedule.Matches)
            .Concat(session.Snapshot.Schedule.PlayoffMatches)
            .First(candidate => candidate.Date == row.Date && (candidate.HomeTeamId == teamId || candidate.AwayTeamId == teamId));
        return match.HomeTeamId == teamId ? match.AwayTeamId : match.HomeTeamId;
    }

    private static GameSession SessionWhereContinueIsBlocked()
    {
        var manager = new GameManager();
        GameTestData.StartSession(manager);
        for (var day = 0; day < MaximumDays; day++)
        {
            if (manager.GetSnapshot().PlayersToReplace.Count > 0)
            {
                return new GameSession(manager, "Injuries");
            }

            manager.AdvanceDay();
        }

        throw new InvalidOperationException("The managed team never dressed a player who could not play.");
    }
}