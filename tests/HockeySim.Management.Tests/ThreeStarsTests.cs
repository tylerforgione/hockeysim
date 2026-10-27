using HockeySim.Domain;
using HockeySim.Management.Matches;

using Xunit;

namespace HockeySim.Management.Tests;

public sealed class ThreeStarsTests
{
    private static readonly TeamId Home = new(Guid.Parse("00000000-0000-0000-0000-00000000000a"));
    private static readonly TeamId Away = new(Guid.Parse("00000000-0000-0000-0000-00000000000b"));
    private static readonly PlayerId HomeGoalie = Player(90);
    private static readonly PlayerId AwayGoalie = Player(91);

    [Fact]
    public void ASkatersGameScoreWeighsEachContribution()
    {
        // 1.0 a goal, 0.5 an empty-net goal, 0.7 a primary assist, 0.4 a secondary, 0.1 a shot or
        // block, 0.05 a hit or takeaway, less 0.05 a giveaway.
        var scorer = Player(1);
        var passer = Player(2);
        var trailer = Player(3);
        var match = Build(
            [
                new Line(scorer, Home, Shots: 3, Hits: 2, Takeaways: 1, Giveaways: 1),
                new Line(passer, Home, Shots: 1, Blocks: 2),
                new Line(trailer, Home),
                new Line(Player(4), Away, Shots: 10),
            ],
            [
                Goal(Home, scorer, passer, trailer),
                Goal(Home, passer, scorer, isEmptyNet: true),
            ]);

        Assert.Equal(1.0 + 0.7 + 0.3 + 0.1 + 0.05 - 0.05 + 0.5, ScoreOf(match, scorer), precision: 10);
        Assert.Equal(0.5 + 0.7 + 0.1 + 0.2, ScoreOf(match, passer), precision: 10);
        Assert.Equal(0.4, ScoreOf(match, trailer), precision: 10);
    }

    [Fact]
    public void TheGameWinningGoalEarnsABonus()
    {
        // Two goals, one of them the winner, on five shots: 3.0.
        var scorer = Player(1);
        var match = Build(
            [new Line(scorer, Home, Shots: 5), new Line(Player(2), Away, Shots: 1)],
            [Goal(Away, Player(2)), Goal(Home, scorer), Goal(Home, scorer)]);

        Assert.Equal(3.0, ScoreOf(match, scorer), precision: 10);
    }

    [Fact]
    public void OnIceExpectedGoalsForLessAgainstCountsInEverySituation()
    {
        // One goal, one primary assist, four shots, and half an expected goal ahead on the ice: 2.475.
        var skater = Player(1);
        var onIce = new SituationalShotTotals(
            Shots(expectedGoalsFor: 0.75, expectedGoalsAgainst: 0.5),
            Shots(expectedGoalsFor: 0.25),
            ShotTotals.None,
            ShotTotals.None);
        var match = Build(
            [
                new Line(skater, Home, Shots: 4, ExpectedGoals: 1.0, OnIce: onIce),
                new Line(Player(2), Home, ExpectedGoals: 1.0),
                new Line(Player(3), Away, ExpectedGoals: 0.5),
            ],
            [Goal(Home, skater), Goal(Home, Player(2), skater), Goal(Away, Player(3))],
            homeShotTotals: onIce);

        Assert.Equal(1.0 + 0.7 + 0.4 + (0.5 * 0.75), ScoreOf(match, skater), precision: 10);
    }

    [Fact]
    public void AGoaliesGameScoreIsGoalsSavedAboveExpectedSavesAndAShutout()
    {
        // A 30-save shutout on 2.5 expected goals: 4.0. Twenty-eight saves on three goals against
        // three expected: 0.7.
        var shutout = Build(
            [new Line(Player(1), Home, Shots: 31, ExpectedGoals: 3.0), new Line(Player(2), Away, Shots: 30, ExpectedGoals: 2.5)],
            [Goal(Home, Player(1))]);
        var beaten = Build(
            [new Line(Player(1), Home, Shots: 31, ExpectedGoals: 3.0), new Line(Player(2), Away, Shots: 30, ExpectedGoals: 2.5)],
            [Goal(Home, Player(1)), Goal(Home, Player(1)), Goal(Home, Player(1))]);

        Assert.Equal(4.0, ScoreOf(shutout, HomeGoalie), precision: 10);
        Assert.Equal(0.7, ScoreOf(beaten, AwayGoalie), precision: 10);
    }

    [Fact]
    public void AnEmptyNetGoalAgainstPreventsAShutout()
    {
        var match = Build(
            [new Line(Player(1), Home, Shots: 20), new Line(Player(2), Away, Shots: 20)],
            [Goal(Home, Player(1)), Goal(Home, Player(1)), Goal(Away, Player(2), isEmptyNet: true)]);

        Assert.Equal(0, match.Home.Goalie.GoalsAgainst);
        Assert.Equal(19 * 0.025, ScoreOf(match, HomeGoalie), precision: 10);
    }

    [Fact]
    public void TheGameWinnerPutTheWinnersOneAheadOfTheLosersFinalScore()
    {
        var regulation = Build(
            [new Line(Player(1), Home, Shots: 9), new Line(Player(2), Away, Shots: 9)],
            [Goal(Home, Player(1)), Goal(Away, Player(2)), Goal(Away, Player(2)), Goal(Home, Player(1)), Goal(Home, Player(1), period: 3), Goal(Home, Player(1), period: 3)]);
        var overtime = Build(
            [new Line(Player(1), Home, Shots: 9), new Line(Player(2), Away, Shots: 9)],
            [Goal(Away, Player(2)), Goal(Home, Player(1)), Goal(Home, Player(1), period: 4)],
            MatchDecision.Overtime);
        var shootout = Build(
            [new Line(Player(1), Home, Shots: 9), new Line(Player(2), Away, Shots: 9)],
            [Goal(Away, Player(2)), Goal(Home, Player(1))],
            MatchDecision.Shootout);

        Assert.Same(regulation.Goals[4], ThreeStars.GameWinningGoal(regulation));
        Assert.Same(overtime.Goals[2], ThreeStars.GameWinningGoal(overtime));
        Assert.Null(ThreeStars.GameWinningGoal(shootout));
    }

    [Fact]
    public void TheStarsAreTheThreeHighestGameScores()
    {
        var match = Build(
            [
                new Line(Player(1), Home, Shots: 2),
                new Line(Player(2), Home, Shots: 9),
                new Line(Player(3), Home),
                new Line(Player(4), Away, Shots: 4),
            ],
            [Goal(Home, Player(1)), Goal(Home, Player(1)), Goal(Away, Player(4))]);

        var stars = ThreeStars.Choose(match);

        Assert.Equal(ThreeStars.Rank(match).Take(3), stars);
        Assert.Equal([Player(1), Player(4), Player(2)], stars.Select(star => star.PlayerId));
    }

    [Fact]
    public void TheWinnersBestPlayerReplacesTheThirdStarWhenNoneOfTheTopThreeWon()
    {
        var match = Build(
            [
                new Line(Player(1), Away, Shots: 30),
                new Line(Player(2), Away, Shots: 30),
                new Line(Player(3), Away, Shots: 30),
                new Line(Player(4), Home, Shots: 2),
                new Line(Player(5), Home, Shots: 1),
            ],
            [Goal(Away, Player(1)), Goal(Home, Player(4)), Goal(Home, Player(4))]);

        var ranked = ThreeStars.Rank(match);
        var stars = ThreeStars.Choose(match);

        Assert.All(ranked.Take(3), score => Assert.Equal(Away, score.TeamId));
        Assert.Equal(ranked.Take(2), stars.Take(2));
        Assert.Equal(ranked.First(score => score.TeamId == Home), stars[2]);
    }

    [Fact]
    public void TiesGoToMorePointsThenMoreGoals()
    {
        // Each has 1.4: a goal and four shots, two secondary assists and six shots, or fourteen shots.
        var scorer = Player(1);
        var passer = Player(2);
        var shooter = Player(3);
        var match = Build(
            [
                new Line(scorer, Home, Shots: 4),
                new Line(passer, Home, Shots: 6),
                new Line(shooter, Home, Shots: 14),
                new Line(Player(4), Home, Shots: 2),
                new Line(Player(5), Home, Shots: 1),
                new Line(Player(6), Away, Shots: 1),
            ],
            [Goal(Home, Player(4), Player(5), passer), Goal(Home, scorer), Goal(Home, Player(4), Player(5), passer)]);

        var tied = ThreeStars.Rank(match).Where(score => Math.Round(score.Score, 6) == 1.4).Select(score => score.PlayerId);

        Assert.Equal([passer, scorer, shooter], tied);
    }

    [Fact]
    public void TiesThenGoToTheWinnerThenTheLowerPlayerIdentity()
    {
        var match = Build(
            [
                new Line(Player(7), Away, Shots: 3),
                new Line(Player(6), Home, Shots: 3),
                new Line(Player(5), Home, Shots: 3),
                new Line(Player(1), Home, Shots: 1),
            ],
            [Goal(Home, Player(1))]);

        var tied = ThreeStars.Rank(match).Where(score => score.PlayerId.Value != Player(1).Value
            && Math.Round(score.Score, 6) == 0.3).Select(score => score.PlayerId);

        Assert.Equal([Player(5), Player(6), Player(7)], tied);
    }

    private static double ScoreOf(CompletedMatch match, PlayerId playerId) =>
        ThreeStars.Rank(match).Single(score => score.PlayerId == playerId).Score;

    private static PlayerId Player(int number) => new(Guid.Parse($"00000000-0000-0000-0000-{number:D12}"));

    private static ShotTotals Shots(double expectedGoalsFor = 0, double expectedGoalsAgainst = 0) =>
        new(1, 1, 1, 1, 0, 0, 0, 0, expectedGoalsFor, expectedGoalsAgainst);

    private static MatchGoal Goal(
        TeamId team,
        PlayerId scorer,
        PlayerId? primary = null,
        PlayerId? secondary = null,
        bool isEmptyNet = false,
        int period = 1) =>
        new(period, TimeSpan.Zero, team, scorer, primary, secondary, GoalSituation.EvenStrength, isEmptyNet);

    /// <summary>
    /// A completed match whose box scores are worked out from the goals: each skater's goals,
    /// assists, and empty-net goals come from the scoring summary, each goalie faces the other
    /// side's shots, and each period has a shot for every goal in it. Goals in the same period
    /// keep their order but share a time, which the summary allows.
    /// </summary>
    private static CompletedMatch Build(
        IReadOnlyList<Line> lines,
        IReadOnlyList<MatchGoal> goals,
        MatchDecision decision = MatchDecision.Regulation,
        SituationalShotTotals? homeShotTotals = null)
    {
        goals = [.. goals.OrderBy(goal => goal.Period)];
        var periods = decision == MatchDecision.Regulation ? 3 : 4;
        var homeGoals = goals.Count(goal => goal.TeamId == Home);
        var awayGoals = goals.Count(goal => goal.TeamId == Away);
        var shootoutWinner = decision == MatchDecision.Shootout ? Home : (TeamId?)null;
        var home = Side(Home, homeGoals + (shootoutWinner == Home ? 1 : 0), Away, homeShotTotals ?? SituationalShotTotals.None);
        var away = Side(Away, awayGoals, Home, (homeShotTotals ?? SituationalShotTotals.None).Reverse());
        return new CompletedMatch(new ScheduledMatch(new DateOnly(2026, 10, 1), Home, Away), home, away, decision, goals, []);

        CompletedMatchTeam Side(TeamId team, int score, TeamId opponent, SituationalShotTotals shotTotals)
        {
            var skaters = lines.Where(line => line.Team == team).Select(SkaterBoxScore).ToList();
            var opposing = lines.Where(line => line.Team == opponent).Select(SkaterBoxScore).ToList();
            var opposingEmptyNet = opposing.Sum(skater => skater.EmptyNetGoals);
            var goalie = new GoalieBoxScore(
                team == Home ? HomeGoalie : AwayGoalie,
                opposing.Sum(skater => skater.Shots) - opposingEmptyNet,
                opposing.Sum(skater => skater.Goals) - opposingEmptyNet,
                opposing.Sum(skater => skater.ExpectedGoals),
                TimeSpan.FromMinutes(60));
            var shots = skaters.Sum(skater => skater.Shots);
            var laterGoals = Enumerable.Range(2, periods - 1)
                .ToDictionary(period => period, period => goals.Count(goal => goal.TeamId == team && goal.Period == period));
            var byPeriod = Enumerable.Range(1, periods)
                .Select(period => period == 1
                    ? new PeriodTotals(1, shots - laterGoals.Values.Sum(), skaters.Sum(skater => skater.ExpectedGoals), 0)
                    : new PeriodTotals(period, laterGoals[period], 0, 0));
            return new CompletedMatchTeam(team, score, shots, 0, skaters, goalie, shotTotals, byPeriod);
        }

        SkaterBoxScore SkaterBoxScore(Line line)
        {
            var scored = goals.Where(goal => goal.ScorerId == line.Id).ToList();
            var assisted = goals.Count(goal => goal.AssistIds.Contains(line.Id));
            var shots = Math.Max(line.Shots, scored.Count);
            return new SkaterBoxScore(
                line.Id,
                scored.Count,
                assisted,
                0,
                TimeSpan.FromMinutes(15),
                new SituationalTimeOnIce(TimeSpan.FromMinutes(15), TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
                shots,
                shots + 10,
                line.Hits,
                line.Blocks,
                0,
                0,
                line.Takeaways,
                line.Giveaways,
                line.ExpectedGoals,
                0,
                0,
                0,
                0,
                0,
                scored.Count(goal => goal.IsEmptyNet),
                line.OnIce ?? SituationalShotTotals.None);
        }
    }

    /// <param name="Shots">Shots on goal; at least the skater's goals.</param>
    private sealed record Line(
        PlayerId Id,
        TeamId Team,
        int Shots = 0,
        int Hits = 0,
        int Blocks = 0,
        int Takeaways = 0,
        int Giveaways = 0,
        double ExpectedGoals = 0,
        SituationalShotTotals? OnIce = null);
}