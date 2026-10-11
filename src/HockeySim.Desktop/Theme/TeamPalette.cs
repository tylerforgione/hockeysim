using Avalonia.Controls;
using Avalonia.Media;

using HockeySim.Domain;

namespace HockeySim.Desktop.Theme;

/// <summary>
/// Colours the window with a team's identity by replacing the team brushes that Palette.axaml
/// defines. Removing them restores the league defaults.
/// </summary>
public static class TeamPalette
{
    public const string PrimaryKey = "TeamPrimaryBrush";
    public const string OnPrimaryKey = "OnTeamPrimaryBrush";
    public const string SecondaryKey = "TeamSecondaryBrush";
    public const string OnSecondaryKey = "OnTeamSecondaryBrush";

    // Below this contrast with the surface, a team colour is too dark to pick out.
    private const double MinimumSurfaceContrast = 1.3;

    // Colours nearer than this, as a distance in red, green, and blue, look alike side by side.
    private const int LookAlikeDistance = 100;

    private static readonly Color LightText = Colors.White;
    private static readonly Color DarkText = Color.Parse("#0D1014");

    // Palette.axaml's SurfaceBrush, which panels and tables sit on.
    private static readonly Color Surface = Color.Parse("#20262D");

    /// <summary>
    /// Sets <paramref name="resources"/> to <paramref name="colours"/>, or clears them back to the
    /// defaults inherited from the application when there is no team.
    /// </summary>
    public static void Apply(IResourceDictionary resources, TeamColours? colours)
    {
        ArgumentNullException.ThrowIfNull(resources);

        if (colours is null)
        {
            foreach (var key in new[] { PrimaryKey, OnPrimaryKey, SecondaryKey, OnSecondaryKey })
            {
                resources.Remove(key);
            }

            return;
        }

        var primary = ToColor(colours.Primary);
        resources[PrimaryKey] = new SolidColorBrush(primary);
        resources[OnPrimaryKey] = new SolidColorBrush(ReadableTextOn(primary));
        resources[SecondaryKey] = new SolidColorBrush(ToColor(colours.Secondary));
        // Management keeps every team's pair far enough apart for primary-coloured text.
        resources[OnSecondaryKey] = new SolidColorBrush(primary);
    }

    /// <summary>Picks whichever of light or dark text contrasts more with the background.</summary>
    public static Color ReadableTextOn(Color background) =>
        ContrastRatio(background, LightText) >= ContrastRatio(background, DarkText) ? LightText : DarkText;

    /// <summary>
    /// Two teams' colours for a chart on the app's dark panels, such as a box score's comparison
    /// bars. Each team shows its primary, or its secondary when the primary is too dark to see
    /// there; the home team switches to its other colour if the two would look alike.
    /// </summary>
    public static (Colour Away, Colour Home) ChartColours(TeamColours away, TeamColours home)
    {
        ArgumentNullException.ThrowIfNull(away);
        ArgumentNullException.ThrowIfNull(home);

        var awayColour = VisibleColours(away).First();
        var homeColours = VisibleColours(home).ToList();
        return (awayColour, homeColours.FirstOrDefault(colour => !LookAlike(colour, awayColour), homeColours[0]));
    }

    public static Color ToColor(Colour colour) => Color.FromRgb(colour.Red, colour.Green, colour.Blue);

    /// <summary>
    /// The team's colours that can be seen on the dark panels, primary first; the secondary alone
    /// when neither stands out, since secondaries are the lighter accents.
    /// </summary>
    private static IEnumerable<Colour> VisibleColours(TeamColours colours)
    {
        var visible = new[] { colours.Primary, colours.Secondary }
            .Where(colour => ContrastRatio(ToColor(colour), Surface) >= MinimumSurfaceContrast)
            .ToList();
        return visible.Count > 0 ? visible : [colours.Secondary];
    }

    /// <summary>Colours close enough in red, green, and blue to be mistaken for each other in a chart.</summary>
    private static bool LookAlike(Colour first, Colour second)
    {
        var (red, green, blue) = (first.Red - second.Red, first.Green - second.Green, first.Blue - second.Blue);
        return (red * red) + (green * green) + (blue * blue) < LookAlikeDistance * LookAlikeDistance;
    }

    private static double ContrastRatio(Color first, Color second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    /// <summary>WCAG relative luminance of an sRGB colour.</summary>
    private static double RelativeLuminance(Color colour)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linear(colour.R)) + (0.7152 * Linear(colour.G)) + (0.0722 * Linear(colour.B));
    }
}