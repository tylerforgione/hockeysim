using System.Globalization;
using System.Windows.Input;

using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Standings;

/// <summary>
/// One team's line in a standings table, in the NHL's column conventions.
/// </summary>
/// <param name="TeamColour">The team's primary colour, shown as a swatch beside its name.</param>
/// <param name="Losses">Regulation losses; overtime and shootout losses are counted in <paramref name="OvertimeLosses"/>.</param>
/// <param name="PointsPercentage">".625" style, or a dash before the team has played.</param>
/// <param name="GoalDifferential">Signed, such as "+4" or "-2".</param>
/// <param name="Goals">Goals for and against behind the differential, for its tooltip.</param>
/// <param name="LastTen">The last ten games as wins-losses-overtime losses, such as "6-3-1".</param>
/// <param name="Streak">The current run of like results, such as "W3".</param>
/// <param name="PlayoffMarker">The NHL's clinch or elimination letter, or empty while undecided.</param>
/// <param name="PlayoffStatus">The marker's meaning, or <see langword="null"/> while undecided.</param>
/// <param name="IsLastQualifier">
/// The table's last playoff position as it stands, drawn with a line beneath it.
/// </param>
public sealed record StandingsRowViewModel(
    int Rank,
    TeamId TeamId,
    string TeamName,
    Colour TeamColour,
    bool IsManaged,
    string PlayoffMarker,
    string? PlayoffStatus,
    int GamesPlayed,
    int Wins,
    int Losses,
    int OvertimeLosses,
    int Points,
    string PointsPercentage,
    int RegulationAndOvertimeWins,
    string GoalDifferential,
    string Goals,
    string LastTen,
    string Streak,
    bool IsLastQualifier = false)
{
    /// <summary>Opens the team; absent where a row links nowhere.</summary>
    public ICommand? OpenTeamCommand { get; init; }

    /// <param name="results">The team's regular-season results, oldest first.</param>
    public static StandingsRowViewModel Create(
        StandingsEntrySnapshot entry,
        TeamSnapshot team,
        bool isManaged,
        IReadOnlyList<CompletedMatchSnapshot> results)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(team);

        var record = entry.Record;
        return new StandingsRowViewModel(
            entry.Rank,
            team.Id,
            team.Name,
            team.Colours.Primary,
            isManaged,
            PlayoffStatusDisplay.Marker(entry.PlayoffStatus),
            PlayoffStatusDisplay.Describe(entry.PlayoffStatus),
            record.GamesPlayed,
            record.Wins,
            record.RegulationLosses,
            record.OvertimeLosses + record.ShootoutLosses,
            record.Points,
            record.PointsPercentage is { } percentage
                ? percentage.ToString(".000", CultureInfo.CurrentCulture)
                : "—",
            record.RegulationAndOvertimeWins,
            record.GoalDifferential.ToString("+0;-0;0", CultureInfo.CurrentCulture),
            string.Create(CultureInfo.CurrentCulture, $"{record.GoalsFor} for, {record.GoalsAgainst} against"),
            TeamForm.LastTen(results, team.Id),
            TeamForm.Streak(results, team.Id));
    }
}