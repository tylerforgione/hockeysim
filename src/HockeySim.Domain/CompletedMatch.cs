namespace HockeySim.Domain;

/// <summary>
/// The canonical record of a scheduled match that has been played. The score is always decisive,
/// and individual statistics reconcile with it: only a shootout winner's score exceeds its player
/// goals, by exactly the one deciding goal. Each goalie's shots, goals, and expected goals against
/// match the opponent's skaters, and one team's faceoff wins are the other's losses.
/// </summary>
public sealed class CompletedMatch
{
    public CompletedMatch(
        ScheduledMatch scheduledMatch,
        CompletedMatchTeam home,
        CompletedMatchTeam away,
        MatchDecision decision)
    {
        ArgumentNullException.ThrowIfNull(scheduledMatch);
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(away);

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
                "Each goalie's shots, goals, and expected goals against must match the opponent's totals.");
        }

        if (FaceoffsWon(home) != FaceoffsLost(away) || FaceoffsWon(away) != FaceoffsLost(home))
        {
            throw new ArgumentException("Each faceoff one team won must be one the other team lost.");
        }

        if (BlockedShots(home) > UnsuccessfulAttempts(away) || BlockedShots(away) > UnsuccessfulAttempts(home))
        {
            throw new ArgumentException("A team cannot block more shots than the opponent attempted without reaching the net.");
        }

        // Every goal changes a skater's plus/minus by at most one.
        var playerGoals = home.PlayerGoals + away.PlayerGoals;
        if (home.Skaters.Concat(away.Skaters).Any(skater => Math.Abs(skater.PlusMinus) > playerGoals))
        {
            throw new ArgumentException("A skater's plus/minus cannot exceed the goals scored in the match.");
        }

        ScheduledMatch = scheduledMatch;
        Home = home;
        Away = away;
        Decision = decision;
    }

    public ScheduledMatch ScheduledMatch { get; }

    public DateOnly Date => ScheduledMatch.Date;

    public CompletedMatchTeam Home { get; }

    public CompletedMatchTeam Away { get; }

    public MatchDecision Decision { get; }

    public CompletedMatchTeam Winner => Home.Score > Away.Score ? Home : Away;

    public CompletedMatchTeam Loser => Home.Score > Away.Score ? Away : Home;

    private static bool GoalieFacedOpponent(GoalieBoxScore goalie, CompletedMatchTeam opponent) =>
        goalie.ShotsAgainst == opponent.Shots
        && goalie.GoalsAgainst == opponent.PlayerGoals
        && ExpectedGoalTotals.AreEqual(goalie.ExpectedGoalsAgainst, opponent.ExpectedGoals);

    private static int FaceoffsWon(CompletedMatchTeam team) => team.Skaters.Sum(skater => skater.FaceoffsWon);

    private static int FaceoffsLost(CompletedMatchTeam team) => team.Skaters.Sum(skater => skater.FaceoffsLost);

    private static int BlockedShots(CompletedMatchTeam team) => team.Skaters.Sum(skater => skater.BlockedShots);

    /// <summary>Attempts that missed the net or were blocked.</summary>
    private static int UnsuccessfulAttempts(CompletedMatchTeam team) => team.ShotAttempts - team.Shots;
}