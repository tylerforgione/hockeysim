using HockeySim.Domain;

namespace HockeySim.Simulation.Events;

/// <summary>
/// A goal scored by a dressed player during regulation or overtime. A goal is also a shot on goal
/// and an unblocked shot attempt. Shootout goals are not goal events; they are recorded as
/// <see cref="ShootoutAttempt"/> values.
/// </summary>
/// <param name="TeamId">The scoring team.</param>
/// <param name="PrimaryAssistId">
/// The teammate on the ice who last passed to the scorer, or <see langword="null"/> for an
/// unassisted goal.
/// </param>
/// <param name="SecondaryAssistId">
/// A second on-ice teammate credited with an assist. Present only with a primary assist.
/// </param>
/// <param name="ExpectedGoals">
/// The scoring shot's expected-goal value, or <see langword="null"/> for an empty-net goal, which
/// has none.
/// </param>
/// <param name="Situation">Whether it was an even-strength, power-play, shorthanded, or penalty-shot goal.</param>
public sealed record GoalEvent(
    int Period,
    TimeSpan TimeInPeriod,
    OnIcePlayers OnIce,
    TeamId TeamId,
    PlayerId ScorerId,
    PlayerId? PrimaryAssistId,
    PlayerId? SecondaryAssistId,
    ShotContext Context,
    double? ExpectedGoals,
    GoalSituation Situation)
    : MatchEvent(Period, TimeInPeriod, OnIce)
{
    /// <summary>Scored while the conceding team's goalie was pulled for an extra attacker.</summary>
    public bool IsEmptyNet => Context.IsEmptyNet;
}