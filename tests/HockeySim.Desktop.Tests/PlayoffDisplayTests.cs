using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Playoffs;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.Standings;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Saves;

using Xunit;

namespace HockeySim.Desktop.Tests;

/// <summary>
/// The playoffs as Desktop shows them at each stage: the bracket, the schedule's playoff games,
/// playoff statistics, and the champion. One season is played through the shell and shared; each
/// test loads a fresh session from the stage it needs.
/// </summary>
public sealed class PlayoffDisplayTests(PlayoffDisplayTests.PlayedSeason season)
    : IClassFixture<PlayoffDisplayTests.PlayedSeason>
{
    [Fact]
    public void BeforeTheRegularSeasonEndsThereIsNoBracketOrPlayoffStatistics()
    {
        var shell = new GameShellViewModel(GameTestData.StartSession());

        Assert.False(shell.Playoffs.HasPlayoffs);
        Assert.Contains("after the regular season ends", shell.Playoffs.NotStartedMessage, StringComparison.Ordinal);
        Assert.EndsWith("begin after the regular season", shell.Playoffs.Subtitle, StringComparison.Ordinal);
        Assert.Equal(["FIRST ROUND", "SECOND ROUND", "CONFERENCE FINAL", "FINAL"], shell.Playoffs.Rounds.Select(round => round.Title));
        Assert.All(shell.Playoffs.Rounds, round => Assert.False(round.HasStarted));
        Assert.Null(shell.Playoffs.SelectedSeries);

        var roster = shell.Roster.Roster;
        Assert.False(roster.HasPlayoffStatistics);
        roster.ShowStatisticsSetCommand.Execute(StatisticsSet.Playoffs);
        Assert.True(roster.ShowsRegularSeason);
    }

    [Fact]
    public void OnceTheRegularSeasonEndsTheFirstRoundIsShownWithItsFirstGames()
    {
        var shell = new GameShellViewModel(season.Load(season.PlayoffsStart));
        var playoffs = shell.Session.Snapshot.Season.Playoffs!;
        var page = shell.Playoffs;

        Assert.True(page.HasPlayoffs);
        Assert.False(page.HasChampion);
        Assert.EndsWith("first round in progress", page.Subtitle, StringComparison.Ordinal);
        Assert.EndsWith(" · Playoffs · First round", shell.CalendarLabel, StringComparison.Ordinal);

        var firstRound = page.Rounds[0].Series;
        Assert.Equal(8, firstRound.Count);
        Assert.All(page.Rounds.Skip(1), round => Assert.False(round.HasStarted));
        Assert.All(firstRound, series =>
        {
            Assert.Equal("Series starts", series.Status);
            Assert.Equal(0, series.HigherRanked.Wins + series.LowerRanked.Wins);
            var game = Assert.Single(series.Games);
            Assert.Equal("Game 1", game.Game);
            Assert.Equal("Scheduled", game.Result);
            Assert.False(game.IsPlayed);
        });

        // Each conference's two division winners face its wild cards; every other team is a
        // division's second or third.
        var seeds = firstRound.SelectMany(series => new[] { series.HigherRanked.Seed, series.LowerRanked.Seed }).ToList();
        Assert.Equal(4, seeds.Count(seed => seed.StartsWith("WC", StringComparison.Ordinal)));
        Assert.Equal(4, seeds.Count(seed => seed.EndsWith('1') && !seed.StartsWith("WC", StringComparison.Ordinal)));
        Assert.All(firstRound, series => Assert.DoesNotContain("WC", series.HigherRanked.Seed, StringComparison.Ordinal));

        // The managed team's series is shown first when it qualified.
        var managedTeamId = shell.Session.Snapshot.ManagedTeamId;
        var managedSeries = firstRound.SingleOrDefault(series => series.Involves(managedTeamId));
        Assert.Same(managedSeries ?? firstRound[0], page.SelectedSeries);
        Assert.True(page.SelectedSeries!.IsSelected);

        // A qualifier's schedule lists its first playoff game next, labelled by round and game.
        var qualifier = playoffs.Series[0].HigherRanked.TeamId;
        shell.Schedule.SelectedTeam = shell.Schedule.Teams.Single(team => team.Id == qualifier);
        var playoffRow = Assert.Single(shell.Schedule.Matches, match => match.Phase == SeasonPhase.Playoffs);
        Assert.Equal("R1 G1", playoffRow.PhaseLabel);
        Assert.Equal("Playoffs: First round, game 1", playoffRow.PhaseDescription);
        Assert.True(playoffRow.IsNext);
        Assert.Equal(84, shell.Schedule.Matches.Count(match => match.Phase == SeasonPhase.RegularSeason && match.IsCompleted));
        Assert.Equal("Playoffs", shell.Home.Tiles[0].Value);
    }

    [Fact]
    public void MidPlayoffsEverySeriesShowsItsScoreAndItsGamesOpenTheirBoxScores()
    {
        var shell = new GameShellViewModel(season.Load(season.SecondRound));
        var page = shell.Playoffs;
        string Name(TeamId teamId) => shell.Session.GetTeam(teamId).Name;

        Assert.EndsWith(" · Playoffs · Second round", shell.CalendarLabel, StringComparison.Ordinal);
        Assert.Equal([8, 4, 0, 0], page.Rounds.Select(round => round.Series.Count));
        Assert.All(page.Rounds[0].Series, series =>
        {
            var (winner, loser) = series.HigherRanked.IsWinner ? (series.HigherRanked, series.LowerRanked) : (series.LowerRanked, series.HigherRanked);
            Assert.True(winner.IsWinner);
            Assert.True(loser.IsEliminated);
            Assert.Equal(4, winner.Wins);
            Assert.Equal($"{winner.Name} win 4–{loser.Wins}", series.Status);
            Assert.Equal(4 + loser.Wins, series.Games.Count);
            Assert.All(series.Games, game => Assert.True(game.IsPlayed));
        });
        Assert.Contains(page.Rounds[1].Series, series => series.Games.Any(game => game.IsPlayed));

        // Selecting a decided series lists its games; opening one shows its box score.
        var decided = page.Rounds[0].Series[^1];
        decided.SelectCommand.Execute(null);
        Assert.Same(decided, page.SelectedSeries);
        Assert.True(decided.IsSelected);
        Assert.All(page.Rounds[0].Series.Where(series => series != decided), series => Assert.False(series.IsSelected));

        var lastGame = decided.Games[^1];
        lastGame.OpenCommand.Execute(null);

        var managesSelectedTeam = shell.Schedule.SelectedTeam.Id == shell.Session.Snapshot.ManagedTeamId;
        Assert.Equal(managesSelectedTeam ? ShellPage.TeamSchedule : ShellPage.LeagueSchedule, shell.CurrentPageKind);
        Assert.NotNull(shell.Schedule.SelectedResult);
        Assert.Equal(lastGame.Date, MatchDisplay.ShortDate(shell.Schedule.SelectedMatch!.Date));
        Assert.StartsWith("R1 G", shell.Schedule.SelectedMatch!.PhaseLabel, StringComparison.Ordinal);
        Assert.Contains(Name(shell.Schedule.SelectedTeam.Id), decided.Title, StringComparison.Ordinal);

        // The selection survives a refresh.
        page.Refresh();
        Assert.Equal(decided.Title, page.SelectedSeries!.Title);
    }

    [Fact]
    public void RostersAndProfilesSwitchBetweenRegularSeasonAndPlayoffStatistics()
    {
        var session = season.Load(season.SecondRound);
        var shell = new GameShellViewModel(session);
        var playoffs = session.Snapshot.Season.Playoffs!;
        var qualifier = playoffs.Series[0].HigherRanked.TeamId;
        shell.Teams.SelectTeam(qualifier);
        var roster = shell.Teams.Roster;

        Assert.True(roster.HasPlayoffStatistics);
        Assert.True(roster.ShowsRegularSeason);
        Assert.Contains(roster.Skaters, row => row.Season.GamesPlayed > 10);

        var skater = roster.Skaters.First(row => playoffs.SkaterStatistics.Any(statistics => statistics.PlayerId == row.Player.Id));
        roster.SelectPlayer(skater.Player.Id);
        roster.ShowStatisticsSetCommand.Execute(StatisticsSet.Playoffs);

        Assert.True(roster.ShowsPlayoffs);
        Assert.All(roster.Skaters, row =>
        {
            var statistics = playoffs.SkaterStatistics.SingleOrDefault(candidate => candidate.PlayerId == row.Player.Id);
            Assert.Equal(statistics?.GamesPlayed ?? 0, row.Season.GamesPlayed);
            Assert.Equal(statistics?.Points ?? 0, row.Season.Points);
        });
        Assert.All(roster.Goalies, row => Assert.Equal(
            playoffs.GoalieStatistics.SingleOrDefault(candidate => candidate.PlayerId == row.Player.Id)?.GamesPlayed ?? 0,
            row.Season.GamesPlayed));

        // The profile follows the switch and keeps the selected player.
        var profile = roster.SelectedPlayer!;
        Assert.Equal(skater.Player.Id, profile.Id);
        Assert.Equal("2026–27 PLAYOFFS", profile.SeasonTitle);
        var gamesPlayed = playoffs.SkaterStatistics.Single(statistics => statistics.PlayerId == skater.Player.Id).GamesPlayed;
        Assert.Equal(gamesPlayed.ToString(System.Globalization.CultureInfo.CurrentCulture), profile.SeasonStatisticGroups[0].Statistics[0].Value);

        // The choice holds across teams and refreshes. A team that missed the playoffs has none.
        var missed = session.Snapshot.League.Teams.First(team => !playoffs.Series.Any(series =>
            series.HigherRanked.TeamId == team.Id || series.LowerRanked.TeamId == team.Id));
        shell.Teams.SelectTeam(missed.Id);
        roster = shell.Teams.Roster;
        Assert.True(roster.ShowsPlayoffs);
        Assert.All(roster.Skaters.Concat(roster.Goalies), row => Assert.Equal(PlayerSeasonTotals.None, row.Season));
        roster.SelectPlayer(roster.Skaters[0].Player.Id);
        Assert.Equal("No playoff appearances.", roster.SelectedPlayer!.SeasonCaption);
        roster.SelectPlayer(roster.Goalies[0].Player.Id);
        Assert.Equal("No playoff starts.", roster.SelectedPlayer!.SeasonCaption);

        shell.Teams.Refresh();
        Assert.True(shell.Teams.Roster.ShowsPlayoffs);

        shell.Teams.Roster.ShowStatisticsSetCommand.Execute(StatisticsSet.RegularSeason);
        Assert.Contains(shell.Teams.Roster.Skaters, row => row.Season.GamesPlayed > 10);
        Assert.Equal("2026–27 REGULAR SEASON", shell.Teams.Roster.SelectedPlayer!.SeasonTitle);

        shell.Roster.Roster.ShowStatisticsSetCommand.Execute(StatisticsSet.Playoffs);
        shell.Roster.Refresh();
        Assert.True(shell.Roster.Roster.ShowsPlayoffs);
    }

    [Fact]
    public void ACompletedSeasonCannotBeAdvancedButStaysBrowsable()
    {
        var shell = season.Shell;
        var session = shell.Session;
        var championId = session.Snapshot.Season.Playoffs!.ChampionId!.Value;
        var champion = session.GetTeam(championId).Name;

        Assert.True(session.Snapshot.Season.IsComplete);
        Assert.False(shell.AdvanceDayCommand.CanExecute(null));
        Assert.EndsWith($" · {champion} are champions", shell.CalendarLabel, StringComparison.Ordinal);
        Assert.Equal("Season complete", shell.ContinueDescription);
        Assert.Equal("Season complete", shell.Home.NextMatchTitle);
        Assert.Equal("Complete", shell.Home.Tiles[0].Value);
        var finalGame = Assert.Single(shell.Home.LatestResults);
        Assert.True(finalGame.OpenCommand.CanExecute(null));
        Assert.All(shell.Home.DivisionStandings, row => Assert.Equal(84, row.GamesPlayed));
        Assert.EndsWith("final standings", shell.Standings.Subtitle, StringComparison.Ordinal);
        Assert.All(shell.Standings.Tables.SelectMany(table => table.Rows), row => Assert.Equal(84, row.GamesPlayed));

        // The final standings decide every team: each wild-card race's top two clinched a spot and
        // the rest are eliminated.
        Assert.All(shell.Standings.Tables.SelectMany(table => table.Rows), row => Assert.NotEmpty(row.PlayoffMarker));
        shell.Standings.SelectScopeCommand.Execute(StandingsScope.WildCard);
        var wildCardRaces = shell.Standings.Tables.Where(table => table.Title.EndsWith("WILD CARD", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, wildCardRaces.Count);
        Assert.All(wildCardRaces, table =>
        {
            Assert.Equal(["x", "x"], table.Rows.Take(2).Select(row => row.PlayoffMarker));
            Assert.All(table.Rows.Skip(2), row => Assert.Equal("e", row.PlayoffMarker));
        });
        Assert.Equal(84, shell.Roster.Roster.Goalies.Sum(row => row.Season.GamesPlayed));
        Assert.All(shell.Schedule.Matches, match => Assert.True(match.IsCompleted));
        Assert.DoesNotContain(shell.Schedule.Matches, match => match.IsNext);

        // The bracket and the inbox announce the champion.
        Assert.True(shell.Playoffs.HasChampion);
        Assert.Equal($"{champion} · 2026–27 champions", shell.Playoffs.ChampionAnnouncement);
        Assert.EndsWith($"{champion} are champions", shell.Playoffs.Subtitle, StringComparison.Ordinal);
        Assert.All(shell.Playoffs.Rounds, round => Assert.All(round.Series, series => Assert.Contains(" win 4–", series.Status, StringComparison.Ordinal)));
        Assert.True(Assert.Single(shell.Playoffs.Rounds[^1].Series).Involves(championId));
        Assert.Equal($"Champions: the {champion}", session.Snapshot.Inbox[0].Subject);

        shell.Navigate(ShellPage.Roster);
        Assert.IsType<RosterPageViewModel>(shell.CurrentPage);
        shell.Navigate(ShellPage.PlayoffPicture);
        Assert.IsType<PlayoffsPageViewModel>(shell.CurrentPage);
        finalGame.OpenCommand.Execute(null);
        var schedule = Assert.IsType<SchedulePageViewModel>(shell.CurrentPage);
        Assert.NotNull(schedule.SelectedResult);
        Assert.Equal(SeasonPhase.Playoffs, schedule.SelectedMatch!.Phase);
    }

    /// <summary>
    /// Plays one season through the shell's Continue button, replacing injured players whenever
    /// it waits for that, and saves the game when the playoffs start and once the second round
    /// is under way.
    /// </summary>
    public sealed class PlayedSeason : IAsyncLifetime
    {
        // 84 rounds, every other day, finish 167 days after opening night, and the playoffs take
        // at most about two months more.
        private const int MaximumDays = 366;

        public GameShellViewModel Shell { get; private set; } = null!;

        /// <summary>The game after the final regular-season day, before any playoff game.</summary>
        public MemorySaveStore PlayoffsStart { get; } = new();

        /// <summary>The game once the first round is decided and a second-round game is played.</summary>
        public MemorySaveStore SecondRound { get; } = new();

        public GameSession Load(MemorySaveStore store)
        {
            var manager = new GameManager();
            manager.LoadGame(store);
            return new GameSession(manager, "Test Career", isSaved: true);
        }

        public async ValueTask InitializeAsync()
        {
            var session = GameTestData.StartSession();
            Shell = new GameShellViewModel(session);
            for (var day = 0; day < MaximumDays && !session.Snapshot.Season.IsComplete; day++)
            {
                if (Shell.HasPlayersToReplace)
                {
                    session.SetLineup(InjuredPlayerReplacement.ReplacingInjured(session.Snapshot));
                }

                await Shell.AdvanceDayCommand.ExecuteAsync(null);
                Assert.Null(Shell.AdvanceError);

                var playoffs = session.Snapshot.Season.Playoffs;
                if (playoffs is not null && PlayoffsStart.Save is null)
                {
                    session.SaveGame(PlayoffsStart, SaveName.Parse("Playoffs start"));
                }

                if (playoffs is { CurrentRound: PlayoffRound.SecondRound } && SecondRound.Save is null
                    && playoffs.Series.Any(series => series.Round == PlayoffRound.SecondRound && series.Games.Count > 0))
                {
                    session.SaveGame(SecondRound, SaveName.Parse("Second round"));
                }
            }

            Assert.True(session.Snapshot.Season.IsComplete);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Holds one save in memory.</summary>
    public sealed class MemorySaveStore : IGameSaveStore
    {
        public GameSave? Save { get; private set; }

        void IGameSaveStore.Save(GameSave save) => Save = save;

        public GameSave Load() => Save ?? throw new InvalidOperationException("Nothing is saved.");
    }
}