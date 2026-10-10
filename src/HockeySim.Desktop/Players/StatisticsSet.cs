namespace HockeySim.Desktop.Players;

/// <summary>
/// Which totals the roster tables, team strip, and player profile show. Playoff statistics are
/// kept apart from the regular season's and exist only once the playoffs start.
/// </summary>
public enum StatisticsSet
{
    RegularSeason,
    Playoffs,
}