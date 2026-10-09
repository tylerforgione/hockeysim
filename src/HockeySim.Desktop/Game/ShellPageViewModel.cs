using CommunityToolkit.Mvvm.ComponentModel;

namespace HockeySim.Desktop.Game;

/// <summary>
/// A page hosted in the game shell's content area.
/// </summary>
public abstract class ShellPageViewModel : ObservableObject
{
    /// <summary>Gets the line shown at the right of the page tabs, such as whose page it is.</summary>
    public abstract string Subtitle { get; }

    /// <summary>
    /// Gets the banner shown in place of the managed team's, such as a player's or a match's, or
    /// null to show the team's.
    /// </summary>
    public virtual object? Banner => null;

    /// <summary>
    /// Re-renders the page from the session's latest snapshot.
    /// </summary>
    public abstract void Refresh();
}