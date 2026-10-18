using HockeySim.Desktop.Tables;

namespace HockeySim.Desktop.Schedule;

/// <summary>
/// The sortable columns of the schedule table. The phases follow one another, so phase and date
/// both sort chronologically; an unplayed match has no result and sorts last.
/// </summary>
public static class ScheduleTableColumns
{
    public static IReadOnlyList<TableColumn<ScheduleMatchRowViewModel>> Matches { get; } =
    [
        new("phase", row => row.Date, AscendingFirst: true),
        new("date", row => row.Date, AscendingFirst: true),
        new("venue", row => row.Venue, AscendingFirst: true),
        new("opponent", row => row.Opponent, AscendingFirst: true),
        new("result", row => row.GoalDifferential),
    ];
}