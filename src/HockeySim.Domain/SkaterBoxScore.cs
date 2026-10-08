namespace HockeySim.Domain;

/// <summary>
/// A skater's individual production in one completed match. An entry is an appearance; shootout
/// attempts are never counted.
/// </summary>
public sealed record SkaterBoxScore
{
    /// <param name="plusMinus">
    /// Goals the team scored while the skater was on the ice, less those it conceded, excluding
    /// power-play and penalty-shot goals.
    /// </param>
    /// <param name="timeOnIce">Time on the ice in regulation and overtime, in whole seconds.</param>
    /// <param name="shots">Shots on goal, including goals.</param>
    /// <param name="shotAttempts">Shots on goal, missed shots, and shots that were blocked.</param>
    /// <param name="blockedShots">The opponent's shot attempts this skater blocked.</param>
    /// <param name="expectedGoals">The summed expected-goal value of the skater's unblocked attempts.</param>
    /// <param name="penaltyMinutes">Minutes of every penalty assessed to the skater.</param>
    /// <param name="powerPlayGoals">Goals scored on the power play, counted among the goals.</param>
    /// <param name="powerPlayAssists">Assists on power-play goals, counted among the assists.</param>
    /// <param name="shorthandedGoals">Goals scored shorthanded, counted among the goals.</param>
    /// <param name="shorthandedAssists">Assists on shorthanded goals, counted among the assists.</param>
    /// <param name="emptyNetGoals">
    /// Goals scored into a net whose goalie was pulled for an extra attacker, counted among the goals.
    /// </param>
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
        double expectedGoals,
        int penaltyMinutes,
        int powerPlayGoals,
        int powerPlayAssists,
        int shorthandedGoals,
        int shorthandedAssists,
        int emptyNetGoals)
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
        ArgumentOutOfRangeException.ThrowIfNegative(penaltyMinutes);
        ArgumentOutOfRangeException.ThrowIfNegative(powerPlayGoals);
        ArgumentOutOfRangeException.ThrowIfNegative(powerPlayAssists);
        ArgumentOutOfRangeException.ThrowIfNegative(shorthandedGoals);
        ArgumentOutOfRangeException.ThrowIfNegative(shorthandedAssists);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(powerPlayGoals + shorthandedGoals, goals, nameof(shorthandedGoals));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(powerPlayAssists + shorthandedAssists, assists, nameof(shorthandedAssists));
        ArgumentOutOfRangeException.ThrowIfNegative(emptyNetGoals);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(emptyNetGoals, goals);

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
        PenaltyMinutes = penaltyMinutes;
        PowerPlayGoals = powerPlayGoals;
        PowerPlayAssists = powerPlayAssists;
        ShorthandedGoals = shorthandedGoals;
        ShorthandedAssists = shorthandedAssists;
        EmptyNetGoals = emptyNetGoals;
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

    public int PenaltyMinutes { get; }

    public int PowerPlayGoals { get; }

    public int PowerPlayAssists { get; }

    public int PowerPlayPoints => PowerPlayGoals + PowerPlayAssists;

    public int ShorthandedGoals { get; }

    public int ShorthandedAssists { get; }

    public int ShorthandedPoints => ShorthandedGoals + ShorthandedAssists;

    public int EmptyNetGoals { get; }
}