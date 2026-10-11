using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

using HockeySim.Domain;

namespace HockeySim.Desktop.Theme;

/// <summary>
/// Paints a team's colour, such as the swatch beside its name in a table.
/// </summary>
public sealed class ColourBrushConverter : IValueConverter
{
    public static ColourBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Colour colour ? new SolidColorBrush(TeamPalette.ToColor(colour)) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}