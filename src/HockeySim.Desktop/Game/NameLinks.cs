using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

namespace HockeySim.Desktop.Game;

/// <summary>
/// Makes the commands behind player and team names. A name links nowhere when the page was given
/// no way to open it, so its command is absent and the name reads as plain text.
/// </summary>
public static class NameLinks
{
    /// <summary>Gets a command that opens the entity, or <see langword="null"/> without an <paramref name="open"/> callback.</summary>
    public static ICommand? Open<TId>(Action<TId>? open, TId id) =>
        open is null ? null : new RelayCommand(() => open(id));
}