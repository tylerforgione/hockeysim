using System.Globalization;

using Avalonia.Data.Converters;

namespace HockeySim.Desktop.Theme;

/// <summary>
/// Upper-cases bound text for panel headers, which Avalonia cannot transform in a style.
/// </summary>
public sealed class UpperCaseConverter : IValueConverter
{
    public static UpperCaseConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString()?.ToUpper(culture);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}