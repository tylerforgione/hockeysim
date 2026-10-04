namespace HockeySim.Domain;

/// <summary>
/// The starting goalie's record for one completed match. Shootout attempts and the shootout
/// deciding goal are never counted.
/// </summary>
public sealed record GoalieBoxScore
{
    public GoalieBoxScore(PlayerId playerId, int shotsAgainst, int goalsAgainst)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shotsAgainst);
        ArgumentOutOfRangeException.ThrowIfNegative(goalsAgainst);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(goalsAgainst, shotsAgainst);

        PlayerId = playerId;
        ShotsAgainst = shotsAgainst;
        GoalsAgainst = goalsAgainst;
    }

    public PlayerId PlayerId { get; }

    public int ShotsAgainst { get; }

    public int GoalsAgainst { get; }

    public int Saves => ShotsAgainst - GoalsAgainst;
}