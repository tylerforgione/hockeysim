using System.Globalization;

using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Standings;

/// <summary>
/// A team's recent form from its results, oldest first: its last ten games and current streak.
/// Wins, regulation losses, and overtime and shootout losses are told apart as standings count them.
/// </summary>
public static class TeamForm
{
    private const string None = "—";
    private const int RecentGames = 10;

    /// <summary>The last ten games as wins-losses-overtime losses, such as "6-3-1"; a dash before any game.</summary>
    public static string LastTen(IReadOnlyList<CompletedMatchSnapshot> results, TeamId teamId)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (results.Count == 0)
        {
            return None;
        }

        var kinds = results.TakeLast(RecentGames).Select(result => Kind(result, teamId)).ToList();
        return string.Create(
            CultureInfo.CurrentCulture,
            $"{kinds.Count(kind => kind == "W")}-{kinds.Count(kind => kind == "L")}-{kinds.Count(kind => kind == "OT")}");
    }

    /// <summary>The run of like results ending with the latest, such as "W3" or "OT1"; a dash before any game.</summary>
    public static string Streak(IReadOnlyList<CompletedMatchSnapshot> results, TeamId teamId)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (results.Count == 0)
        {
            return None;
        }

        var kind = Kind(results[^1], teamId);
        var length = 0;
        for (var index = results.Count - 1; index >= 0 && Kind(results[index], teamId) == kind; index--)
        {
            length++;
        }

        return string.Create(CultureInfo.CurrentCulture, $"{kind}{length}");
    }

    private static string Kind(CompletedMatchSnapshot result, TeamId teamId) =>
        result.WinnerId == teamId ? "W" : result.Decision == MatchDecision.Regulation ? "L" : "OT";
}