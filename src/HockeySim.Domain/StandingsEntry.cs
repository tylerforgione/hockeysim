namespace HockeySim.Domain;

/// <summary>
/// A team's place in a ranked group of teams, such as a division, conference, or the league.
/// </summary>
/// <param name="Rank">
/// One-based position. Teams that every official criterion leaves level share a rank; they are
/// listed in league team order rather than separated by an invented tie-breaker.
/// </param>
public sealed record StandingsEntry(int Rank, TeamRecord Record);