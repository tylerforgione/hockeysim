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