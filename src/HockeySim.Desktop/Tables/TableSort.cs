namespace HockeySim.Desktop.Tables;

public enum SortDirection
{
    Ascending,
    Descending,
}

/// <summary>
/// A table's chosen sort: the key of the column and the direction. Tables are unsorted (their
/// default order) until a header is clicked, so a table's sort is a nullable <see cref="TableSort"/>.
/// </summary>
public readonly record struct TableSort(string Column, SortDirection Direction)
{
    public SortDirection Opposite => Direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
}