using HockeySim.Domain;

namespace HockeySim.Desktop.Standings;

/// <summary>
/// The NHL's standings letters for a team's playoff status, and the legend explaining them.
/// </summary>
public static class PlayoffStatusDisplay
{
    private static readonly PlayoffStatus[] LegendOrder =
    [
        PlayoffStatus.ClinchedPlayoffSpot,
        PlayoffStatus.ClinchedDivision,
        PlayoffStatus.ClinchedConference,
        PlayoffStatus.ClinchedBestRecord,
        PlayoffStatus.Eliminated,
    ];

    /// <summary>"x", "y", "z", "p", or "e"; empty while the team's status is undecided.</summary>
    public static string Marker(PlayoffStatus status) => status switch
    {
        PlayoffStatus.Undecided => "",
        PlayoffStatus.Eliminated => "e",
        PlayoffStatus.ClinchedPlayoffSpot => "x",
        PlayoffStatus.ClinchedDivision => "y",
        PlayoffStatus.ClinchedConference => "z",
        PlayoffStatus.ClinchedBestRecord => "p",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown playoff status."),
    };

    /// <summary>What the marker means; <see langword="null"/> while the team's status is undecided.</summary>
    public static string? Describe(PlayoffStatus status) => status switch
    {
        PlayoffStatus.Undecided => null,
        PlayoffStatus.Eliminated => "Eliminated from playoff contention",
        PlayoffStatus.ClinchedPlayoffSpot => "Clinched a playoff spot",
        PlayoffStatus.ClinchedDivision => "Clinched the division",
        PlayoffStatus.ClinchedConference => "Clinched the conference",
        PlayoffStatus.ClinchedBestRecord => "Clinched the best record in the league",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown playoff status."),
    };

    /// <summary>Every marker with its meaning, such as "x Clinched a playoff spot".</summary>
    public static string Legend { get; } =
        string.Join(" · ", LegendOrder.Select(status => $"{Marker(status)} {Describe(status)}"));
}