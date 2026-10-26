namespace HockeySim.Desktop.Players;

/// <summary>
/// Which totals the roster tables, player profile, and team statistics page show. Playoff statistics are
/// kept apart from the regular season's and exist only once the playoffs start.
/// </summary>
public enum StatisticsSet
{
    RegularSeason,
    Playoffs,
}

public static class StatisticsSetExtensions
{
    /// <summary>
    /// Returns the requested totals when the playoffs have started, and the regular season's otherwise,
    /// since playoff statistics do not exist before then.
    /// </summary>
    public static StatisticsSet OrRegularSeason(this StatisticsSet statistics, bool hasPlayoffStatistics) =>
        hasPlayoffStatistics ? statistics : StatisticsSet.RegularSeason;
}