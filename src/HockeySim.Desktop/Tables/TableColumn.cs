namespace HockeySim.Desktop.Tables;

/// <summary>
/// How one table column sorts its rows.
/// </summary>
/// <typeparam name="TRow">The row view model.</typeparam>
/// <param name="Key">The column's key, which its header passes when clicked.</param>
/// <param name="SortKey">
/// The row's value for the column, over the raw value rather than the displayed text: an
/// <see cref="int"/>, <see cref="double"/>, <see cref="TimeSpan"/>, or <see cref="DateOnly"/>, an
/// enum, or a <see cref="string"/> (compared ordinally). It is <see langword="null"/> where the
/// table shows a dash, and such rows sort last in either direction.
/// </param>
/// <param name="AscendingFirst">
/// Whether the first click sorts ascending: text and order-like columns (name, position, lineup)
/// start at the top of the alphabet; figures start with the highest.
/// </param>
public sealed record TableColumn<TRow>(string Key, Func<TRow, IComparable?> SortKey, bool AscendingFirst = false);