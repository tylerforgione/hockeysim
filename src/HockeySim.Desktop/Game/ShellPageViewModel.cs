using CommunityToolkit.Mvvm.ComponentModel;

namespace HockeySim.Desktop.Game;

/// <summary>
/// A page hosted in the game shell's content area.
/// </summary>
public abstract class ShellPageViewModel : ObservableObject
{
    public abstract string Title { get; }

    public abstract string Subtitle { get; }

    /// <summary>
    /// Re-renders the page from the session's latest snapshot.
    /// </summary>
    public abstract void Refresh();
}