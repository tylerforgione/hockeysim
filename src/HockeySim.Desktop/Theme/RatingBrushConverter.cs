using System.Globalization;

using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace HockeySim.Desktop.Theme;

/// <summary>
/// Colours a 0–100 rating by tier so strengths and weaknesses stand out in dense tables.
/// </summary>
public sealed class RatingBrushConverter : IValueConverter
{
    public static string GetTierResourceKey(int rating) => rating switch
    {
        >= 80 => "RatingEliteBrush",
        >= 70 => "RatingGoodBrush",
        >= 60 => "RatingAverageBrush",
        >= 50 => "RatingWeakBrush",
        _ => "RatingPoorBrush",
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int rating)
        {
            return AvaloniaProperty.UnsetValue;
        }

        return Application.Current?.TryGetResource(GetTierResourceKey(rating), null, out var brush) == true
            ? brush as IBrush
            : AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}