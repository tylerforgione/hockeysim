using Avalonia.Media;

namespace HockeySim.Desktop.Theme;

/// <summary>
/// Line icons drawn on a 16×16 grid. Render them with a stroked <c>Path</c>, not a fill.
/// </summary>
public static class Icons
{
    public static Geometry Home { get; } = Geometry.Parse("M2,7.5 L8,2.5 L14,7.5 M3.5,6.5 V13.5 H6.5 V9.5 H9.5 V13.5 H12.5 V6.5");

    public static Geometry Inbox { get; } = Geometry.Parse("M1.5,3.5 H14.5 V12.5 H1.5 Z M1.5,3.5 L8,9 L14.5,3.5");

    public static Geometry Roster { get; } = Geometry.Parse("M1.5,4 H3.5 M1.5,8 H3.5 M1.5,12 H3.5 M5.5,4 H14.5 M5.5,8 H14.5 M5.5,12 H14.5");

    public static Geometry Lines { get; } = Geometry.Parse("M1.5,2.5 H14.5 V13.5 H1.5 Z M1.5,8 H14.5 M8,2.5 V13.5 M4.75,5.25 H4.8 M11.25,5.25 H11.3 M4.75,10.75 H4.8 M11.25,10.75 H11.3");

    public static Geometry Teams { get; } = Geometry.Parse("M8,1.5 L13.5,3.5 V8 C13.5,11 11,13.2 8,14.5 C5,13.2 2.5,11 2.5,8 V3.5 Z");

    public static Geometry Standings { get; } = Geometry.Parse("M2.5,13.5 V8.5 H5.5 V13.5 M6.5,13.5 V3.5 H9.5 V13.5 M10.5,13.5 V6.5 H13.5 V13.5 M1.5,13.5 H14.5");

    public static Geometry Playoffs { get; } = Geometry.Parse("M4.5,2 H11.5 V6 C11.5,8 10,9.5 8,9.5 C6,9.5 4.5,8 4.5,6 Z M4.5,3.5 H2.5 C2.5,5.5 3.5,6.5 4.8,6.8 M11.5,3.5 H13.5 C13.5,5.5 12.5,6.5 11.2,6.8 M8,9.5 V12 M6,12 H10 V14 H6 Z");

    public static Geometry Schedule { get; } = Geometry.Parse("M1.5,3.5 H14.5 V14 H1.5 Z M1.5,6.5 H14.5 M4.5,1.5 V4.5 M11.5,1.5 V4.5");

    public static Geometry FreeAgents { get; } = Geometry.Parse("M8,2 A2.75,2.75 0 1 1 7.99,2 Z M2.5,14 C2.5,10.5 5,9 8,9 C11,9 13.5,10.5 13.5,14");

    public static Geometry Trades { get; } = Geometry.Parse("M2,5 H13 M10,2 L13,5 L10,8 M14,11 H3 M6,8 L3,11 L6,14");
}