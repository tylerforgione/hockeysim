using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// The canonical record of a scheduled match that has been played. The score is always decisive,
/// and individual statistics reconcile with it: only a shootout winner's score exceeds its player
/// goals, by exactly the one deciding goal. Each goalie's shots, goals, and expected goals against
/// match the opponent's skaters, apart from the empty-net goals scored while the goalie was pulled,
/// one team's faceoff wins are the other's losses, a team scores shorthanded only when the
/// opponent had a power play, and each team's shot totals are the other's seen from the opposite
/// side. The scoring and penalty summaries account for exactly the goals, assists, and penalty
/// minutes in the box scores. Injuries and wear (the match's health changes) belong to appearing
/// players of their team.
/// </summary>
public sealed class CompletedMatch
{
    private readonly ReadOnlyCollection<MatchGoal> _goals;
    private readonly ReadOnlyCollection<MatchPenalty> _penalties;

    /// <param name="goals">Every goal scored by a player, in the order scored.</param>
    /// <param name="penalties">Every penalty assessed, in the order called.</param>
    /// <param name="health">The injuries and wear the match caused; none when omitted.</param>
    public CompletedMatch(
        ScheduledMatch scheduledMatch,
        CompletedMatchTeam home,
        CompletedMatchTeam away,
        MatchDecision decision,
        IEnumerable<MatchGoal> goals,
        IEnumerable<MatchPenalty> penalties,
        MatchHealthChanges? health = null)
    {
        ArgumentNullException.ThrowIfNull(scheduledMatch);
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(away);
        ArgumentNullException.ThrowIfNull(goals);
        ArgumentNullException.ThrowIfNull(penalties);

        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision), "The match decision is not recognised.");
        }

        if (home.TeamId != scheduledMatch.HomeTeamId || away.TeamId != scheduledMatch.AwayTeamId)
        {
            throw new ArgumentException("A completed match must be between the scheduled home and away teams.");
        }

        if (home.Score == away.Score)
        {
            throw new ArgumentException("A completed match must have a winner.");
        }

        if (decision != MatchDecision.Regulation && Math.Abs(home.Score - away.Score) != 1)
        {
            throw new ArgumentException("A match decided after regulation must be won by exactly one goal.");
        }

        var (winner, loser) = home.Score > away.Score ? (home, away) : (away, home);
        var shootoutGoal = decision == MatchDecision.Shootout ? 1 : 0;
        if (winner.Score != winner.PlayerGoals + shootoutGoal || loser.Score != loser.PlayerGoals)
        {
            throw new ArgumentException(
                "Each score must equal its player goals, plus the deciding goal for a shootout winner.");
        }

        if (!GoalieFacedOpponent(home.Goalie, away) || !GoalieFacedOpponent(away.Goalie, home))
        {
            throw new ArgumentException(
                "Each goalie's shots, goals, and expected goals against must match the opponent's totals, less its empty-net goals.");
        }

        if (FaceoffsWon(home) != FaceoffsLost(away) || FaceoffsWon(away) != FaceoffsLost(home))
        {
            throw new ArgumentException("Each faceoff one team won must be one the other team lost.");
        }

        if (BlockedShots(home) > UnsuccessfulAttempts(away) || BlockedShots(away) > UnsuccessfulAttempts(home))
        {
            throw new ArgumentException("A team cannot block more shots than the opponent attempted without reaching the net.");
        }

        if ((home.ShorthandedGoals > 0 && away.PowerPlayOpportunities == 0)
            || (away.ShorthandedGoals > 0 && home.PowerPlayOpportunities == 0))
        {
            throw new ArgumentException("A team cannot score shorthanded unless the opponent had a power play.");
        }

        // Every goal changes a skater's plus/minus by at most one.
        var playerGoals = home.PlayerGoals + away.PlayerGoals;
        if (home.Skaters.Concat(away.Skaters).Any(skater => Math.Abs(skater.PlusMinus) > playerGoals))
        {
            throw new ArgumentException("A skater's plus/minus cannot exceed the goals scored in the match.");
        }

        if (!home.ShotTotals.Matches(away.ShotTotals.Reverse()))
        {
            throw new ArgumentException("Each team's shot totals must be the other team's seen from the opposite side.");
        }

        var goalList = goals.ToList();
        var penaltyList = penalties.ToList();
        ThrowIfSummaryDiffers(goalList, penaltyList, home, away);

        health ??= MatchHealthChanges.None;
        ThrowIfHealthChangesAreNotForAppearingPlayers(health, home, away);

        ScheduledMatch = scheduledMatch;
        Home = home;
        Away = away;
        Decision = decision;
        _goals = goalList.AsReadOnly();
        _penalties = penaltyList.AsReadOnly();
        Health = health;
    }

    public ScheduledMatch ScheduledMatch { get; }

    public DateOnly Date => ScheduledMatch.Date;

    public CompletedMatchTeam Home { get; }

    public CompletedMatchTeam Away { get; }

    public MatchDecision Decision { get; }

    public CompletedMatchTeam Winner => Home.Score > Away.Score ? Home : Away;

    public CompletedMatchTeam Loser => Home.Score > Away.Score ? Away : Home;

    /// <summary>The scoring summary: every goal scored by a player, in the order scored.</summary>
    public IReadOnlyList<MatchGoal> Goals => _goals;

    /// <summary>The penalty summary: every penalty assessed, in the order called.</summary>
    public IReadOnlyList<MatchPenalty> Penalties => _penalties;

    /// <summary>The injuries suffered and the hidden wear taken in the match.</summary>
    public MatchHealthChanges Health { get; }

    /// <summary>
    /// Only a player who appeared can be injured or take wear, and each injury belongs to that
    /// player's team.
    /// </summary>
    private static void ThrowIfHealthChangesAreNotForAppearingPlayers(
        MatchHealthChanges health,
        CompletedMatchTeam home,
        CompletedMatchTeam away)
    {
        var appearing = new[] { home, away }
            .SelectMany(side => side.Skaters.Select(skater => skater.PlayerId).Append(side.Goalie.PlayerId)
                .Select(playerId => (playerId, side.TeamId)))
            .ToDictionary(entry => entry.playerId, entry => entry.TeamId);
        if (health.Injuries.Any(injury => !appearing.TryGetValue(injury.PlayerId, out var teamId) || teamId != injury.TeamId)
            || health.Wear.Any(gain => !appearing.ContainsKey(gain.PlayerId)))
        {
            throw new ArgumentException("Every injury and wear must belong to an appearing player of that team.", nameof(health));
        }
    }

    /// <summary>
    /// The summaries must be in time order and credit exactly the box scores' goals, assists,
    /// special-teams and empty-net goals, and penalty minutes, each to a skater of the right team.
    /// </summary>
    private static void ThrowIfSummaryDiffers(
        List<MatchGoal> goals,
        List<MatchPenalty> penalties,
        CompletedMatchTeam home,
        CompletedMatchTeam away)
    {
        if (goals.Any(goal => goal is null) || penalties.Any(penalty => penalty is null))
        {
            throw new ArgumentException("A match summary cannot contain a missing goal or penalty.");
        }

        if (!IsInTimeOrder(goals.Select(goal => (goal.Period, goal.TimeInPeriod)))
            || !IsInTimeOrder(penalties.Select(penalty => (penalty.Period, penalty.TimeInPeriod))))
        {
            throw new ArgumentException("The scoring and penalty summaries must each be in time order.");
        }

        var sides = new[] { home, away }.ToDictionary(side => side.TeamId);
        if (goals.Any(goal => !sides.TryGetValue(goal.TeamId, out var side)
                || !new[] { goal.ScorerId }.Concat(goal.AssistIds).All(id => side.Skaters.Any(skater => skater.PlayerId == id)))
            || penalties.Any(penalty => !sides.TryGetValue(penalty.TeamId, out var side)
                || side.Skaters.All(skater => skater.PlayerId != penalty.PlayerId)))
        {
            throw new ArgumentException("Every goal, assist, and penalty in the summary must belong to an appearing skater of that team.");
        }

        foreach (var skater in home.Skaters.Concat(away.Skaters))
        {
            var scored = goals.Where(goal => goal.ScorerId == skater.PlayerId).ToList();
            var assisted = goals.Where(goal => goal.AssistIds.Contains(skater.PlayerId)).ToList();
            if (scored.Count != skater.Goals
                || assisted.Count != skater.Assists
                || scored.Count(goal => goal.Situation == GoalSituation.PowerPlay) != skater.PowerPlayGoals
                || scored.Count(goal => goal.Situation == GoalSituation.Shorthanded) != skater.ShorthandedGoals
                || scored.Count(goal => goal.IsEmptyNet) != skater.EmptyNetGoals
                || assisted.Count(goal => goal.Situation == GoalSituation.PowerPlay) != skater.PowerPlayAssists
                || assisted.Count(goal => goal.Situation == GoalSituation.Shorthanded) != skater.ShorthandedAssists
                || penalties.Where(penalty => penalty.PlayerId == skater.PlayerId).Sum(penalty => penalty.Minutes) != skater.PenaltyMinutes)
            {
                throw new ArgumentException("The scoring and penalty summaries must match every skater's box score.");
            }
        }
    }

    private static bool IsInTimeOrder(IEnumerable<(int Period, TimeSpan TimeInPeriod)> times) =>
        times.Zip(times.Skip(1)).All(pair => pair.First.CompareTo(pair.Second) <= 0);

    /// <summary>
    /// The goalie faced every opponent shot and goal except the empty-net goals scored while they
    /// were pulled. Empty-net attempts carry no expected goals, so those match in full.
    /// </summary>
    private static bool GoalieFacedOpponent(GoalieBoxScore goalie, CompletedMatchTeam opponent) =>
        goalie.ShotsAgainst == opponent.Shots - opponent.EmptyNetGoals
        && goalie.GoalsAgainst == opponent.PlayerGoals - opponent.EmptyNetGoals
        && ExpectedGoalTotals.AreEqual(goalie.ExpectedGoalsAgainst, opponent.ExpectedGoals);

    private static int FaceoffsWon(CompletedMatchTeam team) => team.Skaters.Sum(skater => skater.FaceoffsWon);

    private static int FaceoffsLost(CompletedMatchTeam team) => team.Skaters.Sum(skater => skater.FaceoffsLost);

    private static int BlockedShots(CompletedMatchTeam team) => team.Skaters.Sum(skater => skater.BlockedShots);

    /// <summary>Attempts that missed the net or were blocked.</summary>
    private static int UnsuccessfulAttempts(CompletedMatchTeam team) => team.ShotAttempts - team.Shots;
}