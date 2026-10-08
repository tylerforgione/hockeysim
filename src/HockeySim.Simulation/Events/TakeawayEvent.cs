using HockeySim.Domain;

namespace HockeySim.Simulation.Events;

/// <summary>A defending skater winning the puck from the carrier.</summary>
/// <param name="TeamId">The team that won the puck.</param>
public sealed record TakeawayEvent(
    int Period,
    TimeSpan TimeInPeriod,
    OnIcePlayers OnIce,
    TeamId TeamId,
    PlayerId PlayerId)
    : MatchEvent(Period, TimeInPeriod, OnIce);