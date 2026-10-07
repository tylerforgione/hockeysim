using HockeySim.Domain;

namespace HockeySim.Simulation.Events;

/// <summary>
/// A shot attempt that did not score. A goal is a <see cref="GoalEvent"/> instead.
/// </summary>
/// <param name="TeamId">The shooting team.</param>
/// <param name="BlockerId">The defending skater who blocked the attempt; present only when blocked.</param>
/// <param name="ExpectedGoals">
/// The attempt's expected-goal value, present only when it was not blocked: the chance that a
/// league-average shooter scores on a league-average goalie from the same context.
/// </param>
public sealed record ShotAttemptEvent(
    int Period,
    TimeSpan TimeInPeriod,
    OnIcePlayers OnIce,
    TeamId TeamId,
    PlayerId ShooterId,
    ShotContext Context,
    ShotOutcome Outcome,
    PlayerId? BlockerId,
    double? ExpectedGoals)
    : MatchEvent(Period, TimeInPeriod, OnIce)
{
    public bool IsOnGoal => Outcome == ShotOutcome.Saved;
}