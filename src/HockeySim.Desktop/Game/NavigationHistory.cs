namespace HockeySim.Desktop.Game;

/// <summary>
/// A browser-style history: the entry shown, the entries behind it, and the entries ahead after
/// going back. Visiting somewhere new discards what was ahead. Entries are compared by value, so
/// visiting the entry already shown adds nothing.
/// </summary>
/// <typeparam name="T">The entry, which should have value equality.</typeparam>
public sealed class NavigationHistory<T>
{
    /// <summary>The most entries kept behind the current one; the oldest are dropped beyond it.</summary>
    public const int DefaultCapacity = 100;

    private readonly int _capacity;
    private readonly List<T> _back = [];
    private readonly List<T> _forward = [];

    /// <param name="initial">The entry shown at the start.</param>
    /// <param name="capacity">The most entries kept behind the current one.</param>
    public NavigationHistory(T initial, int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        Current = initial;
        _capacity = capacity;
    }

    public T Current { get; private set; }

    public bool CanGoBack => _back.Count > 0;

    public bool CanGoForward => _forward.Count > 0;

    /// <summary>
    /// Moves to a new entry, keeping the current one behind it and discarding the entries ahead.
    /// Does nothing when the entry is the current one.
    /// </summary>
    public void Visit(T entry)
    {
        if (EqualityComparer<T>.Default.Equals(entry, Current))
        {
            return;
        }

        _back.Add(Current);
        if (_back.Count > _capacity)
        {
            _back.RemoveAt(0);
        }

        _forward.Clear();
        Current = entry;
    }

    /// <summary>
    /// Replaces the current entry without moving, so the history can record how the page was left.
    /// </summary>
    public void ReplaceCurrent(T entry)
    {
        Current = entry;
    }

    /// <summary>Moves to the previous entry and returns it.</summary>
    /// <exception cref="InvalidOperationException">There is no entry behind the current one.</exception>
    public T GoBack()
    {
        if (!CanGoBack)
        {
            throw new InvalidOperationException("There is no earlier entry.");
        }

        _forward.Add(Current);
        Current = _back[^1];
        _back.RemoveAt(_back.Count - 1);
        return Current;
    }

    /// <summary>Moves to the next entry and returns it.</summary>
    /// <exception cref="InvalidOperationException">There is no entry ahead of the current one.</exception>
    public T GoForward()
    {
        if (!CanGoForward)
        {
            throw new InvalidOperationException("There is no later entry.");
        }

        _back.Add(Current);
        Current = _forward[^1];
        _forward.RemoveAt(_forward.Count - 1);
        return Current;
    }
}