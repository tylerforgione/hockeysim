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

    private static readonly Color LightText = Colors.White;
    private static readonly Color DarkText = Color.Parse("#0D1014");

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

    private static Color ToColor(Colour colour) => Color.FromRgb(colour.Red, colour.Green, colour.Blue);

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