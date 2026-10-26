using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Schedule;

/// <summary>
/// Picks a team's matches out of a list of results.
/// </summary>
public static class TeamResults
{
    public static bool Involves(CompletedMatchSnapshot result, TeamId teamId) =>
        result.Home.TeamId == teamId || result.Away.TeamId == teamId;

    /// <summary>
    /// Groups results by team, each team's oldest first when <paramref name="results"/> is in play order.
    /// Every team in <paramref name="teamIds"/> has an entry, empty before it plays.
    /// </summary>
    public static Dictionary<TeamId, List<CompletedMatchSnapshot>> ByTeam(
        IEnumerable<TeamId> teamIds,
        IEnumerable<CompletedMatchSnapshot> results)
    {
        var byTeam = teamIds.ToDictionary(id => id, _ => new List<CompletedMatchSnapshot>());
        foreach (var result in results)
        {
            byTeam[result.Home.TeamId].Add(result);
            byTeam[result.Away.TeamId].Add(result);
        }

        return byTeam;
    }
}