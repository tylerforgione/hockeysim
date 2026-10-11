using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

using HockeySim.Domain;

namespace HockeySim.Desktop.Theme;

/// <summary>
/// Paints text that reads on a team's colour: light or dark, whichever contrasts more. Used where
/// a team other than the managed one is shown in its own colours, such as a box score's banner.
/// </summary>
public sealed class ReadableTextBrushConverter : IValueConverter
{
    public static ReadableTextBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Colour colour ? new SolidColorBrush(TeamPalette.ReadableTextOn(TeamPalette.ToColor(colour))) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}