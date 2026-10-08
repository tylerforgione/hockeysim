using HockeySim.Domain;

namespace HockeySim.Simulation.Events;

/// <summary>
/// A penalty assessed to a skater, recorded when play stops for it. A foul by the team without
/// the puck is a delayed penalty: play continues until that team touches the puck, so the
/// penalty is recorded at the whistle, and not at all when a goal scored during the delay wipes
/// out a minor. Coincidental penalties are recorded together at the same moment, and a fight is
/// a fighting major to each fighter.
/// </summary>
/// <param name="TeamId">The penalized team.</param>
public sealed record PenaltyEvent(
    int Period,
    TimeSpan TimeInPeriod,
    OnIcePlayers OnIce,
    TeamId TeamId,
    PlayerId PlayerId,
    Infraction Infraction,
    PenaltyKind Kind)
    : MatchEvent(Period, TimeInPeriod, OnIce)
{
    /// <summary>The penalty minutes charged to the player.</summary>
    public int Minutes => MinutesFor(Kind);

    public static int MinutesFor(PenaltyKind kind) => kind switch
    {
        PenaltyKind.Minor => 2,
        PenaltyKind.DoubleMinor => 4,
        PenaltyKind.Major => 5,
        PenaltyKind.Misconduct => 10,
        PenaltyKind.GameMisconduct => 10,
        PenaltyKind.PenaltyShot => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), "The penalty kind must be defined."),
    };
}