using Avalonia.Media;

namespace HockeySim.Desktop.Theme;

/// <summary>
/// Line icons drawn on a 16×16 grid. Render them with a stroked <c>Path</c>, not a fill.
/// </summary>
public static class Icons
{
    public static Geometry Back { get; } = Geometry.Parse("M10,3 L5,8 L10,13");

    public static Geometry Home { get; } = Geometry.Parse("M2,7.5 L8,2.5 L14,7.5 M3.5,6.5 V13.5 H6.5 V9.5 H9.5 V13.5 H12.5 V6.5");

    public static Geometry Forward { get; } = Geometry.Parse("M6,3 L11,8 L6,13");

    public static Geometry Expand { get; } = Geometry.Parse("M4,6 L8,10 L12,6");
}