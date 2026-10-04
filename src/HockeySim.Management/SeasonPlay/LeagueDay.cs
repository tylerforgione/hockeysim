using HockeySim.Domain;
using HockeySim.Simulation;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Management.SeasonPlay;

/// <summary>
/// Plays every match scheduled on the season's current date and applies the results.
/// </summary>
internal static class LeagueDay
{
    /// <summary>
    /// Simulates the day's matches in schedule order on one continuous random stream, then hands
    /// the whole day to the season. Simulation never changes the teams, so if any match fails the
    /// season is untouched and the caller keeps its original random state.
    /// </summary>
    /// <returns>The random state after the day's final match.</returns>
    public static RandomState Play(Season season, IMatchSimulator simulator, RandomState randomState)
    {
        var teams = season.League.Teams.ToDictionary(team => team.Id);
        var results = new List<CompletedMatch>();

        foreach (var scheduledMatch in season.CurrentDateMatches)
        {
            var match = new Match(teams[scheduledMatch.HomeTeamId], teams[scheduledMatch.AwayTeamId]);
            var result = simulator.Simulate(match, randomState);
            results.Add(ToCompletedMatch(scheduledMatch, result));
            randomState = result.RandomState;
        }

        season.CompleteDay(results);
        return randomState;
    }

    private static CompletedMatch ToCompletedMatch(ScheduledMatch scheduledMatch, MatchResult result) =>
        new(scheduledMatch, ToCompletedMatchTeam(result.Home), ToCompletedMatchTeam(result.Away), result.Decision);

    private static CompletedMatchTeam ToCompletedMatchTeam(MatchTeamResult side) =>
        new(
            side.TeamId,
            side.Score,
            side.Shots,
            side.Skaters.Select(skater => new SkaterBoxScore(skater.PlayerId, skater.Goals, skater.Assists)),
            new GoalieBoxScore(side.Goalie.PlayerId, side.Goalie.ShotsAgainst, side.Goalie.GoalsAgainst));
}