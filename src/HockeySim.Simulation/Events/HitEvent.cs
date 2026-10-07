using HockeySim.Domain;

namespace HockeySim.Simulation.Events;

/// <summary>A body check on the puck carrier.</summary>
/// <param name="TeamId">The hitter's team.</param>
public sealed record HitEvent(
    int Period,
    TimeSpan TimeInPeriod,
    OnIcePlayers OnIce,
    TeamId TeamId,
    PlayerId HitterId,
    PlayerId HitPlayerId)
    : MatchEvent(Period, TimeInPeriod, OnIce);