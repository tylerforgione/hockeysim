using System.Globalization;

using Avalonia;
using Avalonia.Data.Converters;

using HockeySim.Desktop.Theme;

namespace HockeySim.Desktop.Game;

/// <summary>
/// Maps a shell page to its sidebar icon, keeping Avalonia geometry out of view models.
/// </summary>
public sealed class NavigationIconConverter : IValueConverter
{
    public static NavigationIconConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ShellPage.Home => Icons.Home,
        ShellPage.Inbox => Icons.Inbox,
        ShellPage.Roster => Icons.Roster,
        ShellPage.Lines => Icons.Lines,
        ShellPage.Teams => Icons.Teams,
        ShellPage.Standings => Icons.Standings,
        ShellPage.Playoffs => Icons.Playoffs,
        ShellPage.Schedule => Icons.Schedule,
        ShellPage.FreeAgents => Icons.FreeAgents,
        ShellPage.Trades => Icons.Trades,
        _ => AvaloniaProperty.UnsetValue,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}