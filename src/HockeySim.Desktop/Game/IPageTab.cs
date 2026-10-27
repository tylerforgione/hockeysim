using System.ComponentModel;
using System.Windows.Input;

namespace HockeySim.Desktop.Game;

/// <summary>
/// A tab in the strip under the banner: one of the current section's pages, or one of the tabs
/// the current page supplies in their place (see <see cref="ShellPageViewModel.Tabs"/>).
/// </summary>
public interface IPageTab : INotifyPropertyChanged
{
    string Label { get; }

    bool IsActive { get; }

    /// <summary>Shows the tab's page; a tab that cannot be shown yet cannot execute, and is disabled.</summary>
    ICommand Command { get; }
}