namespace HockeySim.Domain;

/// <summary>
/// One team's shots on goal, expected goals, and power-play opportunities in one period of a
/// completed match. Goals by period are in the scoring summary.
/// </summary>
public sealed record PeriodTotals
{
    /// <param name="period">One to three for regulation, then each overtime period in turn.</param>
    /// <param name="shots">Shots on goal in the period, including goals.</param>
    /// <param name="expectedGoals">The summed expected-goal value of the period's unblocked attempts.</param>
    /// <param name="powerPlayOpportunities">
    /// Power-play opportunities that began in the period: each opponent penalty counts in the
    /// period in which it first gave the team the advantage.
    /// </param>
    public PeriodTotals(int period, int shots, double expectedGoals, int powerPlayOpportunities)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(shots);
        ExpectedGoalTotals.ThrowIfInvalid(expectedGoals, nameof(expectedGoals));
        ArgumentOutOfRangeException.ThrowIfNegative(powerPlayOpportunities);

        Period = period;
        Shots = shots;
        ExpectedGoals = expectedGoals;
        PowerPlayOpportunities = powerPlayOpportunities;
    }

    public int Period { get; }

    public int Shots { get; }

    public double ExpectedGoals { get; }

    public int PowerPlayOpportunities { get; }
}