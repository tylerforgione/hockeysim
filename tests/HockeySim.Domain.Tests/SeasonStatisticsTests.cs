using Xunit;

using static HockeySim.Domain.Tests.TestResults;

namespace HockeySim.Domain.Tests;

public sealed class SeasonStatisticsTests
{
    private static readonly DateOnly OpeningDay = new(2026, 10, 1);

    private readonly League _league = TestLeague.Create();
    private readonly Team _first;
    private readonly Team _second;
    private readonly ScheduledMatch _openingFirst;
    private readonly ScheduledMatch _openingSecond;
    private readonly ScheduledMatch _rematch;
    private readonly Season _season;

    public SeasonStatisticsTests()
    {
        (_first, _second) = (_league.Teams[0], _league.Teams[1]);
        _openingFirst = new ScheduledMatch(OpeningDay, _first.Id, _second.Id);
        _openingSecond = new ScheduledMatch(OpeningDay, _league.Teams[2].Id, _league.Teams[3].Id);
        _rematch = new ScheduledMatch(OpeningDay.AddDays(1), _second.Id, _first.Id);
        _season = new Season(_league, new SeasonSchedule([_openingFirst, _openingSecond, _rematch]));
    }

    private PlayerId Scorer => _first.Lineup.ForwardLines[0].Centre.Id;

    private PlayerId FirstGoalie => _first.Lineup.StartingGoalie.Id;

    private PlayerId SecondGoalie => _second.Lineup.StartingGoalie.Id;

    /// <summary>The scorer's on-ice totals in each of these matches, and the first team's.</summary>
    private static SituationalShotTotals ScorerOnIce { get; } = new(
        Shots(attemptsFor: 10, attemptsAgainst: 6, unblockedFor: 8, unblockedAgainst: 5, shotsFor: 2, shotsAgainst: 4,
            expectedGoalsFor: 0.4, expectedGoalsAgainst: 0.5),
        Shots(attemptsFor: 3, shotsFor: 2, goalsFor: 1, expectedGoalsFor: 0.3),
        Shots(attemptsFor: 1, attemptsAgainst: 4, shotsFor: 1, shotsAgainst: 2, goalsFor: 1, expectedGoalsFor: 0.2),
        ShotTotals.None);

    [Fact]
    public void BeforeAnyMatchEveryRateAndPercentageIsUndefined()
    {
        var skater = new SkaterSeasonStatistics(Scorer, _first.Id);
        var goalie = new GoalieSeasonStatistics(FirstGoalie, _first.Id);

        Assert.Null(skater.FaceoffPercentage);
        Assert.Null(skater.TimeOnIcePerGame);
        Assert.Equal(SituationalShotTotals.None, skater.OnIce);
        Assert.Null(goalie.SavePercentage);
        Assert.Null(goalie.GoalsAgainstAverage);
        Assert.Null(goalie.TimeOnIcePerGame);
        Assert.Equal(0, goalie.GoalsSavedAboveExpected);
        Assert.Equal(_league.Teams.Select(team => team.Id), _season.TeamStatistics.Select(team => team.TeamId));
        Assert.All(_season.TeamStatistics, team =>
        {
            Assert.Equal(0, team.GamesPlayed);
            Assert.Null(team.PowerPlayPercentage);
            Assert.Null(team.PenaltyKillPercentage);
            Assert.Null(team.FaceoffPercentage);
            Assert.Null(team.ShotTotals.FiveOnFive.CorsiPercentage);
        });
    }

    [Fact]
    public void SkaterTotalsAccumulateEveryBoxScoreStatistic()
    {
        PlayBothMatches();

        var scorer = _season.SkaterStatistics.Single(skater => skater.PlayerId == Scorer);
        Assert.Equal(2, scorer.GamesPlayed);
        Assert.Equal(4, scorer.Goals);
        Assert.Equal(0, scorer.Assists);
        Assert.Equal(2, scorer.PlusMinus);
        Assert.Equal(new TimeSpan(0, 37, 1), scorer.TimeOnIce);
        Assert.Equal(10, scorer.Shots);
        Assert.Equal(18, scorer.ShotAttempts);
        Assert.Equal(6, scorer.Hits);
        Assert.Equal(2, scorer.BlockedShots);
        Assert.Equal(14, scorer.FaceoffsWon);
        Assert.Equal(6, scorer.FaceoffsLost);
        Assert.Equal(4, scorer.Takeaways);
        Assert.Equal(2, scorer.Giveaways);
        Assert.Equal(1.8, scorer.ExpectedGoals, precision: 10);
        Assert.Equal(4, scorer.PenaltyMinutes);
        Assert.Equal((2, 2, 0, 0), (scorer.PowerPlayGoals, scorer.ShorthandedGoals, scorer.PowerPlayAssists, scorer.ShorthandedAssists));
        Assert.Equal((2, 2), (scorer.PowerPlayPoints, scorer.ShorthandedPoints));
        Assert.Equal(0, scorer.EmptyNetGoals);
        AssertSameTotals(ScorerOnIce.Add(ScorerOnIce), scorer.OnIce);

        var assister = _season.SkaterStatistics.Single(skater => skater.PlayerId == _first.Lineup.ForwardLines[0].LeftWing.Id);
        Assert.Equal((2, 2, 4), (assister.PowerPlayAssists, assister.ShorthandedAssists, assister.Assists));
        Assert.Equal((2, 2), (assister.PowerPlayPoints, assister.ShorthandedPoints));
    }

    [Fact]
    public void SkaterRatesAreDerivedFromTheTotals()
    {
        PlayBothMatches();

        var scorer = _season.SkaterStatistics.Single(skater => skater.PlayerId == Scorer);

        Assert.Equal(0.7, scorer.FaceoffPercentage!.Value, precision: 10);

        // 37:01 over two games is 18:30.5, rounded up to the whole second.
        Assert.Equal(new TimeSpan(0, 18, 31), scorer.TimeOnIcePerGame);
        Assert.Equal(20 / 32.0, scorer.OnIce.FiveOnFive.CorsiPercentage!.Value, precision: 10);
        Assert.Equal(0.8 / 1.8, scorer.OnIce.FiveOnFive.ExpectedGoalsPercentage!.Value, precision: 10);
        Assert.Null(scorer.OnIce.FiveOnFive.GoalsPercentage);

        var assister = _season.SkaterStatistics.Single(skater => skater.PlayerId == _first.Lineup.ForwardLines[0].LeftWing.Id);
        Assert.Null(assister.FaceoffPercentage);
    }

    [Fact]
    public void GoalieTotalsAndRatesAccumulateFromEachStart()
    {
        PlayBothMatches();

        var shutoutGoalie = _season.GoalieStatistics.Single(goalie => goalie.PlayerId == FirstGoalie);
        Assert.Equal((2, 12, 0, 12), (shutoutGoalie.GamesPlayed, shutoutGoalie.ShotsAgainst, shutoutGoalie.GoalsAgainst, shutoutGoalie.Saves));
        Assert.Equal(2, shutoutGoalie.Shutouts);
        Assert.Equal(1.0, shutoutGoalie.SavePercentage);
        Assert.Equal(0, shutoutGoalie.GoalsAgainstAverage);
        Assert.Equal(1.0, shutoutGoalie.ExpectedGoalsAgainst, precision: 10);
        Assert.Equal(1.0, shutoutGoalie.GoalsSavedAboveExpected, precision: 10);
        Assert.Equal(TimeSpan.FromMinutes(120), shutoutGoalie.TimeOnIce);
        Assert.Equal(TimeSpan.FromMinutes(60), shutoutGoalie.TimeOnIcePerGame);

        // Four goals in 116 minutes in net (pulled for two minutes each match).
        var beatenGoalie = _season.GoalieStatistics.Single(goalie => goalie.PlayerId == SecondGoalie);
        Assert.Equal((2, 10, 4, 0), (beatenGoalie.GamesPlayed, beatenGoalie.ShotsAgainst, beatenGoalie.GoalsAgainst, beatenGoalie.Shutouts));
        Assert.Equal(0.6, beatenGoalie.SavePercentage!.Value, precision: 10);
        Assert.Equal(4 * 60 / 116.0, beatenGoalie.GoalsAgainstAverage!.Value, precision: 10);
        Assert.Equal(-2.2, beatenGoalie.GoalsSavedAboveExpected, precision: 10);
        Assert.Equal(TimeSpan.FromMinutes(58), beatenGoalie.TimeOnIcePerGame);
    }

    [Fact]
    public void NeitherAnEmptyNetGoalNorAShootoutPreventsAShutout()
    {
        // A 0-0 match decided in a shootout: the deciding goal is no player's and no goalie's.
        _season.CompleteDay([
            Create(_league, _openingFirst, 0, 0, MatchDecision.Shootout, shootoutWinnerIsHome: false),
            Create(_league, _openingSecond, 1, 0),
        ]);

        Assert.Equal(1, _season.GoalieStatistics.Single(goalie => goalie.PlayerId == FirstGoalie).Shutouts);
        Assert.Equal(1, _season.GoalieStatistics.Single(goalie => goalie.PlayerId == SecondGoalie).Shutouts);

        // The rematch is lost 0-1 to an empty-net goal scored while the first team's goalie was pulled.
        var winner = Side(_second, Skater(_second.Lineup.ForwardLines[0].Centre.Id, 1, shots: 1, emptyNetGoals: 1), Goalie(SecondGoalie, 0, 0));
        var loser = Side(_first, Skater(Scorer), Goalie(FirstGoalie, 0, 0));
        _season.CompleteDay([Match(_rematch, winner, loser)]);

        Assert.Equal(2, _season.GoalieStatistics.Single(goalie => goalie.PlayerId == FirstGoalie).Shutouts);
    }

    [Fact]
    public void TeamStatisticsAccumulateSpecialTeamsFaceoffsAndShotTotals()
    {
        PlayBothMatches();

        var first = _season.TeamStatistics.Single(team => team.TeamId == _first.Id);
        Assert.Equal(2, first.GamesPlayed);
        Assert.Equal((2, 2, 2, 0, 2), (first.PowerPlayGoals, first.PowerPlayOpportunities, first.TimesShorthanded, first.PowerPlayGoalsAgainst, first.ShorthandedGoals));
        Assert.Equal(1.0, first.PowerPlayPercentage);
        Assert.Equal(1.0, first.PenaltyKillPercentage);
        Assert.Equal((14, 6), (first.FaceoffsWon, first.FaceoffsLost));
        Assert.Equal(0.7, first.FaceoffPercentage!.Value, precision: 10);
        AssertSameTotals(ScorerOnIce.Add(ScorerOnIce), first.ShotTotals);

        // The second team killed two penalties and conceded on both, and had two power plays
        // that ended in shorthanded goals against.
        var second = _season.TeamStatistics.Single(team => team.TeamId == _second.Id);
        Assert.Equal(0, second.PowerPlayPercentage);
        Assert.Equal(0, second.PenaltyKillPercentage);
        Assert.Equal(0.3, second.FaceoffPercentage!.Value, precision: 10);
        AssertSameTotals(first.ShotTotals.Reverse(), second.ShotTotals);

        // A team that took no penalties and drew none still has its percentages undefined.
        var untouched = _season.TeamStatistics.Single(team => team.TeamId == _league.Teams[2].Id);
        Assert.Equal(1, untouched.GamesPlayed);
        Assert.Null(untouched.PowerPlayPercentage);
        Assert.Null(untouched.PenaltyKillPercentage);
        Assert.Null(untouched.FaceoffPercentage);
    }

    private void PlayBothMatches()
    {
        _season.CompleteDay([FirstTeamWins(_openingFirst, new TimeSpan(0, 18, 30)), Create(_league, _openingSecond, 2, 1)]);
        _season.CompleteDay([FirstTeamWins(_rematch, new TimeSpan(0, 18, 31))]);
    }

    /// <summary>
    /// The first team wins 2-0: the scorer scores once on the power play and once shorthanded,
    /// both assisted by their winger, and the first team's goalie records a shutout.
    /// </summary>
    private CompletedMatch FirstTeamWins(ScheduledMatch scheduled, TimeSpan scorerTimeOnIce)
    {
        var first = Side(
            _first,
            [
                Skater(
                    Scorer, 2, shots: 5, shotAttempts: 9, expectedGoals: 0.9, plusMinus: 1, blockedShots: 1,
                    faceoffsWon: 7, faceoffsLost: 3, penaltyMinutes: 2, powerPlayGoals: 1, shorthandedGoals: 1,
                    hits: 3, takeaways: 2, giveaways: 1, timeOnIce: scorerTimeOnIce, onIce: ScorerOnIce),
                Skater(_first.Lineup.ForwardLines[0].LeftWing.Id, 0, 2, shots: 0, shotAttempts: 8, powerPlayAssists: 1, shorthandedAssists: 1),
            ],
            Goalie(FirstGoalie, 6, 0, 0.5),
            powerPlayOpportunities: 1,
            ScorerOnIce);
        var second = Side(
            _second,
            [Skater(_second.Lineup.ForwardLines[0].Centre.Id, 0, shots: 6, shotAttempts: 10, expectedGoals: 0.5, faceoffsWon: 3, faceoffsLost: 7, penaltyMinutes: 2)],
            Goalie(SecondGoalie, 5, 2, 0.9, TimeSpan.FromMinutes(58)),
            powerPlayOpportunities: 1,
            ScorerOnIce.Reverse());

        return scheduled.HomeTeamId == _first.Id ? Match(scheduled, first, second) : Match(scheduled, second, first);
    }

    private static CompletedMatchTeam Side(
        Team team,
        SkaterBoxScore skater,
        GoalieBoxScore goalie) =>
        Side(team, [skater], goalie, 0, SituationalShotTotals.None);

    private static CompletedMatchTeam Side(
        Team team,
        IReadOnlyList<SkaterBoxScore> skaters,
        GoalieBoxScore goalie,
        int powerPlayOpportunities,
        SituationalShotTotals shotTotals) =>
        new(
            team.Id,
            skaters.Sum(skater => skater.Goals),
            skaters.Sum(skater => skater.Shots),
            powerPlayOpportunities,
            skaters,
            goalie,
            shotTotals);
}