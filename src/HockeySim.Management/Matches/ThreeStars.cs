using HockeySim.Domain;

namespace HockeySim.Management.Matches;

/// <summary>
/// Chooses a completed match's three stars: the three players with the highest game score, from
/// every appearing skater and both starting goalies. It reads only what a completed match keeps
/// (the box scores and the scoring summary), so every match ever played can be given its stars
/// without saving them.
/// </summary>
/// <remarks>
/// The weights were calibrated over a generated league's regular season so that goalies take about
/// a third of first stars and a fifth of all stars, and defencemen about a sixth (see issue #94).
/// Penalties drawn are not recorded yet; they belong in the skater score once they are.
/// </remarks>
public static class ThreeStars
{
    public const int Count = 3;

    private const double GoalWeight = 1.0;
    private const double EmptyNetGoalWeight = 0.5;
    private const double PrimaryAssistWeight = 0.7;
    private const double SecondaryAssistWeight = 0.4;
    private const double GameWinningGoalBonus = 0.5;
    private const double ShotWeight = 0.1;
    private const double BlockedShotWeight = 0.1;
    private const double HitWeight = 0.05;
    private const double TakeawayWeight = 0.05;
    private const double GiveawayWeight = -0.05;
    private const double OnIceExpectedGoalDifferenceWeight = 0.75;

    private const double GoalsSavedAboveExpectedWeight = 1.0;
    private const double SaveWeight = 0.025;
    private const double ShutoutBonus = 0.75;

    /// <summary>
    /// Scores are sums of fractions, so they are compared at this many decimal places: two players
    /// with the same contributions tie however their sums were rounded.
    /// </summary>
    private const int ComparedDecimals = 6;

    /// <summary>
    /// The three stars, first star first: the top three by game score, except that when none of
    /// them plays for the winning team, the winning team's best player replaces the third star.
    /// </summary>
    public static IReadOnlyList<PlayerGameScore> Choose(CompletedMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var ranked = Rank(match);
        var stars = ranked.Take(Count).ToList();
        var winnerId = match.Winner.TeamId;
        if (stars.All(star => star.TeamId != winnerId))
        {
            stars[^1] = ranked.First(score => score.TeamId == winnerId);
        }

        return stars.AsReadOnly();
    }

    /// <summary>
    /// Every appearing skater and both starting goalies, highest game score first. Ties go to more
    /// points, then more goals, then the winning team's player, then the lower player identity, so
    /// the order is fully determined.
    /// </summary>
    public static IReadOnlyList<PlayerGameScore> Rank(CompletedMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var winnerId = match.Winner.TeamId;
        var gameWinningScorerId = GameWinningGoal(match)?.ScorerId;
        var entries = new List<RankedEntry>();
        foreach (var (side, opponent) in new[] { (match.Home, match.Away), (match.Away, match.Home) })
        {
            var isWinner = side.TeamId == winnerId;
            entries.AddRange(side.Skaters.Select(skater => new RankedEntry(
                new PlayerGameScore(skater.PlayerId, side.TeamId, SkaterScore(skater, match.Goals, gameWinningScorerId)),
                skater.Points,
                skater.Goals,
                isWinner)));
            entries.Add(new RankedEntry(
                new PlayerGameScore(side.Goalie.PlayerId, side.TeamId, GoalieScore(side.Goalie, opponent)),
                Points: 0,
                Goals: 0,
                isWinner));
        }

        return entries
            .OrderByDescending(entry => Math.Round(entry.Score.Score, ComparedDecimals))
            .ThenByDescending(entry => entry.Points)
            .ThenByDescending(entry => entry.Goals)
            .ThenByDescending(entry => entry.IsWinner)
            .ThenBy(entry => entry.Score.PlayerId.Value)
            .Select(entry => entry.Score)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// The winning team's goal that put it one ahead of the loser's final score, counting
    /// regulation and overtime only; <see langword="null"/> for a match decided by a shootout.
    /// </summary>
    public static MatchGoal? GameWinningGoal(CompletedMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);

        if (match.Decision == MatchDecision.Shootout)
        {
            return null;
        }

        return match.Goals.Where(goal => goal.TeamId == match.Winner.TeamId).ElementAt(match.Loser.Score);
    }

    private static double SkaterScore(SkaterBoxScore skater, IReadOnlyList<MatchGoal> goals, PlayerId? gameWinningScorerId)
    {
        var onIce = skater.OnIce.All;
        return ((skater.Goals - skater.EmptyNetGoals) * GoalWeight)
            + (skater.EmptyNetGoals * EmptyNetGoalWeight)
            + (goals.Count(goal => goal.PrimaryAssistId == skater.PlayerId) * PrimaryAssistWeight)
            + (goals.Count(goal => goal.SecondaryAssistId == skater.PlayerId) * SecondaryAssistWeight)
            + (gameWinningScorerId == skater.PlayerId ? GameWinningGoalBonus : 0)
            + (skater.Shots * ShotWeight)
            + (skater.BlockedShots * BlockedShotWeight)
            + (skater.Hits * HitWeight)
            + (skater.Takeaways * TakeawayWeight)
            + (skater.Giveaways * GiveawayWeight)
            + ((onIce.ExpectedGoalsFor - onIce.ExpectedGoalsAgainst) * OnIceExpectedGoalDifferenceWeight);
    }

    /// <summary>
    /// A shutout here means the opponent scored no goal in regulation or overtime, so an
    /// empty-net goal against prevents one, unlike the season statistic.
    /// </summary>
    private static double GoalieScore(GoalieBoxScore goalie, CompletedMatchTeam opponent) =>
        ((goalie.ExpectedGoalsAgainst - goalie.GoalsAgainst) * GoalsSavedAboveExpectedWeight)
        + (goalie.Saves * SaveWeight)
        + (opponent.PlayerGoals == 0 ? ShutoutBonus : 0);

    private sealed record RankedEntry(PlayerGameScore Score, int Points, int Goals, bool IsWinner);
}

/// <summary>A player's game score in one completed match.</summary>
/// <param name="Score">The game score: higher is better, and it can be negative.</param>
public sealed record PlayerGameScore(PlayerId PlayerId, TeamId TeamId, double Score);