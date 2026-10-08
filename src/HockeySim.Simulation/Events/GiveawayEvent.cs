using HockeySim.Domain;

namespace HockeySim.Simulation.Events;

/// <summary>The puck carrier losing the puck to the opponent through their own error.</summary>
/// <param name="TeamId">The team that lost the puck.</param>
public sealed record GiveawayEvent(
    int Period,
    TimeSpan TimeInPeriod,
    OnIcePlayers OnIce,
    TeamId TeamId,
    PlayerId PlayerId)
    : MatchEvent(Period, TimeInPeriod, OnIce);