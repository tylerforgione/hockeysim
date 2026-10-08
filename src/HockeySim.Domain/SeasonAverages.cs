namespace HockeySim.Domain;

/// <summary>Per-game and per-sixty-minute rates of season totals, undefined before any play.</summary>
internal static class SeasonAverages
{
    private static readonly TimeSpan SixtyMinutes = TimeSpan.FromMinutes(60);

    /// <summary>
    /// The average per game, rounded to the nearest whole second like every recorded time (a half
    /// second up), or <see langword="null"/> with no games.
    /// </summary>
    public static TimeSpan? PerGame(TimeSpan total, int gamesPlayed) =>
        gamesPlayed == 0
            ? null
            : TimeSpan.FromSeconds(Math.Round(total.TotalSeconds / gamesPlayed, MidpointRounding.AwayFromZero));

    /// <summary>A count per sixty minutes played, or <see langword="null"/> with no time played.</summary>
    public static double? PerSixtyMinutes(int count, TimeSpan timePlayed) =>
        timePlayed == TimeSpan.Zero ? null : count * (SixtyMinutes / timePlayed);
}