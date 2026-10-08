using HockeySim.Domain;

namespace HockeySim.Simulation.Events;

/// <summary>
/// A player injured in the play. A player whose injury cannot be played through takes no further
/// shifts; one playing through it plays on at reduced ratings. The hidden wear the play added is
/// in <see cref="MatchResult.Wear"/>, not the play-by-play.
/// </summary>
/// <param name="TeamId">The injured player's team.</param>
/// <param name="RecoveryDays">League days the injury takes to heal.</param>
public sealed record InjuryEvent(
    int Period,
    TimeSpan TimeInPeriod,
    OnIcePlayers OnIce,
    TeamId TeamId,
    PlayerId PlayerId,
    InjuryType Type,
    int RecoveryDays)
    : MatchEvent(Period, TimeInPeriod, OnIce);