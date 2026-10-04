using HockeySim.Domain;

namespace HockeySim.Simulation;

/// <summary>
/// One dressed skater's production in a match. Every dressed skater has an entry, including
/// those who recorded nothing; scratched players have none. Shootout attempts are not counted.
/// </summary>
public sealed record SkaterMatchStatistics(PlayerId PlayerId, int Goals, int Assists)
{
    /// <summary>
    /// Always one: an entry is an appearance. Exposed so season totals can sum it directly.
    /// </summary>
    public int GamesPlayed => 1;

    public int Points => Goals + Assists;
}