namespace HockeySim.Domain;

/// <summary>
/// Rules for when something happened in a match: a period numbered from one and an elapsed time
/// in whole seconds no longer than the longest period, twenty minutes.
/// </summary>
internal static class MatchClock
{
    public static readonly TimeSpan LongestPeriod = TimeSpan.FromMinutes(20);

    public static void ThrowIfInvalid(int period, TimeSpan timeInPeriod)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        MatchTime.ThrowIfInvalid(timeInPeriod, nameof(timeInPeriod));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(timeInPeriod, LongestPeriod);
    }
}