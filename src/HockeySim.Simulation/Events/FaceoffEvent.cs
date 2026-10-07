using HockeySim.Domain;

namespace HockeySim.Simulation.Events;

/// <summary>
/// A faceoff between the two centres on the ice, which starts every period and restarts play
/// after every stoppage.
/// </summary>
/// <param name="WinnerTeamId">The team that won possession.</param>
public sealed record FaceoffEvent(
    int Period,
    TimeSpan TimeInPeriod,
    OnIcePlayers OnIce,
    TeamId WinnerTeamId,
    PlayerId WinnerId,
    PlayerId LoserId)
    : MatchEvent(Period, TimeInPeriod, OnIce);