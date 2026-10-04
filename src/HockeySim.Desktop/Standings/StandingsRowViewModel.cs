using System.Globalization;

using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Standings;

/// <summary>
/// One team's line in a standings table, in the NHL's column conventions.
/// </summary>
/// <param name="Losses">Regulation losses; overtime and shootout losses are counted in <paramref name="OvertimeLosses"/>.</param>
/// <param name="PointsPercentage">".625" style, or a dash before the team has played.</param>
/// <param name="GoalDifferential">Signed, such as "+4" or "-2".</param>
public sealed record StandingsRowViewModel(
    int Rank,
    string TeamName,
    bool IsManaged,
    int GamesPlayed,
    int Wins,
    int Losses,
    int OvertimeLosses,
    int Points,
    string PointsPercentage,
    int RegulationWins,
    int RegulationAndOvertimeWins,
    int GoalsFor,
    int GoalsAgainst,
    string GoalDifferential)
{
    public static StandingsRowViewModel Create(StandingsEntrySnapshot entry, string teamName, bool isManaged)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var record = entry.Record;
        return new StandingsRowViewModel(
            entry.Rank,
            teamName,
            isManaged,
            record.GamesPlayed,
            record.Wins,
            record.RegulationLosses,
            record.OvertimeLosses + record.ShootoutLosses,
            record.Points,
            record.PointsPercentage is { } percentage
                ? percentage.ToString(".000", CultureInfo.CurrentCulture)
                : "—",
            record.RegulationWins,
            record.RegulationAndOvertimeWins,
            record.GoalsFor,
            record.GoalsAgainst,
            record.GoalDifferential.ToString("+0;-0;0", CultureInfo.CurrentCulture));
    }
}