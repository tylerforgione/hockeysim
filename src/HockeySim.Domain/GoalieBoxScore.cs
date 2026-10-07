namespace HockeySim.Domain;

/// <summary>
/// The starting goalie's record for one completed match. Shootout attempts and the shootout
/// deciding goal are never counted.
/// </summary>
public sealed record GoalieBoxScore
{
    /// <param name="expectedGoalsAgainst">The summed expected-goal value of the opponent's unblocked attempts.</param>
    /// <param name="timeOnIce">Time in net in regulation and overtime, in whole seconds.</param>
    public GoalieBoxScore(
        PlayerId playerId,
        int shotsAgainst,
        int goalsAgainst,
        double expectedGoalsAgainst,
        TimeSpan timeOnIce)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shotsAgainst);
        ArgumentOutOfRangeException.ThrowIfNegative(goalsAgainst);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(goalsAgainst, shotsAgainst);
        ExpectedGoalTotals.ThrowIfInvalid(expectedGoalsAgainst, nameof(expectedGoalsAgainst));
        MatchTime.ThrowIfInvalid(timeOnIce, nameof(timeOnIce));

        PlayerId = playerId;
        ShotsAgainst = shotsAgainst;
        GoalsAgainst = goalsAgainst;
        ExpectedGoalsAgainst = expectedGoalsAgainst;
        TimeOnIce = timeOnIce;
    }

    public PlayerId PlayerId { get; }

    public int ShotsAgainst { get; }

    public int GoalsAgainst { get; }

    public int Saves => ShotsAgainst - GoalsAgainst;

    public double ExpectedGoalsAgainst { get; }

    public TimeSpan TimeOnIce { get; }
}