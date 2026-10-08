namespace HockeySim.Simulation.Events;

/// <summary>
/// How a goal counts toward special-teams statistics, decided by the penalties being served
/// rather than by who is on the ice: an extra attacker during a delayed penalty does not make a
/// power play.
/// </summary>
public enum GoalSituation
{
    /// <summary>Both teams had the same number of skaters available.</summary>
    EvenStrength,

    /// <summary>The scoring team had more skaters because the opponent was serving penalties.</summary>
    PowerPlay,

    /// <summary>The scoring team had fewer skaters because it was serving penalties.</summary>
    Shorthanded,

    /// <summary>
    /// Scored on a penalty shot. It is neither a power-play nor a shorthanded goal, ends no
    /// penalty, and does not count toward plus/minus.
    /// </summary>
    PenaltyShot,
}