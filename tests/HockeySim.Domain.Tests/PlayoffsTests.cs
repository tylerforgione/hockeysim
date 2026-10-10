using HockeySim.Domain;

using Xunit;

namespace HockeySim.Domain.Tests;

/// <summary>
/// Seeding, series, scheduling, and completion of the playoffs. The regular season is a round
/// robin that the stronger team always wins in regulation, so every team finishes on its own
/// points in the order <see cref="Strength"/> lists: the East's two wild cards both come from the
/// Atlantic and have more points than the Metro's winner, and each West wild card plays in the
/// other division's bracket.
/// </summary>
public sealed class PlayoffsTests
{
    private static readonly DateOnly OpeningDay = new(2026, 10, 1);

    private readonly League _league = TestLeague.Create();
    private readonly List<Team> _strength;
    private readonly Season _season;
    private readonly DateOnly _finalRegularSeasonDay;

    public PlayoffsTests()
    {
        _strength =
        [
            A(0), C(0), A(1), P(0), A(2), C(1), A(3), P(1), A(4), C(2), M(0), P(2), M(1), C(3), M(2), P(3),
            .. _league.Teams.Except([A(0), C(0), A(1), P(0), A(2), C(1), A(3), P(1), A(4), C(2), M(0), P(2), M(1), C(3), M(2), P(3)]),
        ];

        var games = new List<ScheduledMatch>();
        for (var better = 0; better < _strength.Count; better++)
        {
            for (var worse = better + 1; worse < _strength.Count; worse++)
            {
                games.Add(new ScheduledMatch(OpeningDay.AddDays(games.Count), _strength[better].Id, _strength[worse].Id));
            }
        }

        _season = new Season(_league, new SeasonSchedule(games));
        _finalRegularSeasonDay = games[^1].Date;
        while (!_season.IsRegularSeasonComplete)
        {
            _season.CompleteDay(_season.CurrentDateMatches.Select(match => TestResults.Create(_league, match, 1, 0)));
        }
    }

    private Playoffs Playoffs => _season.Playoffs!;

    [Fact]
    public void DivisionWinnersPlayTheWildCardsAndSecondPlaysThirdWithHomeIceByBracketPlace()
    {
        // The Atlantic winner has the better record, so it meets the lower wild card; the Metro
        // winner hosts the upper wild card, which has more points than it.
        Assert.Equal(
            [
                (A(0), A(4)), (A(1), A(2)), (M(0), A(3)), (M(1), M(2)),
                (C(0), P(3)), (C(1), C(2)), (P(0), C(3)), (P(1), P(2)),
            ],
            Playoffs.SeriesIn(PlayoffRound.FirstRound).Select(Teams));
        Assert.True(_season.RankStandings([A(3).Id, M(0).Id])[0].Record.TeamId == A(3).Id);

        var crossover = Playoffs.Series[2];
        Assert.Equal(new PlayoffSeed(M(0).Id, East, Metro, IsWildCard: false, Rank: 1), crossover.HigherRanked);
        Assert.Equal(new PlayoffSeed(A(3).Id, East, Atlantic, IsWildCard: true, Rank: 1), crossover.LowerRanked);
        Assert.Equal(PlayoffRound.FirstRound, Playoffs.CurrentRound);
        Assert.Null(Playoffs.Champion);
    }

    [Fact]
    public void ThePlayoffsBeginTwoDaysAfterTheFinalRegularSeasonDayWithEveryHigherRankedTeamAtHome()
    {
        Assert.Equal(SeasonPhase.Playoffs, _season.Phase);
        Assert.Empty(_season.CurrentDateMatches);
        _season.CompleteDay([]);

        var start = _finalRegularSeasonDay.AddDays(2);
        Assert.Equal(start, _season.CurrentDate);
        Assert.Equal(
            Playoffs.Series.Select(series => (series.HigherRanked.TeamId, series.LowerRanked.TeamId)),
            _season.CurrentDateMatches.Select(match => (match.HomeTeamId, match.AwayTeamId)));
        Assert.Equal(_season.CurrentDateMatches, Playoffs.Schedule);
    }

    [Fact]
    public void ASevenGameSeriesFollowsTheTwoTwoOneOneOnePatternEveryOtherDay()
    {
        // The higher-ranked team wins the odd-numbered games.
        var series = Playoffs.Series[0];
        PlayUntil(() => series.IsDecided, (current, game) => game % 2 == 1 ? current.HigherRanked : current.LowerRanked);

        var hosts = series.Games.Select(game => game.Home.TeamId == series.HigherRanked.TeamId ? 'H' : 'L');
        Assert.Equal("HHLLHLH", string.Concat(hosts));
        Assert.Equal(
            Enumerable.Range(0, 7).Select(game => _finalRegularSeasonDay.AddDays(2 + (2 * game))),
            series.Games.Select(game => game.Date));
        Assert.Equal(series.HigherRanked, series.Winner);
        Assert.Equal((4, 3), (series.HigherRankedWins, series.LowerRankedWins));
        Assert.Null(series.NextGame);
    }

    [Fact]
    public void ADecidedSeriesSchedulesNoMoreGamesWhileTheRestOfTheRoundPlaysOn()
    {
        var sweep = Playoffs.Series[0];
        var longest = Playoffs.Series[1];
        PlayUntil(
            () => longest.Games.Count == 6,
            (current, game) => current == sweep || game % 2 == 1 ? current.HigherRanked : current.LowerRanked);

        Assert.Equal(4, sweep.Games.Count);
        Assert.Null(sweep.NextGame);
        Assert.Equal(4, Playoffs.Schedule.Count(match => sweep.Involves(match.HomeTeamId)));
        Assert.Equal(PlayoffRound.FirstRound, Playoffs.CurrentRound);
        Assert.Equal(longest.Games[^1].Date.AddDays(2), longest.NextGame!.Date);
    }

    [Fact]
    public void TheNextRoundStartsTwoDaysAfterTheRoundsLastGameWithinEachBracket()
    {
        // Every higher-ranked team sweeps except the Metro winner, swept by its wild card, and
        // the Metro's second, which loses in seven.
        var upset = Playoffs.Series[2];
        var seventh = Playoffs.Series[3];
        PlayRound(
            series => series == upset || series == seventh ? series.LowerRanked : series.HigherRanked,
            gamesFor: series => series == seventh ? 7 : 4);

        var lastGame = seventh.Games[^1].Date;
        Assert.Equal(PlayoffRound.SecondRound, Playoffs.CurrentRound);
        Assert.All(Playoffs.SeriesIn(PlayoffRound.SecondRound), series => Assert.Equal(lastGame.AddDays(2), series.NextGame!.Date));

        // The Metro's third hosts the wild card, though the wild card has more points: through
        // the second round, home ice follows the bracket.
        Assert.Equal(
            [(A(0), A(1)), (M(2), A(3)), (C(0), C(1)), (P(0), P(1))],
            Playoffs.SeriesIn(PlayoffRound.SecondRound).Select(Teams));
    }

    [Fact]
    public void FromTheConferenceFinalHomeIceGoesToTheBetterRegularSeasonRecord()
    {
        // Every lower-ranked team wins the first round, so the wild cards and thirds advance.
        PlayRound(series => series.LowerRanked);
        var secondRound = Playoffs.SeriesIn(PlayoffRound.SecondRound);
        Assert.Equal(
            [(A(2), A(4)), (M(2), A(3)), (C(2), P(3)), (P(2), C(3))],
            secondRound.Select(Teams));

        // In the Atlantic and Central brackets the wild card wins again.
        PlayRound(series => series == secondRound[0] || series == secondRound[2] ? series.LowerRanked : series.HigherRanked);

        // A wild card hosts a division qualifier that finished with fewer points.
        Assert.Equal(
            [(A(4), M(2)), (P(2), P(3))],
            Playoffs.SeriesIn(PlayoffRound.ConferenceFinal).Select(Teams));

        PlayRound(series => series.LowerRanked);
        Assert.Equal([(M(2), P(3))], Playoffs.SeriesIn(PlayoffRound.Final).Select(Teams));
    }

    [Fact]
    public void TheFinalsWinnerIsChampionAndTheSeasonIsCompleteWithNoFurtherDays()
    {
        for (var round = 0; round < 4; round++)
        {
            Assert.False(_season.IsComplete);
            PlayRound(series => series.HigherRanked);
        }

        var final = Assert.Single(Playoffs.SeriesIn(PlayoffRound.Final));
        Assert.Equal((A(0), C(0)), Teams(final));
        Assert.Equal(final.HigherRanked, Playoffs.Champion);
        Assert.True(Playoffs.IsComplete);
        Assert.True(_season.IsComplete);
        Assert.Equal(15, Playoffs.Series.Count);
        Assert.Equal(15 * 4, Playoffs.CompletedMatches.Count);
        Assert.All(Playoffs.Series, series => Assert.Null(series.NextGame));

        var completedOn = final.Games[^1].Date.AddDays(1);
        Assert.Equal(completedOn, _season.CurrentDate);
        Assert.Throws<InvalidOperationException>(() => _season.CompleteDay([]));
        Assert.Equal(completedOn, _season.CurrentDate);
    }

    [Fact]
    public void APlayoffMatchCannotBeDecidedByAShootoutAndNothingIsApplied()
    {
        _season.CompleteDay([]);
        var matches = _season.CurrentDateMatches;
        var results = matches
            .Select((match, index) => index == 0
                ? TestResults.Create(_league, match, 2, 2, MatchDecision.Shootout)
                : TestResults.Create(_league, match, 1, 0))
            .ToList();

        Assert.Throws<ArgumentException>(() => _season.CompleteDay(results));
        Assert.Empty(Playoffs.CompletedMatches);
        Assert.Equal(matches, _season.CurrentDateMatches);

        _season.CompleteDay(matches.Select((match, index) =>
            TestResults.Create(_league, match, 3, 2, index == 0 ? MatchDecision.Overtime : MatchDecision.Regulation)));
        Assert.Equal(MatchDecision.Overtime, Playoffs.Series[0].Games[0].Decision);
    }

    [Fact]
    public void PlayoffRecordsAndStatisticsAreKeptApartFromTheRegularSeason()
    {
        var regularSeasonRecords = _season.TeamRecords;
        var regularSeasonSkaters = _season.SkaterStatistics;
        var standings = _season.RankStandings(_league.Teams.Select(team => team.Id));
        var statuses = _season.PlayoffStatuses();

        _season.CompleteDay([]);
        _season.CompleteDay(_season.CurrentDateMatches.Select(match => TestResults.Create(_league, match, 0, 3)));

        Assert.Equal(regularSeasonRecords, _season.TeamRecords);
        Assert.Equal(regularSeasonSkaters, _season.SkaterStatistics);
        Assert.Equal(standings, _season.RankStandings(_league.Teams.Select(team => team.Id)));
        Assert.Equal(statuses, _season.PlayoffStatuses());

        var qualifiers = Playoffs.Series.SelectMany(series => new[] { series.HigherRanked.TeamId, series.LowerRanked.TeamId });
        Assert.Equal(
            _league.Teams.Select(team => team.Id).Where(qualifiers.Contains),
            Playoffs.TeamRecords.Select(record => record.TeamId));
        Assert.All(Playoffs.TeamRecords, record => Assert.Equal(1, record.GamesPlayed));
        Assert.All(Playoffs.Series, series => Assert.Equal(1, series.LowerRankedWins));
        Assert.Equal(16, Playoffs.GoalieStatistics.Count);

        var scorer = Playoffs.SkaterStatistics.Single(skater => skater.PlayerId == TestResults.FirstSkater(A(4)));
        Assert.Equal((1, 3), (scorer.GamesPlayed, scorer.Goals));
        Assert.Equal(16, Playoffs.TeamStatistics.Count(team => team.GamesPlayed == 1));
    }

    [Fact]
    public void PlayoffInjuriesCountTowardsPlayerHealth()
    {
        _season.CompleteDay([]);
        var player = TestResults.FirstSkater(A(0));
        var game = _season.CurrentDateMatches[0];
        var injury = new MatchInjury(1, TimeSpan.FromMinutes(5), A(0).Id, player, InjuryType.Concussion, InjuryCause.Hit, 10);
        var injured = TestResults.Create(_league, game, 1, 0);
        injured = new CompletedMatch(
            game,
            injured.Home,
            injured.Away,
            injured.Decision,
            injured.Goals,
            injured.Penalties,
            new MatchHealthChanges([injury], []));

        _season.CompleteDay(_season.CurrentDateMatches.Select(match => match == game ? injured : TestResults.Create(_league, match, 1, 0)));

        Assert.False(_season.HealthOf(player).CanPlayOn(_season.CurrentDate));
    }

    private Conference East => _league.Conferences[0];

    private Division Atlantic => East.Divisions[0];

    private Division Metro => East.Divisions[1];

    private Team A(int index) => _league.Conferences[0].Divisions[0].Teams[index];

    private Team M(int index) => _league.Conferences[0].Divisions[1].Teams[index];

    private Team C(int index) => _league.Conferences[1].Divisions[0].Teams[index];

    private Team P(int index) => _league.Conferences[1].Divisions[1].Teams[index];

    private (Team, Team) Teams(PlayoffSeries series) =>
        (_league.Teams.Single(team => team.Id == series.HigherRanked.TeamId),
            _league.Teams.Single(team => team.Id == series.LowerRanked.TeamId));

    /// <summary>
    /// Plays league days until <paramref name="done"/>, each game won by the team
    /// <paramref name="winner"/> picks from its series and the game's number in it.
    /// </summary>
    private void PlayUntil(Func<bool> done, Func<PlayoffSeries, int, PlayoffSeed> winner)
    {
        while (!done())
        {
            _season.CompleteDay(_season.CurrentDateMatches.Select(match =>
            {
                var series = Playoffs.Series.Single(series => series.NextGame == match);
                return match.HomeTeamId == winner(series, series.Games.Count + 1).TeamId
                    ? TestResults.Create(_league, match, 2, 1)
                    : TestResults.Create(_league, match, 1, 2);
            }));
        }
    }

    /// <summary>
    /// Plays the current round to its end: each series won by <paramref name="winner"/>, in four
    /// games or, where <paramref name="gamesFor"/> says, in up to seven, the loser taking the
    /// games before.
    /// </summary>
    private void PlayRound(Func<PlayoffSeries, PlayoffSeed> winner, Func<PlayoffSeries, int>? gamesFor = null)
    {
        var round = Playoffs.CurrentRound;
        PlayUntil(
            () => Playoffs.CurrentRound != round || Playoffs.IsComplete,
            (series, game) =>
            {
                var seriesWinner = winner(series);
                var loser = seriesWinner == series.HigherRanked ? series.LowerRanked : series.HigherRanked;
                return game <= (gamesFor?.Invoke(series) ?? 4) - 4 ? loser : seriesWinner;
            });
    }
}