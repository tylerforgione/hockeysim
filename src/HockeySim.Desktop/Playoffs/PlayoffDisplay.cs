using System.Globalization;

using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Playoffs;

/// <summary>
/// Formats playoff rounds, seeds, and series consistently across the bracket, schedule, and home page.
/// </summary>
public static class PlayoffDisplay
{
    public static string RoundName(PlayoffRound round) => round switch
    {
        PlayoffRound.FirstRound => "First round",
        PlayoffRound.SecondRound => "Second round",
        PlayoffRound.ConferenceFinal => "Conference final",
        PlayoffRound.Final => "Final",
        _ => throw new ArgumentOutOfRangeException(nameof(round), round, "Unknown playoff round."),
    };

    /// <summary>A round as the schedule abbreviates it: "R1", "R2", "CF", or "F".</summary>
    public static string RoundAbbreviation(PlayoffRound round) => round switch
    {
        PlayoffRound.FirstRound => "R1",
        PlayoffRound.SecondRound => "R2",
        PlayoffRound.ConferenceFinal => "CF",
        PlayoffRound.Final => "F",
        _ => throw new ArgumentOutOfRangeException(nameof(round), round, "Unknown playoff round."),
    };

    /// <summary>
    /// How a team qualified, as the NHL writes it: its division's initial and finish ("N1"), or
    /// its wild-card rank ("WC2"). The generated league's division names have distinct initials.
    /// </summary>
    public static string SeedLabel(PlayoffSeedSnapshot seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        return seed.IsWildCard
            ? string.Create(CultureInfo.CurrentCulture, $"WC{seed.Rank}")
            : string.Create(CultureInfo.CurrentCulture, $"{seed.DivisionName[0]}{seed.Rank}");
    }

    /// <summary>The seed spelled out for a tooltip, such as "Northern Division, 1st" or "Wild card 2".</summary>
    public static string SeedDescription(PlayoffSeedSnapshot seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        return seed.IsWildCard
            ? string.Create(CultureInfo.CurrentCulture, $"{seed.ConferenceName} wild card {seed.Rank}")
            : $"{seed.DivisionName}, {Ordinal(seed.Rank)}";
    }

    /// <summary>
    /// Describes where a series stands: not started, tied, who leads, or who won, with the
    /// leader's wins first ("Owls lead 3–1").
    /// </summary>
    public static string SeriesStatus(PlayoffSeriesSnapshot series, Func<TeamId, string> teamName)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(teamName);

        var (higher, lower) = (series.HigherRankedWins, series.LowerRankedWins);
        if (higher + lower == 0)
        {
            return "Series starts";
        }

        if (higher == lower)
        {
            return string.Create(CultureInfo.CurrentCulture, $"Series tied {higher}–{lower}");
        }

        var (leader, most, least) = higher > lower
            ? (series.HigherRanked.TeamId, higher, lower)
            : (series.LowerRanked.TeamId, lower, higher);
        var verb = series.WinnerId is null ? "lead" : "win";
        return string.Create(CultureInfo.CurrentCulture, $"{teamName(leader)} {verb} {most}–{least}");
    }

    /// <summary>
    /// Finds the series a playoff game on <paramref name="date"/> belongs to, with the game's
    /// number in it, played or scheduled; none when the team played no playoff game that day.
    /// </summary>
    public static (PlayoffSeriesSnapshot Series, int GameNumber)? FindGame(PlayoffsSnapshot? playoffs, DateOnly date, TeamId teamId)
    {
        foreach (var series in playoffs?.Series ?? [])
        {
            if (series.HigherRanked.TeamId != teamId && series.LowerRanked.TeamId != teamId)
            {
                continue;
            }

            var played = series.Games.ToList().FindIndex(game => game.Date == date);
            if (played >= 0)
            {
                return (series, played + 1);
            }

            if (series.NextGame?.Date == date)
            {
                return (series, series.Games.Count + 1);
            }
        }

        return null;
    }

    private static string Ordinal(int rank) => rank switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => string.Create(CultureInfo.CurrentCulture, $"{rank}th"),
    };
}