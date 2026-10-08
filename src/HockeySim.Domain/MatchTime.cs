namespace HockeySim.Domain;

/// <summary>
/// Rules for recorded match time. Time on ice is kept in whole seconds, the resolution the match
/// clock runs at, so it is stored and compared exactly.
/// </summary>
internal static class MatchTime
{
    public static void ThrowIfInvalid(TimeSpan time, string parameterName)
    {
        if (time < TimeSpan.Zero || time.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Match time must be a non-negative whole number of seconds.");
        }
    }
}