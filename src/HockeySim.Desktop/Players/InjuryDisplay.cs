using System.Globalization;

using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Players;

/// <summary>
/// Formats injuries as the staff report them. Only the expected return is shown: the actual
/// recovery time, like wear and durability, stays hidden.
/// </summary>
public static class InjuryDisplay
{
    /// <summary>The roster marker: OUT for a player who cannot play, INJ for one playing hurt.</summary>
    public static string? StatusMarker(PlayerSnapshot player) =>
        player.Injuries.Count == 0 ? null : player.CanPlay ? "INJ" : "OUT";

    public static string Status(InjurySnapshot injury) =>
        injury.CanPlayThrough ? "Playing through" : "Out";

    /// <summary>For example "Out: high ankle sprain, expected back Oct 12 – Oct 20".</summary>
    public static string Summary(PlayerSnapshot player) =>
        string.Join(
            "\n",
            player.Injuries.Select(injury => $"{Status(injury)}: {Name(injury.Type)}, expected back {ExpectedReturn(injury.ExpectedReturn)}"));

    public static string ExpectedReturn(ExpectedReturn expected) =>
        expected.Earliest == expected.Latest
            ? ShortDate(expected.Earliest)
            : $"{ShortDate(expected.Earliest)} – {ShortDate(expected.Latest)}";

    /// <summary>
    /// When the injuries heal, such as "Injured until Sun 23 Nov", or with the earliest and latest
    /// dates ("Injured until Sun 23 Nov – Wed 26 Nov") when the staff cannot say which day. It
    /// follows the injury that heals last; null when there is none.
    /// </summary>
    public static string? InjuredUntil(IReadOnlyList<InjurySnapshot> injuries)
    {
        ArgumentNullException.ThrowIfNull(injuries);

        if (injuries.Count == 0)
        {
            return null;
        }

        var expected = injuries.MaxBy(injury => injury.ExpectedReturn.Latest)!.ExpectedReturn;
        return expected.Earliest == expected.Latest
            ? $"Injured until {DayDate(expected.Earliest)}"
            : $"Injured until {DayDate(expected.Earliest)} – {DayDate(expected.Latest)}";
    }

    public static string Name(InjuryType type) => type switch
    {
        InjuryType.Concussion => "Concussion",
        InjuryType.BrokenNose => "Broken nose",
        InjuryType.SeparatedShoulder => "Separated shoulder",
        InjuryType.BruisedShoulder => "Bruised shoulder",
        InjuryType.BrokenHand => "Broken hand",
        InjuryType.BrokenFinger => "Broken finger",
        InjuryType.BruisedRibs => "Bruised ribs",
        InjuryType.BackSpasms => "Back spasms",
        InjuryType.GroinStrain => "Groin strain",
        InjuryType.TightGroin => "Tight groin",
        InjuryType.SprainedKnee => "Sprained knee",
        InjuryType.BruisedKnee => "Bruised knee",
        InjuryType.HighAnkleSprain => "High ankle sprain",
        InjuryType.SprainedAnkle => "Sprained ankle",
        InjuryType.BrokenFoot => "Broken foot",
        InjuryType.BruisedFoot => "Bruised foot",
        _ => type.ToString(),
    };

    private static string DayDate(DateOnly date) => date.ToString("ddd d MMM", CultureInfo.CurrentCulture);

    private static string ShortDate(DateOnly date) => date.ToString("MMM d", CultureInfo.CurrentCulture);
}