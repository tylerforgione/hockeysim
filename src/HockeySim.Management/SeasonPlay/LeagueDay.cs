using HockeySim.Domain;
using HockeySim.Simulation;
using HockeySim.Simulation.Events;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Management.SeasonPlay;

/// <summary>
/// Plays every match scheduled on the season's current date and applies the results.
/// </summary>
internal static class LeagueDay
{
    /// <summary>
    /// Simulates the day's matches in schedule order on one continuous random stream, with
    /// regular-season overtime, then hands the whole day to the season. The play-by-play stays
    /// with the Simulation result; the completed match keeps the box score and the scoring and
    /// penalty summaries. Simulation never changes the teams, so if any match fails the season is
    /// untouched and the caller keeps its original random state.
    /// </summary>
    /// <returns>The random state after the day's final match.</returns>
    public static RandomState Play(Season season, IMatchSimulator simulator, RandomState randomState)
    {
        var teams = season.League.Teams.ToDictionary(team => team.Id);
        var results = new List<CompletedMatch>();

        foreach (var scheduledMatch in season.CurrentDateMatches)
        {
            var match = new Match(teams[scheduledMatch.HomeTeamId], teams[scheduledMatch.AwayTeamId]);
            var result = simulator.Simulate(match, OvertimeFormat.RegularSeason, randomState);
            results.Add(ToCompletedMatch(scheduledMatch, result));
            randomState = result.RandomState;
        }

        season.CompleteDay(results);
        return randomState;
    }

    private static CompletedMatch ToCompletedMatch(ScheduledMatch scheduledMatch, MatchResult result) =>
        new(
            scheduledMatch,
            ToCompletedMatchTeam(result.Home),
            ToCompletedMatchTeam(result.Away),
            result.Decision,
            result.Goals.Select(goal => new MatchGoal(
                goal.Period,
                goal.TimeInPeriod,
                goal.TeamId,
                goal.ScorerId,
                goal.PrimaryAssistId,
                goal.SecondaryAssistId,
                goal.Situation,
                goal.IsEmptyNet)),
            result.Events.OfType<PenaltyEvent>().Select(penalty => new MatchPenalty(
                penalty.Period,
                penalty.TimeInPeriod,
                penalty.TeamId,
                penalty.PlayerId,
                penalty.Infraction,
                penalty.Kind)));

    private static CompletedMatchTeam ToCompletedMatchTeam(MatchTeamResult side) =>
        new(
            side.TeamId,
            side.Score,
            side.Shots,
            side.PowerPlayOpportunities,
            side.Skaters.Select(ToBoxScore),
            new GoalieBoxScore(
                side.Goalie.PlayerId,
                side.Goalie.ShotsAgainst,
                side.Goalie.GoalsAgainst,
                side.Goalie.ExpectedGoalsAgainst,
                side.Goalie.TimeOnIce),
            side.ShotTotals);

    private static SkaterBoxScore ToBoxScore(SkaterMatchStatistics skater) =>
        new(
            skater.PlayerId,
            skater.Goals,
            skater.Assists,
            skater.PlusMinus,
            skater.TimeOnIce,
            skater.Shots,
            skater.ShotAttempts,
            skater.Hits,
            skater.BlockedShots,
            skater.FaceoffsWon,
            skater.FaceoffsLost,
            skater.Takeaways,
            skater.Giveaways,
            skater.ExpectedGoals,
            skater.PenaltyMinutes,
            skater.PowerPlayGoals,
            skater.PowerPlayAssists,
            skater.ShorthandedGoals,
            skater.ShorthandedAssists,
            skater.EmptyNetGoals,
            skater.OnIce);
}