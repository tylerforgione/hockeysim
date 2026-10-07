namespace HockeySim.Simulation.Events;

/// <summary>
/// The circumstances of a shot attempt that its expected-goal value is calculated from.
/// </summary>
/// <param name="IsRebound">Taken from the rebound of a save moments earlier; always high danger.</param>
/// <param name="IsRush">Taken on the rush, straight after carrying the puck into the zone.</param>
public readonly record struct ShotContext(ShotDanger Danger, bool IsRebound, bool IsRush);