using HockeySim.Desktop.Tables;

namespace HockeySim.Desktop.Roster;

/// <summary>
/// The sorts chosen on a roster's tables: each column view keeps its own sort for skaters and for
/// goalies, so switching views brings back what was chosen there. Immutable; a roster hands its
/// sorts to the next roster so the choice holds across teams and days.
/// </summary>
public sealed class RosterSorts
{
    private readonly Dictionary<(RosterColumns Columns, bool Goalies), TableSort> _sorts;

    private RosterSorts(Dictionary<(RosterColumns, bool), TableSort> sorts)
    {
        _sorts = sorts;
    }

    public static RosterSorts None { get; } = new([]);

    public TableSort? Get(RosterColumns columns, bool goalies) =>
        _sorts.TryGetValue((columns, goalies), out var sort) ? sort : null;

    public RosterSorts With(RosterColumns columns, bool goalies, TableSort? sort)
    {
        var sorts = new Dictionary<(RosterColumns, bool), TableSort>(_sorts);
        if (sort is { } chosen)
        {
            sorts[(columns, goalies)] = chosen;
        }
        else
        {
            sorts.Remove((columns, goalies));
        }

        return new RosterSorts(sorts);
    }
}