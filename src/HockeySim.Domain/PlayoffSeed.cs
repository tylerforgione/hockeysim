namespace HockeySim.Domain;

/// <summary>
/// How a team qualified for the playoffs: as one of its division's top three, ranked within the
/// division, or as one of its conference's two wild cards, ranked within the wild-card race.
/// </summary>
/// <param name="Division">The team's own division, which for a wild card need not be the division whose bracket it plays in.</param>
/// <param name="Rank">The division finish (one to three), or the wild-card rank (one or two).</param>
public sealed record PlayoffSeed(TeamId TeamId, Conference Conference, Division Division, bool IsWildCard, int Rank)
{
    /// <summary>
    /// Whether this team placed higher than <paramref name="other"/> in its bracket, which decides
    /// home ice in the first two rounds whatever the teams' points: any division qualifier places
    /// above a wild card, and a better division finish above a worse one.
    /// </summary>
    internal bool PlacedAbove(PlayoffSeed other) =>
        IsWildCard != other.IsWildCard ? other.IsWildCard : Rank < other.Rank;
}