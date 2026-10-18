namespace HockeySim.Desktop.Tables;

/// <summary>
/// Sorts a table's rows by a column, and works out the sort a header click chooses. Rows that tie
/// keep their order in the default-ordered input, and rows with no value always come last.
/// </summary>
public static class TableSorting
{
    /// <summary>
    /// The sort after a click on a column: the opposite direction when it is already the sorted
    /// column, otherwise the column's first direction.
    /// </summary>
    public static TableSort Click<TRow>(IReadOnlyList<TableColumn<TRow>> columns, TableSort? current, string columnKey)
    {
        var column = Find(columns, columnKey);
        return current is { } sort && sort.Column == columnKey
            ? sort with { Direction = sort.Opposite }
            : new TableSort(columnKey, column.AscendingFirst ? SortDirection.Ascending : SortDirection.Descending);
    }

    /// <summary>Orders rows (given in the table's default order) by the sort; no sort keeps them as given.</summary>
    public static List<TRow> Apply<TRow>(IEnumerable<TRow> defaultOrder, IReadOnlyList<TableColumn<TRow>> columns, TableSort? sort)
    {
        var rows = defaultOrder.ToList();
        if (sort is not { } chosen)
        {
            return rows;
        }

        var column = Find(columns, chosen.Column);
        var keyed = rows.Select(row => (Row: row, Key: column.SortKey(row))).ToList();
        var defined = keyed.Where(entry => entry.Key is not null);
        var ordered = chosen.Direction == SortDirection.Ascending
            ? defined.OrderBy(entry => entry.Key, KeyComparer.Instance)
            : defined.OrderByDescending(entry => entry.Key, KeyComparer.Instance);

        // OrderBy is stable, so ties keep the default order in both directions.
        return ordered.Concat(keyed.Where(entry => entry.Key is null)).Select(entry => entry.Row).ToList();
    }

    private static TableColumn<TRow> Find<TRow>(IReadOnlyList<TableColumn<TRow>> columns, string key) =>
        columns.FirstOrDefault(column => column.Key == key)
            ?? throw new ArgumentException($"The table has no column '{key}'.", nameof(key));

    private sealed class KeyComparer : IComparer<IComparable?>
    {
        public static KeyComparer Instance { get; } = new();

        public int Compare(IComparable? x, IComparable? y) =>
            x is string left && y is string right
                ? string.CompareOrdinal(left, right)
                : x!.CompareTo(y);
    }
}