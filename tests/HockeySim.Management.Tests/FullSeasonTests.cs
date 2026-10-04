using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;

using Xunit;

using static HockeySim.Management.Tests.SeasonAdvancementTests;

namespace HockeySim.Management.Tests;

/// <summary>
/// Plays one complete regular season headlessly and checks the world is complete and consistent.
/// The season is played once and shared, because it is the slowest Management workflow.
/// </summary>
public sealed class FullSeasonTests(FullSeasonTests.CompletedSeason completed)
    : IClassFixture<FullSeasonTests.CompletedSeason>
{
    private SeasonSnapshot Season => completed.Snapshot.Season;

    [Fact]
    public void EveryScheduledMatchIsPlayedExactlyOnceInScheduleOrder()
    {
        Assert.True(Season.IsComplete);
        Assert.Equal(1344, Season.Results.Count);
        Assert.Equal(
            completed.Snapshot.Schedule.Matches.Select(match => (match.Date, match.HomeTeamId, match.AwayTeamId)),
            Season.Results.Select(result => (result.Date, result.Home.TeamId, result.Away.TeamId)));
    }

    [Fact]
    public void TheSeasonTakesOneAdvancePerCalendarDayAndEndsTheDayAfterTheFinalMatch()
    {
        var schedule = completed.Snapshot.Schedule.Matches;
        var lastMatchDate = schedule[^1].Date;

        Assert.Equal(lastMatchDate.DayNumber - schedule[0].Date.DayNumber + 1, completed.Advances);
        Assert.Equal(lastMatchDate.AddDays(1), Season.CurrentDate);
    }

    [Fact]
    public void EveryTeamPlays84AndItsRecordReconcilesWithItsResults()
    {
        Assert.All(Season.TeamRecords, record =>
        {
            var games = Season.Results
                .Where(result => result.Home.TeamId == record.TeamId || result.Away.TeamId == record.TeamId)
                .Select(result => (result.Decision, Team: Side(result, record.TeamId), Opponent: Opponent(result, record.TeamId)))
                .ToList();
            int Count(bool won, MatchDecision decision) =>
                games.Count(game => (game.Team.Score > game.Opponent.Score) == won && game.Decision == decision);

            Assert.Equal(84, record.GamesPlayed);
            Assert.Equal(Count(true, MatchDecision.Regulation), record.RegulationWins);
            Assert.Equal(Count(true, MatchDecision.Overtime), record.OvertimeWins);
            Assert.Equal(Count(true, MatchDecision.Shootout), record.ShootoutWins);
            Assert.Equal(Count(false, MatchDecision.Regulation), record.RegulationLosses);
            Assert.Equal(Count(false, MatchDecision.Overtime), record.OvertimeLosses);
            Assert.Equal(Count(false, MatchDecision.Shootout), record.ShootoutLosses);
            Assert.Equal((2 * record.Wins) + record.OvertimeLosses + record.ShootoutLosses, record.Points);
            Assert.Equal(games.Sum(game => game.Team.Score), record.GoalsFor);
            Assert.Equal(games.Sum(game => game.Opponent.Score), record.GoalsAgainst);
        });
    }

    [Fact]
    public void EveryKindOfOutcomeOccurs()
    {
        Assert.All(Enum.GetValues<MatchDecision>(), decision =>
            Assert.Contains(Season.Results, result => result.Decision == decision));
    }

    [Fact]
    public void LeagueWinsAndLossesBalance()
    {
        var records = Season.TeamRecords;

        Assert.Equal(1344, records.Sum(record => record.Wins));
        Assert.Equal(
            records.Sum(record => record.OvertimeWins + record.ShootoutWins),
            records.Sum(record => record.OvertimeLosses + record.ShootoutLosses));
        Assert.Equal(records.Sum(record => record.GoalsFor), records.Sum(record => record.GoalsAgainst));
    }

    [Fact]
    public void IndividualSeasonTotalsEqualTheSumOfTheirBoxScores()
    {
        var skaterBoxScores = Season.Results
            .SelectMany(result => new[] { result.Home, result.Away })
            .SelectMany(side => side.Skaters)
            .GroupBy(skater => skater.PlayerId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var goalieBoxScores = Season.Results
            .SelectMany(result => new[] { result.Home, result.Away })
            .GroupBy(side => side.Goalie.PlayerId)
            .ToDictionary(group => group.Key, group => group.Select(side => side.Goalie).ToList());

        Assert.Equal(skaterBoxScores.Keys.OrderBy(id => id.Value), Season.SkaterStatistics.Select(skater => skater.PlayerId).OrderBy(id => id.Value));
        Assert.All(Season.SkaterStatistics, skater =>
        {
            var boxScores = skaterBoxScores[skater.PlayerId];
            Assert.Equal(boxScores.Count, skater.GamesPlayed);
            Assert.Equal(boxScores.Sum(box => box.Goals), skater.Goals);
            Assert.Equal(boxScores.Sum(box => box.Assists), skater.Assists);
        });

        Assert.Equal(goalieBoxScores.Keys.OrderBy(id => id.Value), Season.GoalieStatistics.Select(goalie => goalie.PlayerId).OrderBy(id => id.Value));
        Assert.All(Season.GoalieStatistics, goalie =>
        {
            var boxScores = goalieBoxScores[goalie.PlayerId];
            Assert.Equal(boxScores.Count, goalie.GamesPlayed);
            Assert.Equal(boxScores.Sum(box => box.ShotsAgainst), goalie.ShotsAgainst);
            Assert.Equal(boxScores.Sum(box => box.GoalsAgainst), goalie.GoalsAgainst);
        });
    }

    [Fact]
    public void EachTeamsIndividualTotalsReconcileWithItsRecordExceptShootoutDecidingGoals()
    {
        Assert.All(Season.TeamRecords, record =>
        {
            var skaters = Season.SkaterStatistics.Where(skater => skater.TeamId == record.TeamId).ToList();
            var goalies = Season.GoalieStatistics.Where(goalie => goalie.TeamId == record.TeamId).ToList();

            Assert.Equal(84 * 18, skaters.Sum(skater => skater.GamesPlayed));
            Assert.Equal(84, goalies.Sum(goalie => goalie.GamesPlayed));
            Assert.Equal(record.GoalsFor - record.ShootoutWins, skaters.Sum(skater => skater.Goals));
            Assert.Equal(record.GoalsAgainst - record.ShootoutLosses, goalies.Sum(goalie => goalie.GoalsAgainst));
        });
    }

    [Fact]
    public void FinalStandingsRankEveryTableFromTheFullSeason()
    {
        StandingsTests.AssertTablesMatchTheLeague(completed.Snapshot);
        StandingsTests.AssertEveryTableIsRanked(Season.Standings);
        Assert.All(Season.Standings.League, entry =>
        {
            Assert.Equal(84, entry.Record.GamesPlayed);
            Assert.Equal(entry.Record.Points / 168.0, entry.Record.PointsPercentage);
        });
    }

    [Fact]
    public void ACompletedSeasonRejectsFurtherAdvancementWithoutChangingTheGame()
    {
        var manager = completed.Manager;
        var before = manager.GetSnapshot();

        Assert.Throws<InvalidOperationException>(() => manager.AdvanceDay());

        var after = manager.GetSnapshot();
        Assert.True(after.Season.IsComplete);
        Assert.Equal(before.Season.CurrentDate, after.Season.CurrentDate);
        Assert.Equal(before.RandomState, after.RandomState);
        Assert.Equal(Fingerprint(before), Fingerprint(after));
    }

    [Fact]
    public void ACompletedSeasonCanStillBeBrowsedAndManaged()
    {
        var manager = completed.Manager;
        var team = ManagedTeam(manager.GetSnapshot());

        var after = manager.SetLineup(CurrentLineup(team.Lineup));

        Assert.True(after.Season.IsComplete);
        Assert.Equal(1344, after.Season.Results.Count);
        Assert.Equal(32, after.League.Teams.Count);
    }

    private static CompletedMatchTeamSnapshot Side(CompletedMatchSnapshot result, TeamId teamId) =>
        result.Home.TeamId == teamId ? result.Home : result.Away;

    private static CompletedMatchTeamSnapshot Opponent(CompletedMatchSnapshot result, TeamId teamId) =>
        result.Home.TeamId == teamId ? result.Away : result.Home;

    public sealed class CompletedSeason
    {
        // Far more than the calendar needs, so a season that never completes fails instead of hanging.
        private const int MaximumAdvances = 366;

        public CompletedSeason()
        {
            Manager = new GameManager();
            var snapshot = StartGame(Manager, seed: 2026);
            while (!snapshot.Season.IsComplete && Advances < MaximumAdvances)
            {
                snapshot = Manager.AdvanceDay();
                Advances++;
            }

            Snapshot = snapshot;
        }

        public GameManager Manager { get; }

        public GameSnapshot Snapshot { get; }

        public int Advances { get; }
    }
}