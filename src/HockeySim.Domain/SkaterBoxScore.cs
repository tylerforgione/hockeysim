namespace HockeySim.Domain;

/// <summary>
/// A skater's individual production in one completed match. An entry is an appearance; shootout
/// attempts are never counted.
/// </summary>
public sealed record SkaterBoxScore
{
    /// <param name="plusMinus">
    /// Goals the team scored while the skater was on the ice, less those it conceded, excluding
    /// power-play goals.
    /// </param>
    /// <param name="timeOnIce">Time on the ice in regulation and overtime, in whole seconds.</param>
    /// <param name="shots">Shots on goal, including goals.</param>
    /// <param name="shotAttempts">Shots on goal, missed shots, and shots that were blocked.</param>
    /// <param name="blockedShots">The opponent's shot attempts this skater blocked.</param>
    /// <param name="expectedGoals">The summed expected-goal value of the skater's unblocked attempts.</param>
    public SkaterBoxScore(
        PlayerId playerId,
        int goals,
        int assists,
        int plusMinus,
        TimeSpan timeOnIce,
        int shots,
        int shotAttempts,
        int hits,
        int blockedShots,
        int faceoffsWon,
        int faceoffsLost,
        int takeaways,
        int giveaways,
        double expectedGoals)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(goals);
        ArgumentOutOfRangeException.ThrowIfNegative(assists);
        MatchTime.ThrowIfInvalid(timeOnIce, nameof(timeOnIce));
        ArgumentOutOfRangeException.ThrowIfLessThan(shots, goals);
        ArgumentOutOfRangeException.ThrowIfLessThan(shotAttempts, shots);
        ArgumentOutOfRangeException.ThrowIfNegative(hits);
        ArgumentOutOfRangeException.ThrowIfNegative(blockedShots);
        ArgumentOutOfRangeException.ThrowIfNegative(faceoffsWon);
        ArgumentOutOfRangeException.ThrowIfNegative(faceoffsLost);
        ArgumentOutOfRangeException.ThrowIfNegative(takeaways);
        ArgumentOutOfRangeException.ThrowIfNegative(giveaways);
        ExpectedGoalTotals.ThrowIfInvalid(expectedGoals, nameof(expectedGoals));

        PlayerId = playerId;
        Goals = goals;
        Assists = assists;
        PlusMinus = plusMinus;
        TimeOnIce = timeOnIce;
        Shots = shots;
        ShotAttempts = shotAttempts;
        Hits = hits;
        BlockedShots = blockedShots;
        FaceoffsWon = faceoffsWon;
        FaceoffsLost = faceoffsLost;
        Takeaways = takeaways;
        Giveaways = giveaways;
        ExpectedGoals = expectedGoals;
    }

    public PlayerId PlayerId { get; }

    public int Goals { get; }

    public int Assists { get; }

    public int Points => Goals + Assists;

    public int PlusMinus { get; }

    public TimeSpan TimeOnIce { get; }

    public int Shots { get; }

    public int ShotAttempts { get; }

    public int Hits { get; }

    public int BlockedShots { get; }

    public int FaceoffsWon { get; }

    public int FaceoffsLost { get; }

    public int Takeaways { get; }

    public int Giveaways { get; }

    public double ExpectedGoals { get; }
}