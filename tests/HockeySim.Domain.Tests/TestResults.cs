using HockeySim.Domain;

using Xunit;

namespace HockeySim.Domain.Tests;

/// <summary>
/// Builds consistent completed matches for a <see cref="TestLeague"/>.
/// </summary>
internal static class TestResults
{
    private const int TeamShots = 30;
    private const double TeamExpectedGoals = 2.5;

    public static TimeSpan MatchLength { get; } = TimeSpan.FromMinutes(60);

    public static PlayerId FirstSkater(Team team) => team.Lineup.ForwardLines[0].LeftWing.Id;

    /// <summary>
    /// A skater box score with every unnamed statistic zero. Shots default to the goals and shot
    /// attempts to the shots, the fewest that are consistent.
    /// </summary>
    public static SkaterBoxScore Skater(
        PlayerId playerId,
        int goals = 0,
        int assists = 0,
        int? shots = null,
        int? shotAttempts = null,
        double expectedGoals = 0,
        int plusMinus = 0,
        int blockedShots = 0,
        int faceoffsWon = 0,
        int faceoffsLost = 0,
        int penaltyMinutes = 0,
        int powerPlayGoals = 0,
        int powerPlayAssists = 0,
        int shorthandedGoals = 0,
        int shorthandedAssists = 0,
        int emptyNetGoals = 0,
        int hits = 0,
        int takeaways = 0,
        int giveaways = 0,
        TimeSpan? timeOnIce = null,
        SituationalShotTotals? onIce = null)
    {
        var shotsOnGoal = shots ?? goals;
        return new SkaterBoxScore(
            playerId,
            goals,
            assists,
            plusMinus,
            timeOnIce ?? TimeSpan.FromMinutes(15),
            shotsOnGoal,
            shotAttempts ?? shotsOnGoal,
            hits,
            blockedShots,
            faceoffsWon,
            faceoffsLost,
            takeaways,
            giveaways,
            expectedGoals,
            penaltyMinutes,
            powerPlayGoals,
            powerPlayAssists,
            shorthandedGoals,
            shorthandedAssists,
            emptyNetGoals,
            onIce ?? SituationalShotTotals.None);
    }

    public static GoalieBoxScore Goalie(
        PlayerId playerId,
        int shotsAgainst,
        int goalsAgainst,
        double expectedGoalsAgainst = 0,
        TimeSpan? timeOnIce = null) =>
        new(playerId, shotsAgainst, goalsAgainst, expectedGoalsAgainst, timeOnIce ?? MatchLength);

    /// <summary>Shot totals with every unnamed count zero.</summary>
    public static ShotTotals Shots(
        int attemptsFor = 0,
        int attemptsAgainst = 0,
        int? unblockedFor = null,
        int? unblockedAgainst = null,
        int shotsFor = 0,
        int shotsAgainst = 0,
        int goalsFor = 0,
        int goalsAgainst = 0,
        double expectedGoalsFor = 0,
        double expectedGoalsAgainst = 0) =>
        new(
            attemptsFor,
            attemptsAgainst,
            unblockedFor ?? attemptsFor,
            unblockedAgainst ?? attemptsAgainst,
            shotsFor,
            shotsAgainst,
            goalsFor,
            goalsAgainst,
            expectedGoalsFor,
            expectedGoalsAgainst);

    /// <summary>
    /// Asserts two sets of shot totals count the same play, allowing for expected goals summed in a
    /// different order.
    /// </summary>
    public static void AssertSameTotals(SituationalShotTotals expected, SituationalShotTotals actual)
    {
        foreach (var situation in Enum.GetValues<StrengthSituation>())
        {
            var (want, got) = (expected[situation], actual[situation]);
            Assert.Equal(
                (want.AttemptsFor, want.AttemptsAgainst, want.UnblockedAttemptsFor, want.UnblockedAttemptsAgainst,
                    want.ShotsFor, want.ShotsAgainst, want.GoalsFor, want.GoalsAgainst),
                (got.AttemptsFor, got.AttemptsAgainst, got.UnblockedAttemptsFor, got.UnblockedAttemptsAgainst,
                    got.ShotsFor, got.ShotsAgainst, got.GoalsFor, got.GoalsAgainst));
            Assert.Equal(want.ExpectedGoalsFor, got.ExpectedGoalsFor, precision: 10);
            Assert.Equal(want.ExpectedGoalsAgainst, got.ExpectedGoalsAgainst, precision: 10);
        }
    }

    /// <summary>Shot totals in one situation and none in the others.</summary>
    public static SituationalShotTotals In(StrengthSituation situation, ShotTotals totals) =>
        new(
            situation == StrengthSituation.FiveOnFive ? totals : ShotTotals.None,
            situation == StrengthSituation.PowerPlay ? totals : ShotTotals.None,
            situation == StrengthSituation.PenaltyKill ? totals : ShotTotals.None,
            situation == StrengthSituation.Other ? totals : ShotTotals.None);

    /// <summary>
    /// Builds a result in which each side's first forward takes every shot and scores every player
    /// goal. A shootout adds the deciding goal to the winner's score.
    /// </summary>
    public static CompletedMatch Create(
        League league,
        ScheduledMatch scheduled,
        int homeGoals,
        int awayGoals,
        MatchDecision decision = MatchDecision.Regulation,
        bool shootoutWinnerIsHome = true)
    {
        var home = league.Teams.Single(team => team.Id == scheduled.HomeTeamId);
        var away = league.Teams.Single(team => team.Id == scheduled.AwayTeamId);
        var homeBonus = decision == MatchDecision.Shootout && shootoutWinnerIsHome ? 1 : 0;
        var awayBonus = decision == MatchDecision.Shootout && !shootoutWinnerIsHome ? 1 : 0;

        return Match(
            scheduled,
            Side(home, homeGoals + homeBonus, homeGoals, opponentGoals: awayGoals),
            Side(away, awayGoals + awayBonus, awayGoals, opponentGoals: homeGoals),
            decision);

        static CompletedMatchTeam Side(Team team, int score, int playerGoals, int opponentGoals)
        {
            var skaters = team.Lineup.ForwardLines.SelectMany(line => line.Players)
                .Concat(team.Lineup.DefencePairs.SelectMany(pair => pair.Players))
                .Select(player => player.Id == FirstSkater(team)
                    ? Skater(player.Id, playerGoals, shots: TeamShots, expectedGoals: TeamExpectedGoals)
                    : Skater(player.Id));
            var goalie = Goalie(team.Lineup.StartingGoalie.Id, TeamShots, opponentGoals, TeamExpectedGoals);
            return new CompletedMatchTeam(team.Id, score, TeamShots, powerPlayOpportunities: 0, skaters, goalie, SituationalShotTotals.None);
        }
    }

    /// <summary>
    /// Builds a result from final scores, which for a shootout include the winner's deciding goal.
    /// </summary>
    public static CompletedMatch FromFinalScore(
        League league,
        ScheduledMatch scheduled,
        int homeScore,
        int awayScore,
        MatchDecision decision) =>
        decision == MatchDecision.Shootout
            ? Create(league, scheduled, Math.Min(homeScore, awayScore), Math.Min(homeScore, awayScore), decision, homeScore > awayScore)
            : Create(league, scheduled, homeScore, awayScore, decision);

    /// <summary>
    /// A completed match whose scoring and penalty summaries are derived from the box scores, so
    /// they always agree with them.
    /// </summary>
    public static CompletedMatch Match(
        ScheduledMatch scheduled,
        CompletedMatchTeam home,
        CompletedMatchTeam away,
        MatchDecision decision = MatchDecision.Regulation) =>
        new(scheduled, home, away, decision, GoalsFor(home, away), PenaltiesFor(home, away));

    /// <summary>
    /// One goal per skater goal, power-play and shorthanded goals first, then the remainder at even
    /// strength; the first of a skater's goals are the empty-net ones. Assists go to the first
    /// goals of the right situation with a free assist and a different scorer.
    /// </summary>
    public static IReadOnlyList<MatchGoal> GoalsFor(params CompletedMatchTeam[] sides)
    {
        var goals = new List<GoalDraft>();
        foreach (var side in sides)
        {
            var teamGoals = side.Skaters
                .SelectMany(skater => Enumerable.Range(0, skater.Goals).Select(index => new GoalDraft(
                    side.TeamId,
                    skater.PlayerId,
                    index < skater.PowerPlayGoals ? GoalSituation.PowerPlay
                    : index < skater.PowerPlayGoals + skater.ShorthandedGoals ? GoalSituation.Shorthanded
                    : GoalSituation.EvenStrength,
                    index < skater.EmptyNetGoals)))
                .ToList();

            foreach (var skater in side.Skaters)
            {
                var evenStrengthAssists = skater.Assists - skater.PowerPlayAssists - skater.ShorthandedAssists;
                Assist(teamGoals, skater.PlayerId, GoalSituation.PowerPlay, skater.PowerPlayAssists);
                Assist(teamGoals, skater.PlayerId, GoalSituation.Shorthanded, skater.ShorthandedAssists);
                Assist(teamGoals, skater.PlayerId, GoalSituation.EvenStrength, evenStrengthAssists);
            }

            goals.AddRange(teamGoals);
        }

        return goals
            .Select((goal, index) => new MatchGoal(
                1,
                TimeSpan.FromSeconds(index),
                goal.TeamId,
                goal.ScorerId,
                goal.Assists.Count > 0 ? goal.Assists[0] : null,
                goal.Assists.Count > 1 ? goal.Assists[1] : null,
                goal.Situation,
                goal.IsEmptyNet))
            .ToList();

        static void Assist(List<GoalDraft> goals, PlayerId playerId, GoalSituation situation, int count)
        {
            foreach (var goal in goals
                .Where(goal => goal.Situation == situation && goal.ScorerId != playerId && goal.Assists.Count < 2)
                .Take(count))
            {
                goal.Assists.Add(playerId);
            }
        }
    }

    /// <summary>A major for an odd number of penalty minutes, and minors for the rest.</summary>
    public static IReadOnlyList<MatchPenalty> PenaltiesFor(params CompletedMatchTeam[] sides) =>
        sides
            .SelectMany(side => side.Skaters.SelectMany(skater =>
                (skater.PenaltyMinutes % 2 == 1 ? [PenaltyKind.Major] : Array.Empty<PenaltyKind>())
                    .Concat(Enumerable.Repeat(PenaltyKind.Minor, (skater.PenaltyMinutes - (skater.PenaltyMinutes % 2 == 1 ? 5 : 0)) / 2))
                    .Select(kind => (side.TeamId, skater.PlayerId, kind))))
            .Select((penalty, index) => new MatchPenalty(
                1, TimeSpan.FromSeconds(index), penalty.TeamId, penalty.PlayerId, Infraction.Hooking, penalty.kind))
            .ToList();

    private sealed record GoalDraft(TeamId TeamId, PlayerId ScorerId, GoalSituation Situation, bool IsEmptyNet)
    {
        public List<PlayerId> Assists { get; } = [];
    }
}