namespace HockeySim.Domain;

/// <summary>
/// Rules for expected-goal (xG) totals: the summed chance of scoring of a set of unblocked shot
/// attempts. Totals are sums of fractions, so comparisons allow for rounding.
/// </summary>
internal static class ExpectedGoalTotals
{
    /// <summary>
    /// The largest difference treated as rounding when two totals of the same attempts, summed in
    /// different orders, are compared.
    /// </summary>
    public const double Tolerance = 1e-6;

    public static void ThrowIfInvalid(double expectedGoals, string parameterName)
    {
        if (!double.IsFinite(expectedGoals) || expectedGoals < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Expected goals must be a finite, non-negative value.");
        }
    }

    public static bool AreEqual(double first, double second) => Math.Abs(first - second) <= Tolerance;
}