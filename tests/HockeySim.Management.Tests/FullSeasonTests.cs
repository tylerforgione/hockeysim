using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;

using Xunit;

using static HockeySim.Management.Tests.SeasonAdvancementTests;

namespace HockeySim.Management.Tests;

/// <summary>
/// Plays one complete season, the regular season and the playoffs, headlessly and checks the world
/// is complete and consistent. The season is played once and shared, because it is the slowest
/// Management workflow.
/// </summary>
public sealed class FullSeasonTests(FullSeasonTests.CompletedSeason completed)
    : IClassFixture<FullSeasonTests.CompletedSeason>
{
    private SeasonSnapshot Season => completed.Snapshot.Season;

    [Fact]
    public void EveryScheduledMatchIsPlayedExactlyOnceInScheduleOrder()
    {
        Assert.True(Season.IsRegularSeasonComplete);
        Assert.Equal(1344, Season.Results.Count);
        Assert.Equal(
            completed.Snapshot.Schedule.Matches.Select(match => (match.Date, match.HomeTeamId, match.AwayTeamId)),
            Season.Results.Select(result => (result.Date, result.Home.TeamId, result.Away.TeamId)));
    }

    [Fact]
    public void TheSeasonTakesOneAdvancePerCalendarDayAndEndsTheDayAfterTheFinalPlayoffMatch()
    {
        var schedule = completed.Snapshot.Schedule.Matches;
        var lastMatchDate = completed.Snapshot.Schedule.PlayoffMatches[^1].Date;

        Assert.Equal(lastMatchDate.DayNumber - schedule[0].Date.DayNumber + 1, completed.Advances);
        Assert.Equal(lastMatchDate.AddDays(1), Season.CurrentDate);
        Assert.Equal(schedule[^1].Date.AddDays(1), completed.RegularSeasonEnd.Season.CurrentDate);
        Assert.Equal(SeasonPhase.Playoffs, completed.RegularSeasonEnd.Season.Phase);
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
    public void EveryBoxScoreAccountsForTheTimePlayed()
    {
        // Each team has three to five skaters a side, and six while its goalie is pulled for an
        // extra attacker during a delayed penalty, so its skaters share between three and six
        // times the time played. A goalie is off the ice only while pulled, which delayed penalties
        // and a late pull to tie the match can add up to several minutes.
        const int RegulationSeconds = 60 * 60;
        const int LongestTimePulled = 10 * 60;
        Assert.All(Season.Results, result =>
        {
            // The box score does not record when an overtime winner was scored, only that it was
            // within the five minutes.
            var (shortest, longest) = result.Decision switch
            {
                MatchDecision.Regulation => (RegulationSeconds, RegulationSeconds),
                MatchDecision.Shootout => (RegulationSeconds + (5 * 60), RegulationSeconds + (5 * 60)),
                _ => (RegulationSeconds, RegulationSeconds + (5 * 60)),
            };

            Assert.All(new[] { result.Home, result.Away }, side =>
            {
                Assert.InRange((int)side.Goalie.TimeOnIce.TotalSeconds, shortest - LongestTimePulled, longest);
                Assert.InRange(side.Skaters.Sum(skater => (int)skater.TimeOnIce.TotalSeconds), 3 * shortest, 6 * longest);
            });
        });
    }

    [Fact]
    public void SpecialTeamsStatisticsReconcileAcrossTheSeason()
    {
        var sides = Season.Results.SelectMany(result => new[] { (Team: result.Home, Opponent: result.Away), (Team: result.Away, Opponent: result.Home) }).ToList();

        Assert.All(sides, pair =>
        {
            Assert.True(pair.Team.PowerPlayGoals == 0 || pair.Team.PowerPlayOpportunities > 0);
            Assert.True(pair.Team.Skaters.Sum(skater => skater.ShorthandedGoals) == 0 || pair.Opponent.PowerPlayOpportunities > 0);
            Assert.All(pair.Team.Skaters, skater =>
            {
                Assert.InRange(skater.PowerPlayGoals + skater.ShorthandedGoals, 0, skater.Goals);
                Assert.InRange(skater.PowerPlayAssists + skater.ShorthandedAssists, 0, skater.Assists);
            });
        });
        Assert.Contains(sides, pair => pair.Team.PowerPlayGoals > 0);
        Assert.Contains(sides, pair => pair.Team.Skaters.Any(skater => skater.ShorthandedGoals > 0));
        Assert.Contains(sides, pair => pair.Team.PenaltyMinutes > 0);
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
            Assert.Equal(boxScores.Sum(box => box.PlusMinus), skater.PlusMinus);
            Assert.Equal(boxScores.Aggregate(TimeSpan.Zero, (total, box) => total + box.TimeOnIce), skater.TimeOnIce);
            Assert.Equal(boxScores.Sum(box => box.Shots), skater.Shots);
            Assert.Equal(boxScores.Sum(box => box.ShotAttempts), skater.ShotAttempts);
            Assert.Equal(boxScores.Sum(box => box.Hits), skater.Hits);
            Assert.Equal(boxScores.Sum(box => box.BlockedShots), skater.BlockedShots);
            Assert.Equal(boxScores.Sum(box => box.FaceoffsWon), skater.FaceoffsWon);
            Assert.Equal(boxScores.Sum(box => box.FaceoffsLost), skater.FaceoffsLost);
            Assert.Equal(boxScores.Sum(box => box.Takeaways), skater.Takeaways);
            Assert.Equal(boxScores.Sum(box => box.Giveaways), skater.Giveaways);
            Assert.Equal(boxScores.Sum(box => box.ExpectedGoals), skater.ExpectedGoals, precision: 9);
            Assert.Equal(boxScores.Sum(box => box.PenaltyMinutes), skater.PenaltyMinutes);
            Assert.Equal(boxScores.Sum(box => box.PowerPlayGoals), skater.PowerPlayGoals);
            Assert.Equal(boxScores.Sum(box => box.PowerPlayAssists), skater.PowerPlayAssists);
            Assert.Equal(boxScores.Sum(box => box.ShorthandedGoals), skater.ShorthandedGoals);
            Assert.Equal(boxScores.Sum(box => box.ShorthandedAssists), skater.ShorthandedAssists);
            Assert.Equal(boxScores.Sum(box => box.EmptyNetGoals), skater.EmptyNetGoals);
            Assert.Equal(boxScores.Sum(box => box.OnIce.FiveOnFive.AttemptsFor), skater.OnIce.FiveOnFive.AttemptsFor);
            Assert.Equal(boxScores.Sum(box => box.OnIce.PenaltyKill.AttemptsAgainst), skater.OnIce.PenaltyKill.AttemptsAgainst);
            Assert.Equal(boxScores.Sum(box => box.OnIce.PowerPlay.ExpectedGoalsFor), skater.OnIce.PowerPlay.ExpectedGoalsFor, precision: 9);
            Assert.Equal(boxScores.Sum(box => box.OnIce.Other.GoalsAgainst), skater.OnIce.Other.GoalsAgainst);
        });

        Assert.Equal(goalieBoxScores.Keys.OrderBy(id => id.Value), Season.GoalieStatistics.Select(goalie => goalie.PlayerId).OrderBy(id => id.Value));
        Assert.All(Season.GoalieStatistics, goalie =>
        {
            var boxScores = goalieBoxScores[goalie.PlayerId];
            Assert.Equal(boxScores.Count, goalie.GamesPlayed);
            Assert.Equal(boxScores.Sum(box => box.ShotsAgainst), goalie.ShotsAgainst);
            Assert.Equal(boxScores.Sum(box => box.GoalsAgainst), goalie.GoalsAgainst);
            Assert.Equal(boxScores.Sum(box => box.ExpectedGoalsAgainst), goalie.ExpectedGoalsAgainst, precision: 9);
            Assert.Equal(boxScores.Aggregate(TimeSpan.Zero, (total, box) => total + box.TimeOnIce), goalie.TimeOnIce);
            Assert.Equal(boxScores.Count(box => box.GoalsAgainst == 0), goalie.Shutouts);
        });
        Assert.Contains(Season.GoalieStatistics, goalie => goalie.Shutouts > 0);
    }

    [Fact]
    public void TeamStatisticsReconcileWithTheirResults()
    {
        Assert.All(Season.TeamStatistics, statistics =>
        {
            var games = Season.Results
                .Where(result => result.Home.TeamId == statistics.TeamId || result.Away.TeamId == statistics.TeamId)
                .Select(result => (Team: Side(result, statistics.TeamId), Opponent: Opponent(result, statistics.TeamId)))
                .ToList();

            Assert.Equal(84, statistics.GamesPlayed);
            Assert.Equal(games.Sum(game => game.Team.PowerPlayGoals), statistics.PowerPlayGoals);
            Assert.Equal(games.Sum(game => game.Team.PowerPlayOpportunities), statistics.PowerPlayOpportunities);
            Assert.Equal(games.Sum(game => game.Opponent.PowerPlayOpportunities), statistics.TimesShorthanded);
            Assert.Equal(games.Sum(game => game.Opponent.PowerPlayGoals), statistics.PowerPlayGoalsAgainst);
            Assert.Equal(games.Sum(game => game.Team.Skaters.Sum(skater => skater.FaceoffsWon)), statistics.FaceoffsWon);
            Assert.Equal(games.Sum(game => game.Team.Skaters.Sum(skater => skater.FaceoffsLost)), statistics.FaceoffsLost);
            Assert.Equal(games.Sum(game => game.Team.ShotTotals.FiveOnFive.AttemptsFor), statistics.ShotTotals.FiveOnFive.AttemptsFor);
            Assert.Equal(games.Sum(game => game.Opponent.ShotTotals.FiveOnFive.AttemptsFor), statistics.ShotTotals.FiveOnFive.AttemptsAgainst);
            Assert.Equal(
                statistics.PowerPlayGoals / (double)statistics.PowerPlayOpportunities,
                statistics.PowerPlayPercentage!.Value,
                precision: 12);
            Assert.Equal(
                1 - (statistics.PowerPlayGoalsAgainst / (double)statistics.TimesShorthanded),
                statistics.PenaltyKillPercentage!.Value,
                precision: 12);
        });

        // Every attempt for one team is an attempt against another.
        Assert.Equal(
            Season.TeamStatistics.Sum(team => team.ShotTotals.All.AttemptsFor),
            Season.TeamStatistics.Sum(team => team.ShotTotals.All.AttemptsAgainst));
        Assert.Equal(
            Season.TeamStatistics.Sum(team => team.ShotTotals.PowerPlay.GoalsFor),
            Season.TeamStatistics.Sum(team => team.ShotTotals.PenaltyKill.GoalsAgainst));
    }

    [Fact]
    public void EveryMatchSummaryAccountsForItsPlayerGoalsAndPenaltyMinutes()
    {
        Assert.All(Season.Results, result =>
        {
            foreach (var side in new[] { result.Home, result.Away })
            {
                Assert.Equal(side.Skaters.Sum(skater => skater.Goals), result.Goals.Count(goal => goal.TeamId == side.TeamId));
                Assert.Equal(side.PenaltyMinutes, result.Penalties.Where(penalty => penalty.TeamId == side.TeamId).Sum(penalty => penalty.Minutes));
            }

            Assert.Equal(
                result.Goals.Select(goal => (goal.Period, goal.TimeInPeriod)).Order(),
                result.Goals.Select(goal => (goal.Period, goal.TimeInPeriod)));
        });

        var goals = Season.Results.SelectMany(result => result.Goals).ToList();
        Assert.All(Enum.GetValues<GoalSituation>(), situation => Assert.Contains(goals, goal => goal.Situation == situation));
        Assert.Contains(goals, goal => goal.IsEmptyNet);
        Assert.Contains(goals, goal => goal.Period == 4);
        Assert.Contains(Season.Results.SelectMany(result => result.Penalties), penalty => penalty.Kind == PenaltyKind.PenaltyShot);
    }

    [Fact]
    public void EachTeamsIndividualTotalsReconcileWithItsRecordExceptShootoutDecidingGoals()
    {
        Assert.All(Season.TeamRecords, record =>
        {
            var skaters = Season.SkaterStatistics.Where(skater => skater.TeamId == record.TeamId).ToList();
            var goalies = Season.GoalieStatistics.Where(goalie => goalie.TeamId == record.TeamId).ToList();

            // A dressed skater who cannot play through an injury misses the match; the injury cap
            // leaves at most three of a team's 21 skaters unable to play.
            var appearances = Season.Results
                .SelectMany(result => new[] { result.Home, result.Away })
                .Where(side => side.TeamId == record.TeamId)
                .Sum(side => side.Skaters.Count);
            Assert.Equal(appearances, skaters.Sum(skater => skater.GamesPlayed));
            Assert.InRange(appearances, 84 * (18 - 3), 84 * 18);
            Assert.Equal(84, goalies.Sum(goalie => goalie.GamesPlayed));
            Assert.Equal(record.GoalsFor - record.ShootoutWins, skaters.Sum(skater => skater.Goals));

            // Goals into an empty net count against the team but not its goalie.
            var emptyNetGoalsAgainst = Season.Results
                .Where(result => result.Home.TeamId == record.TeamId || result.Away.TeamId == record.TeamId)
                .Sum(result => Opponent(result, record.TeamId).Skaters.Sum(skater => skater.EmptyNetGoals));
            Assert.Equal(record.GoalsAgainst - record.ShootoutLosses - emptyNetGoalsAgainst, goalies.Sum(goalie => goalie.GoalsAgainst));
        });

        Assert.Contains(Season.Results, result => result.Home.Skaters.Concat(result.Away.Skaters).Any(skater => skater.EmptyNetGoals > 0));
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

    [Fact]
    public void TheFinalStandingsDecideEveryTeamWithEightQualifiersPerConference()
    {
        var standings = Season.Standings;
        var statuses = standings.League.ToDictionary(entry => entry.Record.TeamId, entry => entry.PlayoffStatus);

        Assert.DoesNotContain(PlayoffStatus.Undecided, statuses.Values);
        Assert.Single(statuses.Values, status => status == PlayoffStatus.ClinchedBestRecord);
        foreach (var view in standings.WildCard)
        {
            var qualifiers = view.DivisionLeaders
                .SelectMany(division => division.Teams)
                .Concat(view.WildCardRace.Take(view.WildCardCount))
                .Select(entry => entry.Record.TeamId)
                .ToList();
            Assert.Equal(8, qualifiers.Count);
            Assert.All(qualifiers, teamId => Assert.True(statuses[teamId] >= PlayoffStatus.ClinchedPlayoffSpot));
            Assert.All(view.WildCardRace.Skip(view.WildCardCount), entry => Assert.Equal(PlayoffStatus.Eliminated, entry.PlayoffStatus));
            Assert.All(view.DivisionLeaders, division => Assert.True(division.Teams[0].PlayoffStatus >= PlayoffStatus.ClinchedDivision));
        }

        // Every table carries the same status for a team.
        var everyEntry = standings.Conferences
            .SelectMany(conference => conference.Teams.Concat(conference.Divisions.SelectMany(division => division.Teams)))
            .Concat(standings.WildCard.SelectMany(view =>
                view.WildCardRace.Concat(view.DivisionLeaders.SelectMany(division => division.Teams))));
        Assert.All(everyEntry, entry => Assert.Equal(statuses[entry.Record.TeamId], entry.PlayoffStatus));
    }

    [Fact]
    public void EveryClinchOrEliminationDuringTheSeasonHoldsUntilTheEnd()
    {
        var daily = completed.DailyPlayoffStatuses;
        var final = daily[^1];

        foreach (var (statuses, nextDay) in daily.Zip(daily.Skip(1)))
        {
            foreach (var (teamId, status) in statuses)
            {
                if (status == PlayoffStatus.Eliminated)
                {
                    Assert.Equal(PlayoffStatus.Eliminated, nextDay[teamId]);
                    Assert.Equal(PlayoffStatus.Eliminated, final[teamId]);
                }
                else if (status != PlayoffStatus.Undecided)
                {
                    // A clinch can only be strengthened, never withdrawn.
                    Assert.True(nextDay[teamId] >= status);
                    Assert.True(final[teamId] >= status);
                }
            }
        }

        // Guarantees appear before the final day rather than only once the regular season is over.
        var beforeTheEnd = daily[^2];
        Assert.Contains(beforeTheEnd.Values, status => status >= PlayoffStatus.ClinchedPlayoffSpot);
        Assert.Contains(beforeTheEnd.Values, status => status == PlayoffStatus.Eliminated);
    }

    [Fact]
    public void TheFinalStandingsQualifiersPlayFifteenSeriesUntilAChampionIsCrowned()
    {
        var playoffs = Season.Playoffs!;
        var series = playoffs.Series;

        var qualifiers = Season.Standings.WildCard
            .SelectMany(view => view.DivisionLeaders.SelectMany(division => division.Teams)
                .Concat(view.WildCardRace.Take(view.WildCardCount)))
            .Select(entry => entry.Record.TeamId);
        Assert.Equal(
            qualifiers.OrderBy(id => id.Value),
            series.Where(series => series.Round == PlayoffRound.FirstRound)
                .SelectMany(series => new[] { series.HigherRanked.TeamId, series.LowerRanked.TeamId })
                .OrderBy(id => id.Value));

        Assert.Equal([8, 4, 2, 1], Enum.GetValues<PlayoffRound>().Select(round => series.Count(series => series.Round == round)));
        Assert.All(series, series =>
        {
            Assert.InRange(series.Games.Count, 4, 7);
            Assert.Equal(4, Math.Max(series.HigherRankedWins, series.LowerRankedWins));
            Assert.Equal(series.Games.Count, series.HigherRankedWins + series.LowerRankedWins);
            Assert.Null(series.NextGame);
            Assert.Equal(series.HigherRankedWins == 4 ? series.HigherRanked.TeamId : series.LowerRanked.TeamId, series.WinnerId);
        });

        // Each later series is between winners of the round before.
        foreach (var round in Enum.GetValues<PlayoffRound>().Skip(1))
        {
            var winners = series.Where(series => series.Round == round - 1).Select(series => series.WinnerId!.Value).ToHashSet();
            Assert.All(series.Where(series => series.Round == round), series =>
                Assert.True(winners.Contains(series.HigherRanked.TeamId) && winners.Contains(series.LowerRanked.TeamId)));
        }

        Assert.True(Season.IsComplete);
        Assert.Equal(series[^1].WinnerId, playoffs.ChampionId);
        Assert.Equal(PlayoffRound.Final, playoffs.CurrentRound);
    }

    [Fact]
    public void EverySeriesFollowsTheHomeIcePatternEveryOtherDayAndRoundsFollowTwoDaysApart()
    {
        var playoffs = Season.Playoffs!;
        var regularSeasonEnd = completed.Snapshot.Schedule.Matches[^1].Date;

        Assert.All(playoffs.Series, series =>
        {
            var hosts = string.Concat(series.Games.Select(game => game.Home.TeamId == series.HigherRanked.TeamId ? 'H' : 'L'));
            Assert.StartsWith(hosts, "HHLLHLH", StringComparison.Ordinal);
            Assert.All(series.Games.Zip(series.Games.Skip(1)), pair => Assert.Equal(pair.First.Date.AddDays(2), pair.Second.Date));
        });

        var roundStarts = Enum.GetValues<PlayoffRound>()
            .Select(round => playoffs.Series.Where(series => series.Round == round).ToList())
            .Select(round => (Start: round.Min(series => series.Games[0].Date), End: round.Max(series => series.Games[^1].Date)))
            .ToList();
        Assert.Equal(regularSeasonEnd.AddDays(2), roundStarts[0].Start);
        Assert.All(roundStarts.Zip(roundStarts.Skip(1)), pair => Assert.Equal(pair.First.End.AddDays(2), pair.Second.Start));
        Assert.All(playoffs.Series, series => Assert.Equal(
            roundStarts[(int)series.Round].Start,
            series.Games[0].Date));
    }

    [Fact]
    public void PlayoffMatchesAreDecidedWithoutAShootoutAndOvertimeGoalsEndThem()
    {
        var results = Season.Playoffs!.Results;

        Assert.Equal(
            completed.Snapshot.Schedule.PlayoffMatches.Select(match => (match.Date, match.HomeTeamId, match.AwayTeamId)),
            results.Select(result => (result.Date, result.Home.TeamId, result.Away.TeamId)));
        Assert.DoesNotContain(results, result => result.Decision == MatchDecision.Shootout);
        Assert.All(results.Where(result => result.Decision == MatchDecision.Overtime), result =>
        {
            var winningGoal = result.Goals[^1];
            Assert.True(winningGoal.Period >= 4);
            Assert.Equal(result.WinnerId, winningGoal.TeamId);
            Assert.Equal(1, Math.Abs(result.Home.Score - result.Away.Score));
        });
    }

    [Fact]
    public void PlayoffRecordsAndStatisticsReconcileWithPlayoffResultsAndLeaveTheRegularSeasonAlone()
    {
        var playoffs = Season.Playoffs!;
        var regularSeason = completed.RegularSeasonEnd.Season;

        Assert.Equal(regularSeason.TeamRecords, Season.TeamRecords);
        Assert.Equal(regularSeason.SkaterStatistics, Season.SkaterStatistics);
        Assert.Equal(regularSeason.GoalieStatistics, Season.GoalieStatistics);
        Assert.Equal(regularSeason.Standings.League, Season.Standings.League);

        Assert.Equal(16, playoffs.TeamRecords.Count);
        Assert.All(playoffs.TeamRecords, record =>
        {
            var games = playoffs.Results.Where(result => result.Home.TeamId == record.TeamId || result.Away.TeamId == record.TeamId).ToList();
            Assert.Equal(games.Count, record.GamesPlayed);
            Assert.Equal(games.Count(game => game.WinnerId == record.TeamId), record.Wins);
            Assert.Equal(0, record.ShootoutWins + record.ShootoutLosses);
            Assert.Equal(games.Sum(game => Side(game, record.TeamId).Score), record.GoalsFor);
        });

        var boxScores = playoffs.Results.SelectMany(result => result.Home.Skaters.Concat(result.Away.Skaters)).ToList();
        Assert.All(playoffs.SkaterStatistics, skater =>
        {
            var games = boxScores.Where(boxScore => boxScore.PlayerId == skater.PlayerId).ToList();
            Assert.Equal(games.Count, skater.GamesPlayed);
            Assert.Equal(games.Sum(game => game.Goals), skater.Goals);
            Assert.Equal(games.Sum(game => game.Assists), skater.Assists);
        });
        Assert.Equal(boxScores.Select(boxScore => boxScore.PlayerId).Distinct().Count(), playoffs.SkaterStatistics.Count);
        Assert.Equal(
            playoffs.Results.Sum(result => result.Home.Goalie.ShotsAgainst + result.Away.Goalie.ShotsAgainst),
            playoffs.GoalieStatistics.Sum(goalie => goalie.ShotsAgainst));
    }

    public sealed class CompletedSeason
    {
        // Far more than the calendar needs, so a season that never completes fails instead of hanging.
        private const int MaximumAdvances = 366;

        private readonly List<IReadOnlyDictionary<TeamId, PlayoffStatus>> _dailyPlayoffStatuses = [];

        public CompletedSeason()
        {
            Manager = new GameManager();
            var snapshot = StartAtOpeningDay(Manager, seed: 2026);
            while (!snapshot.Season.IsComplete && Advances < MaximumAdvances)
            {
                var regularSeasonDay = !snapshot.Season.IsRegularSeasonComplete;
                snapshot = Manager.AdvanceDayReplacingInjured();
                Advances++;
                if (regularSeasonDay)
                {
                    _dailyPlayoffStatuses.Add(snapshot.Season.Standings.League
                        .ToDictionary(entry => entry.Record.TeamId, entry => entry.PlayoffStatus));
                    RegularSeasonEnd = snapshot;
                }
            }

            Snapshot = snapshot;
        }

        public GameManager Manager { get; }

        /// <summary>The game once the champion is crowned.</summary>
        public GameSnapshot Snapshot { get; }

        /// <summary>The game after the final regular-season day, before any playoff game.</summary>
        public GameSnapshot RegularSeasonEnd { get; } = null!;

        public int Advances { get; }

        /// <summary>
        /// Every team's playoff status after each regular-season day, the last being the final one.
        /// </summary>
        public IReadOnlyList<IReadOnlyDictionary<TeamId, PlayoffStatus>> DailyPlayoffStatuses => _dailyPlayoffStatuses;
    }
}