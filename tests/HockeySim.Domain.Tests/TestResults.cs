using HockeySim.Domain;

namespace HockeySim.Domain.Tests;

/// <summary>
/// Builds consistent completed matches for a <see cref="TestLeague"/>.
/// </summary>
internal static class TestResults
{
    private const int Shots = 30;
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
        int emptyNetGoals = 0)
    {
        var shotsOnGoal = shots ?? goals;
        return new SkaterBoxScore(
            playerId,
            goals,
            assists,
            plusMinus,
            TimeSpan.FromMinutes(15),
            shotsOnGoal,
            shotAttempts ?? shotsOnGoal,
            hits: 0,
            blockedShots,
            faceoffsWon,
            faceoffsLost,
            takeaways: 0,
            giveaways: 0,
            expectedGoals,
            penaltyMinutes,
            powerPlayGoals,
            powerPlayAssists,
            shorthandedGoals,
            shorthandedAssists,
            emptyNetGoals);
    }

    public static GoalieBoxScore Goalie(PlayerId playerId, int shotsAgainst, int goalsAgainst, double expectedGoalsAgainst = 0) =>
        new(playerId, shotsAgainst, goalsAgainst, expectedGoalsAgainst, MatchLength);

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

        return new CompletedMatch(
            scheduled,
            Side(home, homeGoals + homeBonus, homeGoals, opponentGoals: awayGoals),
            Side(away, awayGoals + awayBonus, awayGoals, opponentGoals: homeGoals),
            decision);

        static CompletedMatchTeam Side(Team team, int score, int playerGoals, int opponentGoals)
        {
            var skaters = team.Lineup.ForwardLines.SelectMany(line => line.Players)
                .Concat(team.Lineup.DefencePairs.SelectMany(pair => pair.Players))
                .Select(player => player.Id == FirstSkater(team)
                    ? Skater(player.Id, playerGoals, shots: Shots, expectedGoals: TeamExpectedGoals)
                    : Skater(player.Id));
            var goalie = Goalie(team.Lineup.StartingGoalie.Id, Shots, opponentGoals, TeamExpectedGoals);
            return new CompletedMatchTeam(team.Id, score, Shots, powerPlayOpportunities: 0, skaters, goalie);
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
}