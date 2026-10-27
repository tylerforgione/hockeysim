using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace HockeySim.Desktop.Roster;

/// <summary>Opens the selected player's profile when a row of a roster table is double-clicked.</summary>
internal static class RosterRowActivation
{
    /// <summary>
    /// Handles double-taps anywhere in <paramref name="table"/>, whose data context is the roster:
    /// on a row, never on a column header.
    /// </summary>
    public static void OpenProfileOnDoubleTap(Control table)
    {
        table.AddHandler(InputElement.DoubleTappedEvent, (_, e) =>
        {
            if (e.Source is Visual source
                && source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not null
                && table.DataContext is TeamRosterViewModel { OpenProfileCommand: { } open }
                && open.CanExecute(null))
            {
                open.Execute(null);
            }
        });
    }
}