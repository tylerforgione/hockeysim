using HockeySim.Domain;

namespace HockeySim.Domain.Tests;

/// <summary>
/// Builds consistent completed matches for a <see cref="TestLeague"/>.
/// </summary>
internal static class TestResults
{
    private const int Shots = 30;

    public static PlayerId FirstSkater(Team team) => team.Lineup.ForwardLines[0].LeftWing.Id;

    /// <summary>
    /// Builds a result in which each side's first forward scores every player goal. A shootout
    /// adds the deciding goal to the winner's score.
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
                .Select(player => new SkaterBoxScore(player.Id, player.Id == FirstSkater(team) ? playerGoals : 0, 0));
            var goalie = new GoalieBoxScore(team.Lineup.StartingGoalie.Id, Shots, opponentGoals);
            return new CompletedMatchTeam(team.Id, score, Shots, skaters, goalie);
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