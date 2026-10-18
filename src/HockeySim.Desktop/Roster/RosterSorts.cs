using HockeySim.Desktop.Tables;

namespace HockeySim.Desktop.Roster;

/// <summary>
/// The sorts chosen on a roster's tables: one for skaters, shared by every skater group, and one
/// for goalies. A sort is remembered even while the chosen column view has no such column, so
/// returning to a view that does brings it back. A roster hands its sorts to the next roster so
/// the choice holds across teams and days.
/// </summary>
public sealed record RosterSorts(TableSort? Skaters, TableSort? Goalies)
{
    public static RosterSorts None { get; } = new(null, null);
}