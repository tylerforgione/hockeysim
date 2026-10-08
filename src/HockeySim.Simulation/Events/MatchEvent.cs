namespace HockeySim.Simulation.Events;

/// <summary>
/// One entry in a match's play-by-play. Every event records when it happened and who was on the
/// ice for both teams at that moment.
/// </summary>
/// <param name="Period">
/// One to three for regulation, then <see cref="MatchResult.OvertimePeriod"/> and, in playoff
/// overtime, each further overtime period in turn.
/// </param>
/// <param name="TimeInPeriod">
/// Elapsed time in the period, in whole seconds. Only a delayed penalty called as the period ends
/// is recorded at the period's full length.
/// </param>
public abstract record MatchEvent(int Period, TimeSpan TimeInPeriod, OnIcePlayers OnIce)
{
    /// <summary>How many skaters each team had on the ice.</summary>
    public StrengthState Strength => OnIce.Strength;
}