using System.Globalization;

using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Schedule;

/// <summary>
/// Formats dates, decisions, and results consistently across the shell, home page, and schedule.
/// </summary>
public static class MatchDisplay
{
    public static string ShortDate(DateOnly date) => date.ToString("ddd, MMM d", CultureInfo.CurrentCulture);

    public static string LongDate(DateOnly date) => date.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture);

    /// <summary>
    /// The suffix that follows a final score, such as "OT"; empty for a regulation result.
    /// </summary>
    public static string DecisionSuffix(MatchDecision decision) => decision switch
    {
        MatchDecision.Regulation => string.Empty,
        MatchDecision.Overtime => "OT",
        MatchDecision.Shootout => "SO",
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown decision."),
    };

    public static string FinalLabel(MatchDecision decision) => decision switch
    {
        MatchDecision.Regulation => "Final",
        MatchDecision.Overtime => "Final · Overtime",
        MatchDecision.Shootout => "Final · Shootout",
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown decision."),
    };

    /// <summary>
    /// Describes a result from one team's side, such as "W 4–2" or "L 2–3 OT".
    /// </summary>
    public static string ResultFor(CompletedMatchSnapshot result, TeamId teamId)
    {
        var (team, opponent) = result.Home.TeamId == teamId ? (result.Home, result.Away) : (result.Away, result.Home);
        var outcome = result.WinnerId == teamId ? "W" : "L";
        var suffix = DecisionSuffix(result.Decision);
        var score = $"{outcome} {team.Score}–{opponent.Score}";
        return suffix.Length == 0 ? score : $"{score} {suffix}";
    }

    /// <summary>
    /// Formats a save percentage the way hockey tables do (".912"), or a dash before any shot so
    /// an undefined percentage is never shown as a number.
    /// </summary>
    public static string SavePercentage(int saves, int shotsAgainst) =>
        shotsAgainst == 0
            ? "—"
            : (saves / (double)shotsAgainst).ToString(".000", CultureInfo.CurrentCulture);
}