namespace HockeySim.Simulation.Randomness;

/// <summary>
/// Identifies the exact deterministic random stream position used by game workflows and match
/// simulation. Preserve this value with game state so later operations continue the stream
/// instead of rerolling an outcome.
/// </summary>
public readonly record struct RandomState(ulong Value);