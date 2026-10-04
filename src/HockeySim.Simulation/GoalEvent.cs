using HockeySim.Domain;

namespace HockeySim.Simulation;

/// <summary>
/// A goal scored by a dressed player during regulation or overtime. Shootout goals are not
/// goal events; they are recorded as <see cref="ShootoutAttempt"/> values.
/// </summary>
/// <param name="PrimaryAssistId">
/// The teammate on the ice who last passed to the scorer, or <see langword="null"/> for an
/// unassisted goal.
/// </param>
/// <param name="SecondaryAssistId">
/// A second on-ice teammate credited with an assist. Present only with a primary assist.
/// </param>
/// <param name="Period">
/// One to three for regulation periods, or <see cref="MatchResult.OvertimePeriod"/>.
/// </param>
/// <param name="TimeInPeriod">Elapsed time in the period when the goal was scored.</param>
public sealed record GoalEvent(
    TeamId TeamId,
    PlayerId ScorerId,
    PlayerId? PrimaryAssistId,
    PlayerId? SecondaryAssistId,
    int Period,
    TimeSpan TimeInPeriod);